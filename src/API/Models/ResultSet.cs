using System.Collections.Generic;

namespace API.Models;

public class ResultSet
{
    public string Name { get; set; }
    public List<string> Columns { get; set; } = new();
    public List<Dictionary<string, object>> Rows { get; set; } = new();
}
