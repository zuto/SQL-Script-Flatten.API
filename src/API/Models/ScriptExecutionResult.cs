using System.Collections.Generic;

namespace API.Models;

public class ScriptExecutionResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; }
    public SqlErrorDetails SqlError { get; set; }
    public List<TableComparison> TableComparisons { get; set; } = new();
    public int ExecutionTimeMs { get; set; }
}

public class SqlErrorDetails
{
    public int Number { get; set; }
    public byte State { get; set; }
    public int LineNumber { get; set; }
}
