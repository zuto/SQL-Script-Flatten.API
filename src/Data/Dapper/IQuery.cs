using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Data.Dapper
{
    public interface IQuery
    {
        //Task<IEnumerable<T>> QueryAsync<T>(string sql);

        Task<IEnumerable<T>> QueryAsync<T>(string sql, IEnumerable<T>? parameters = null,
            int? commandTimeout = null, CommandType? commandType = null);
        
        IDbConnection GetConnection();
        
        /*
        Task<IEnumerable<T>> QueryAsync<T>(string sql, object parameters);

        Task<T> QueryFirstAsync<T>(string sql);

        Task<T> QueryFirstAsync<T>(string sql, object parameters);

        Task<T> QueryFirstOrDefaultAsync<T>(string sql, object parameters);

        Task<int> ExecuteAsync(string sql);

        Task<int> ExecuteAsync(string sql, object parameters);*/

    }
}
