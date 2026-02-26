using System.Collections.Generic;

namespace API.Models;

public class TableComparison
{
    public string TableName { get; set; }
    public ResultSet Before { get; set; }
    public ResultSet After { get; set; }
}
