using System.Data;
using System.Threading;
using System.Threading.Tasks;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}