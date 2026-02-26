using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using Data.Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Data.Repositories;

public class ScriptRepository : IScriptRepository
{
    private readonly IQuery _db;
    private readonly ILogger<ScriptRepository> _logger;

    public ScriptRepository(IQuery db, ILogger<ScriptRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<object> QueryDatabase(string script, int timeoutSeconds)
    {
        var stopwatch = Stopwatch.StartNew();
        var resultSets = new List<Dictionary<string, object>>();
        
        try
        {
            _logger.LogInformation("Executing SQL script with timeout of {TimeoutSeconds} seconds", timeoutSeconds);
            
            using var connection = _db.GetConnection() as SqlConnection;
            if (connection == null)
            {
                throw new InvalidOperationException("Connection is not a SqlConnection");
            }
            
            // Connection is already opened by DbConnectionFactory
            
            using var command = connection.CreateCommand();
            command.CommandText = script;
            command.CommandTimeout = timeoutSeconds;
            command.CommandType = CommandType.Text;
            
            using var reader = await command.ExecuteReaderAsync();
            
            int resultSetIndex = 0;
            do
            {
                var resultSet = new Dictionary<string, object>
                {
                    ["ResultSetIndex"] = resultSetIndex,
                    ["Columns"] = new List<string>(),
                    ["Rows"] = new List<Dictionary<string, object>>()
                };
                
                // Get column names
                var columns = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns.Add(reader.GetName(i));
                }
                resultSet["Columns"] = columns;
                
                // Read rows
                var rows = new List<Dictionary<string, object>>();
                while (await reader.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var value = reader.GetValue(i);
                        row[reader.GetName(i)] = value == DBNull.Value ? null : value;
                    }
                    rows.Add(row);
                }
                resultSet["Rows"] = rows;
                
                resultSets.Add(resultSet);
                resultSetIndex++;
                
            } while (await reader.NextResultAsync());
            
            stopwatch.Stop();
            _logger.LogInformation(
                "Script execution completed successfully in {ElapsedMs}ms with {ResultSetCount} result sets", 
                stopwatch.ElapsedMilliseconds, 
                resultSets.Count
            );
            
            return new
            {
                Success = true,
                ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds,
                ResultSets = resultSets
            };
        }
        catch (SqlException sqlEx)
        {
            stopwatch.Stop();
            _logger.LogError(
                sqlEx,
                "SQL error during script execution: Error {ErrorNumber}, State {State}, Line {LineNumber}",
                sqlEx.Number,
                sqlEx.State,
                sqlEx.LineNumber
            );
            
            return new
            {
                Success = false,
                ErrorMessage = sqlEx.Message,
                SqlError = new
                {
                    Number = sqlEx.Number,
                    State = sqlEx.State,
                    LineNumber = sqlEx.LineNumber
                },
                ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error during script execution");
            
            return new
            {
                Success = false,
                ErrorMessage = $"Unexpected error: {ex.Message}",
                ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }
}
