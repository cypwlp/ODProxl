using Microsoft.Data.SqlClient;
using ODProxl.EntityModels;
using RemoteService;
using System.Data;
using System.Threading.Tasks;

namespace ODProxl.Services
{
    public interface IDataService
    {
        string Server { get; }
        string Database { get; }
        string UserName { get; }
        string Password { get; }
        bool IsAuthenticated { get; }
        string LastMessage { get; }
        bool LocalLogin { get; }
        bool Integrate { get; }
        string Language { get; set; }
        int TimeOut { get; set; }
        string UPS { get; }

        // 認證相關
        Task<bool> InitializeAsync(string username, string password, string database);
        bool Authenticate();
        Task<LoginInfo> GetLoginInfoAsync();

        // 核心資料操作 - 支援動態切換資料庫
        Task<string> ExecuteNonQueryAsync(string database, string sqlCommand);
        string ExecuteNonQuery(string database, string sqlCommand);

        Task<string> ExecuteScalarAsync(string database, string sqlCommand);
        string ExecuteScalar(string database, string sqlCommand);

        Task<DataSet> GetSelectResultAsync(string database, string selectCommand, string message = "", int runType = 0);
        DataSet GetSelectResult(string database, string selectCommand, string message = "", int runType = 0);
        DataSet GetSelectResult(string database, string selectCommand);

        Task<string> UpdateDataTableAsync(string database, DataSet dsChangeDataSet, string tableName = "");
        string UpdateDataTable(string database, DataSet dsChangeDataSet, string tableName = "");

        string CheckGrammar(string expression);

        // ==================== 參數化查詢完整支援 ====================
        Task<string> ExecuteParameterizedQueryAsync(string database, ParameterizedQuery query);
        string ExecuteParameterizedQuery(string database, ParameterizedQuery query);

        Task<string> ExecuteScalarParameterizedQueryAsync(string database, ParameterizedQuery query);
        string ExecuteScalarParameterizedQuery(string database, ParameterizedQuery query);

        Task<DataSet> GetSelectResultParameterizedQueryAsync(string database, ParameterizedQuery query, string message = "", int runType = 0);
        DataSet GetSelectResultParameterizedQuery(string database, ParameterizedQuery query, string message = "", int runType = 0);

        // 簡便重載
        Task<string> ExecuteParameterizedQueryAsync(string database, string sql, params SqlParameter[] parameters);
        Task<string> ExecuteScalarParameterizedQueryAsync(string database, string sql, params SqlParameter[] parameters);
        Task<DataSet> GetSelectResultParameterizedQueryAsync(string database, string sql, string message, int runType, params SqlParameter[] parameters);
    }
}