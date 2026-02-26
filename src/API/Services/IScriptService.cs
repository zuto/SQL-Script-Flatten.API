using System.Threading.Tasks;
using API.Models;

namespace API.Services;

public interface IScriptService
{
    Task<ScriptServiceResult> ScriptFlatten(string script, bool execute = true);
}
