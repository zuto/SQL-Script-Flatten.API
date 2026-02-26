using System.Collections.Generic;
using System.Threading.Tasks;

namespace Data.Repositories;

public interface IScriptRepository
{
    Task<object> QueryDatabase(string script, int timeoutSeconds);
    Task<HashSet<string>> GetAllTableNames();
}
