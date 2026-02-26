using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;
using API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Api.Controllers;

[ApiController]
public class ScriptController : ControllerBase
{
    private readonly IScriptService _service;
    private readonly ILogger<ScriptController> _logger;

    public ScriptController(IScriptService service, ILogger<ScriptController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Execute a SQL script within a transaction and return table comparison results (transaction is rolled back for safe QA testing)
    /// </summary>
    /// <returns>JSON response with before/after table comparisons</returns>
    [Route("script")]
    [Consumes(MediaTypeNames.Text.Plain)]
    [Produces(MediaTypeNames.Application.Json)]
    [HttpPost]
    public async Task<ActionResult> PostScript()
    {
        _logger.LogInformation("Received script execution request (with transaction rollback for safe QA testing)");
        
        using var reader = new StreamReader(Request.Body);
        var script = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(script))
        {
            _logger.LogWarning("Empty script received");
            return BadRequest(new { error = "Script cannot be empty" });
        }

        try
        {
            // Always execute with transaction wrapper for safe QA testing
            var result = await _service.ScriptFlatten(script, execute: true);
            
            if (!result.ExecutionResult.Success)
            {
                _logger.LogWarning("Script execution failed: {ErrorMessage}", result.ExecutionResult.ErrorMessage);
                
                // Return 200 for SQL errors (user errors), not server errors
                return Ok(new
                {
                    success = false,
                    executed = result.Executed,
                    errorMessage = result.ExecutionResult.ErrorMessage,
                    sqlError = result.ExecutionResult.SqlError,
                    executionTimeMs = result.ExecutionResult.ExecutionTimeMs
                });
            }
            
            _logger.LogInformation("Script processed successfully (executed={Executed})", result.Executed);
            
            return Ok(new
            {
                success = true,
                executed = result.Executed,
                executionTimeMs = result.ExecutionResult.ExecutionTimeMs,
                tableComparisons = result.ExecutionResult.TableComparisons
            });
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing script");
            
            // Return 500 for infrastructure/system errors
            return StatusCode(500, new
            {
                success = false,
                executed = false,
                errorMessage = $"System error: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Generate flattened SQL script text without execution (backward compatible)
    /// </summary>
    /// <returns>Plain text flattened script</returns>
    [Route("script/text")]
    [Consumes(MediaTypeNames.Text.Plain)]
    [Produces(MediaTypeNames.Text.Plain)]
    [HttpPost]
    public async Task<ActionResult> PostScriptText()
    {
        _logger.LogInformation("Received script text-only request");
        
        using var reader = new StreamReader(Request.Body);
        var script = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(script))
        {
            _logger.LogWarning("Empty script received");
            return BadRequest("Script cannot be empty");
        }

        try
        {
            // Don't execute, just return the flattened script text
            var result = await _service.ScriptFlatten(script, execute: false);
            
            // For text endpoint, we need to generate and return the actual script text
            // Since the service doesn't return the script anymore, we need to regenerate it
            // This is less efficient but maintains backward compatibility
            var flattenScript = GenerateFlattenedScript(script);
            
            return Content(flattenScript, "text/plain");
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error generating flattened script");
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    private string GenerateFlattenedScript(string script)
    {
        // This duplicates some logic from ScriptService for backward compatibility
        // Ideally we'd refactor ScriptService to optionally return the script text
        var flattenScript = "BEGIN TRANSACTION;" + System.Environment.NewLine;
        
        var tablesCalled = GetTablesCalled(script);
        flattenScript += CreateBeforeTables(tablesCalled);
        flattenScript += script + System.Environment.NewLine;
        flattenScript += CreateComparison(tablesCalled);
        flattenScript += "ROLLBACK TRANSACTION;";
        
        return flattenScript;
    }

    private string CreateComparison(System.Collections.Generic.Dictionary<string, string> tables)
    {
        string comparison = "";
        foreach (var table in tables)
        {
            comparison += $"SELECT * INTO {table.Value}Dif FROM {table.Key}{System.Environment.NewLine} EXCEPT {System.Environment.NewLine} SELECT * FROM {table.Value} {System.Environment.NewLine}";
            comparison += $" IF(SELECT COUNT(*) FROM {table.Value}Dif) > 0\n BEGIN \n";
            comparison += $"SELECT * FROM {table.Value} WHERE [ID] IN (SELECT ID FROM {table.Value}Dif)" + System.Environment.NewLine;
            comparison += $"SELECT * FROM {table.Value}Dif END" + System.Environment.NewLine + System.Environment.NewLine;
        }
        return comparison;
    }
    
    private string CreateBeforeTables(System.Collections.Generic.Dictionary<string, string> tables)
    {
        string beforeTables = "";
        foreach (var table in tables)
        {
            beforeTables += $"SELECT * INTO {table.Value} FROM {table.Key};" + System.Environment.NewLine;
        }
        return beforeTables;
    }
    
    private System.Collections.Generic.Dictionary<string, string> GetTablesCalled(string script)
    {
        var tablesCalled = new System.Collections.Generic.Dictionary<string, string>();
        
        var pattern = @"\b(?:FROM|JOIN|INTO|UPDATE|DELETE\s+FROM)\s+((?:\[[^\]]+\]|\w+)(?:\.(?:\[[^\]]+\]|\w+))?)";
        
        var matches = System.Text.RegularExpressions.Regex.Matches(script, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        int i = 1;
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var tableCheckString = match.Groups[1].Value.Replace("[", "").Replace("]", "");
            if (SQLScriptFlatten.API.DatabaseTables.Tables.Contains(tableCheckString))
            {
                string tableName = tableCheckString.ToUpper();
                tablesCalled.TryAdd(tableName, "#temptable" + i);
                i++;
            }
        }
        
        return tablesCalled;
    }
}
