using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Polly.Retry;

namespace Data.Dapper
{
    public class DapperQuery : IQuery
    {
        private readonly IDbConnectionFactory _factory;
        private readonly AsyncRetryPolicy _retryPolicy;
        public DapperQuery(IDbConnectionFactory factory, AsyncRetryPolicy retryPolicy)
        {
            _factory = factory;
            _retryPolicy = retryPolicy;
        }

        public async Task<IEnumerable<T>> QueryAsync<T>(string sql, IEnumerable<T>? parameters = null,
            int? commandTimeout = null, CommandType? commandType = null)
            => await _retryPolicy.ExecuteAsync(async () =>
            {
                using var conn = _factory.CreateConnection();
                return await conn.QueryAsync<T>(sql);
            });

        public IDbConnection GetConnection()
        {
            return _factory.CreateConnection();
        }

        /*public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object parameters)
        {
            return await _connection.QueryAsync<T>(sql, parameters);
        }

        public async Task<T> QueryFirstAsync<T>(string sql)
        {
            return await _connection.QueryFirstAsync<T>(sql);
        }

        public async Task<T> QueryFirstAsync<T>(string sql, object parameters)
        {
            return await _connection.QueryFirstAsync<T>(sql, parameters);
        }
        public async Task<T> QueryFirstOrDefaultAsync<T>(string sql, object parameters)
        {
            return await _connection.QueryFirstOrDefaultAsync<T>(sql, parameters);
        }

        public async Task<int> ExecuteAsync(string sql)
        {
            return await _connection.ExecuteAsync(sql);
        }

        public async Task<int> ExecuteAsync(string sql, object parameters)
        {
            return await _connection.ExecuteAsync(sql, parameters);
        }*/
    }
}
