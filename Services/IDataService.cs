using Microsoft.Data.SqlClient;
using ODProxl.EntityModels;
using RemoteService;
using System.Data;
using System.Threading.Tasks;
using static ODProxl.Services.impls.DataService;

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

        // 核心資料操作
        Task<string> ExecuteNonQueryAsync(string sqlCommand);
        string ExecuteNonQuery(string sqlCommand);

        Task<string> ExecuteScalarAsync(string sqlCommand);
        string ExecuteScalar(string sqlCommand);

        Task<DataSet> GetSelectResultAsync(string selectCommand, string message = "", int runType = 0);
        DataSet GetSelectResult(string selectCommand, string message = "", int runType = 0);
        DataSet GetSelectResult(string selectCommand);

        Task<string> UpdateDataTableAsync(DataSet dsChangeDataSet, string tableName = "");
        string UpdateDataTable(DataSet dsChangeDataSet, string tableName = "");

        string CheckGrammar(string expression);

        // ==================== 參數化查詢完整支援 ====================
        Task<string> ExecuteParameterizedQueryAsync(ParameterizedQuery query);
        string ExecuteParameterizedQuery(ParameterizedQuery query);

        Task<string> ExecuteScalarParameterizedQueryAsync(ParameterizedQuery query);
        string ExecuteScalarParameterizedQuery(ParameterizedQuery query);

        Task<DataSet> GetSelectResultParameterizedQueryAsync(ParameterizedQuery query, string message = "", int runType = 0);
        DataSet GetSelectResultParameterizedQuery(ParameterizedQuery query, string message = "", int runType = 0);

        // 簡便重載
        Task<string> ExecuteParameterizedQueryAsync(string sql, params SqlParameter[] parameters);
        Task<string> ExecuteScalarParameterizedQueryAsync(string sql, params SqlParameter[] parameters);
        Task<DataSet> GetSelectResultParameterizedQueryAsync(string sql, string message, int runType, params SqlParameter[] parameters);
    }
}