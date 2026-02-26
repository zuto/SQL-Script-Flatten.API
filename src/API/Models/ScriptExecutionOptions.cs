namespace API.Models;

public class ScriptExecutionOptions
{
    public bool EnableExecution { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 300;
    public int MaxRetries { get; set; } = 3;
}
