using System;

namespace API.Exceptions;

public class ScriptExecutionException : Exception
{
    public int? SqlErrorNumber { get; set; }
    public byte? SqlErrorState { get; set; }
    public int? SqlErrorLine { get; set; }
    
    public ScriptExecutionException(string message) : base(message)
    {
    }
    
    public ScriptExecutionException(string message, Exception innerException) : base(message, innerException)
    {
    }
    
    public ScriptExecutionException(string message, int sqlErrorNumber, byte sqlErrorState, int sqlErrorLine) 
        : base(message)
    {
        SqlErrorNumber = sqlErrorNumber;
        SqlErrorState = sqlErrorState;
        SqlErrorLine = sqlErrorLine;
    }
}
