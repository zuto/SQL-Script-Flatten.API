using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using API.Models;
using Data.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SQLScriptFlatten.API;

namespace API.Services;

public class ScriptService : IScriptService
{
    private readonly IScriptRepository _repository;
    private readonly ILogger<ScriptService> _logger;
    private readonly ScriptExecutionOptions _options;
    private readonly TableCacheService _tableCacheService;

    public ScriptService(
        IScriptRepository repository,
        ILogger<ScriptService> logger,
        IOptions<ScriptExecutionOptions> options,
        TableCacheService tableCacheService)
    {
        _repository = repository;
        _logger = logger;
        _options = options.Value;
        _tableCacheService = tableCacheService;
    }

    public async Task<ScriptServiceResult> ScriptFlatten(string script, bool execute = true)
    {
        _logger.LogInformation("Starting script flattening process");
        
        // Generate flattened script with transaction logic
        var flattenScript = "BEGIN TRANSACTION;" + Environment.NewLine;
        
        var tablesCalled = await GetTablesCalledAsync(script);
        _logger.LogInformation("Found {TableCount} unique tables in script", tablesCalled.Count);
        
        flattenScript += CreateBeforeTables(tablesCalled);
        flattenScript += script + Environment.NewLine;
        flattenScript += CreateComparison(tablesCalled);
        flattenScript += "ROLLBACK TRANSACTION;";
        
        // Execute if requested and enabled
        var shouldExecute = execute && _options.EnableExecution;
        
        if (!shouldExecute)
        {
            _logger.LogInformation("Script execution skipped (execute={Execute}, enabled={Enabled})", 
                execute, _options.EnableExecution);
            
            return new ScriptServiceResult
            {
                Executed = false,
                ExecutionResult = new ScriptExecutionResult
                {
                    Success = true,
                    ExecutionTimeMs = 0
                }
            };
        }
        
        _logger.LogInformation("Executing flattened script");
        
        try
        {
            var result = await _repository.QueryDatabase(flattenScript, _options.TimeoutSeconds);
            var executionResult = ParseRepositoryResult(result);
            
            return new ScriptServiceResult
            {
                Executed = true,
                ExecutionResult = executionResult
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing script");
            
            return new ScriptServiceResult
            {
                Executed = true,
                ExecutionResult = new ScriptExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"Execution failed: {ex.Message}",
                    ExecutionTimeMs = 0
                }
            };
        }
    }

    private ScriptExecutionResult ParseRepositoryResult(object result)
    {
        try
        {
            var resultDict = result as IDictionary<string, object>;
            if (resultDict == null)
            {
                // Try to convert dynamic to dictionary
                var expando = result as ExpandoObject;
                if (expando != null)
                {
                    resultDict = expando;
                }
                else
                {
                    // Use reflection to get properties
                    var props = result.GetType().GetProperties();
                    resultDict = props.ToDictionary(p => p.Name, p => p.GetValue(result));
                }
            }
            
            var success = (bool)resultDict["Success"];
            var executionTimeMs = Convert.ToInt32(resultDict["ExecutionTimeMs"]);
            
            if (!success)
            {
                var executionResult = new ScriptExecutionResult
                {
                    Success = false,
                    ErrorMessage = resultDict["ErrorMessage"]?.ToString(),
                    ExecutionTimeMs = executionTimeMs
                };
                
                if (resultDict.ContainsKey("SqlError") && resultDict["SqlError"] != null)
                {
                    var sqlErrorDict = resultDict["SqlError"] as IDictionary<string, object>;
                    if (sqlErrorDict == null)
                    {
                        var sqlErrorProps = resultDict["SqlError"].GetType().GetProperties();
                        sqlErrorDict = sqlErrorProps.ToDictionary(p => p.Name, p => p.GetValue(resultDict["SqlError"]));
                    }
                    
                    executionResult.SqlError = new SqlErrorDetails
                    {
                        Number = Convert.ToInt32(sqlErrorDict["Number"]),
                        State = Convert.ToByte(sqlErrorDict["State"]),
                        LineNumber = Convert.ToInt32(sqlErrorDict["LineNumber"])
                    };
                }
                
                return executionResult;
            }
            
            // Success case - parse result sets and group into table comparisons
            var tableComparisons = new List<TableComparison>();
            
            if (resultDict.ContainsKey("ResultSets") && resultDict["ResultSets"] != null)
            {
                var resultSetsList = (resultDict["ResultSets"] as IEnumerable<object>)?.ToList();
                if (resultSetsList != null)
                {
                    // Result sets come in pairs: [before, after] for each table
                    // Each result set has __TableName__ column injected by CreateComparison
                    for (int i = 0; i < resultSetsList.Count; i += 2)
                    {
                        // Extract table name from first result set
                        string tableName = "Unknown";
                        
                        if (i < resultSetsList.Count)
                        {
                            tableName = ExtractTableName(resultSetsList[i]);
                        }
                        
                        // Parse "before" result set and remove __TableName__ column
                        ResultSet beforeResultSet = null;
                        if (i < resultSetsList.Count)
                        {
                            beforeResultSet = ParseResultSet(resultSetsList[i], "Before");
                            RemoveTableNameColumn(beforeResultSet);
                        }
                        
                        // Parse "after" result set and remove __TableName__ column
                        ResultSet afterResultSet = null;
                        if (i + 1 < resultSetsList.Count)
                        {
                            afterResultSet = ParseResultSet(resultSetsList[i + 1], "After");
                            RemoveTableNameColumn(afterResultSet);
                        }
                        
                        // Only add if we have at least one result set
                        if (beforeResultSet != null || afterResultSet != null)
                        {
                            tableComparisons.Add(new TableComparison
                            {
                                TableName = tableName,
                                Before = beforeResultSet,
                                After = afterResultSet
                            });
                        }
                    }
                }
            }
            
            return new ScriptExecutionResult
            {
                Success = true,
                ExecutionTimeMs = executionTimeMs,
                TableComparisons = tableComparisons
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing repository result");
            
            return new ScriptExecutionResult
            {
                Success = false,
                ErrorMessage = $"Error parsing results: {ex.Message}",
                ExecutionTimeMs = 0
            };
        }
    }
    
    private string ExtractTableName(object rs)
    {
        try
        {
            var rsDict = rs as IDictionary<string, object>;
            if (rsDict == null)
            {
                var rsProps = rs.GetType().GetProperties();
                rsDict = rsProps.ToDictionary(p => p.Name, p => p.GetValue(rs));
            }
            
            var rows = rsDict["Rows"] as IEnumerable<object>;
            if (rows != null)
            {
                var firstRow = rows.FirstOrDefault();
                if (firstRow != null)
                {
                    var rowDict = firstRow as IDictionary<string, object>;
                    if (rowDict == null)
                    {
                        var rowProps = firstRow.GetType().GetProperties();
                        rowDict = rowProps.ToDictionary(p => p.Name, p => p.GetValue(firstRow));
                    }
                    
                    if (rowDict.ContainsKey("__TableName__"))
                    {
                        return rowDict["__TableName__"]?.ToString() ?? "Unknown";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract table name from result set");
        }
        
        return "Unknown";
    }
    
    private void RemoveTableNameColumn(ResultSet resultSet)
    {
        if (resultSet == null) return;
        
        // Remove from columns list
        resultSet.Columns.Remove("__TableName__");
        
        // Remove from all rows
        foreach (var row in resultSet.Rows)
        {
            row.Remove("__TableName__");
        }
    }
    
    private ResultSet ParseResultSet(object rs, string namePrefix)
    {
        var rsDict = rs as IDictionary<string, object>;
        if (rsDict == null)
        {
            var rsProps = rs.GetType().GetProperties();
            rsDict = rsProps.ToDictionary(p => p.Name, p => p.GetValue(rs));
        }
        
        var resultSet = new ResultSet
        {
            Name = $"{namePrefix}_{rsDict["ResultSetIndex"]}",
            Columns = (rsDict["Columns"] as IEnumerable<object>)?.Select(c => c.ToString()).ToList() ?? new List<string>(),
            Rows = new List<Dictionary<string, object>>()
        };
        
        var rows = rsDict["Rows"] as IEnumerable<object>;
        if (rows != null)
        {
            foreach (var row in rows)
            {
                var rowDict = row as IDictionary<string, object>;
                if (rowDict == null)
                {
                    var rowProps = row.GetType().GetProperties();
                    rowDict = rowProps.ToDictionary(p => p.Name, p => p.GetValue(row));
                }
                resultSet.Rows.Add(new Dictionary<string, object>(rowDict));
            }
        }
        
        return resultSet;
    }

    private string CreateComparison(Dictionary<string, string> tables)
    {
        string comparison = "";
        foreach (var table in tables)
        {
            comparison += $"SELECT * INTO {table.Value}Dif FROM {table.Key}{Environment.NewLine} EXCEPT {Environment.NewLine} SELECT * FROM {table.Value} {Environment.NewLine}";
            comparison += $" IF(SELECT COUNT(*) FROM {table.Value}Dif) > 0\n BEGIN \n";
            // Inject table name as a column so we can identify which table this data belongs to
            comparison += $"SELECT '{table.Key}' AS __TableName__, * FROM {table.Value} WHERE [ID] IN (SELECT ID FROM {table.Value}Dif)" + Environment.NewLine;
            comparison += $"SELECT '{table.Key}' AS __TableName__, * FROM {table.Value}Dif END" + Environment.NewLine + Environment.NewLine;
        }
        return comparison;
    }
    
    private string CreateBeforeTables(Dictionary<string, string> tables)
    {
        string beforeTables = "";
        foreach (var table in tables)
        {
            beforeTables += $"SELECT * INTO {table.Value} FROM {table.Key};" + Environment.NewLine;
        }
        return beforeTables;
    }
    
    private async Task<Dictionary<string, string>> GetTablesCalledAsync(string script)
    {
        var tablesCalled = new Dictionary<string, string>();
        
        // Get all valid table names from cache
        var validTableNames = await _tableCacheService.GetTableNamesAsync();
        
        var pattern = @"\b(?:FROM|JOIN|INTO|UPDATE|DELETE\s+FROM)\s+((?:\[[^\]]+\]|\w+)(?:\.(?:\[[^\]]+\]|\w+))?)";
        
        var matches = Regex.Matches(script, pattern, RegexOptions.IgnoreCase);

        int i = 1;
        foreach (Match match in matches)
        {
            var tableCheckString = match.Groups[1].Value.Replace("[", "").Replace("]", "");
            
            // Check against dynamically fetched table names
            if (validTableNames.Contains(tableCheckString))
            {
                string tableName = tableCheckString.ToUpper();
                // Only add if not already in dictionary
                if (tablesCalled.TryAdd(tableName, "#temptable" + i))
                {
                    i++;
                }
            }
        }
        
        return tablesCalled;
    }
}
