using Microsoft.Data.SqlClient;
using ODProxl.EntityModels;
using RemoteService;
using System.Data;
using System.Threading.Tasks;

namespace ODProxl.Services
{
    public interface IDataService
    {
        // ==================== 基礎屬性 ====================
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

        // ==================== 認證相關 ====================
        Task<bool> InitializeAsync(string username, string password, string database);
        bool Authenticate();
        Task<LoginInfo> GetLoginInfoAsync();

        // ==================== 1. 執行命令（INSERT / UPDATE / DELETE） ====================
        Task<string> ExecAsync(string database, string sql);
        string Exec(string database, string sql);

        // ==================== 2. 執行標量查詢（返回單一值） ====================
        Task<string> ScalarAsync(string database, string sql);
        string Scalar(string database, string sql);

        // ==================== 3. 執行查詢，返回 DataSet ====================
        Task<DataSet> QueryAsync(string database, string sql, string msg = "", int runType = 0);
        DataSet Query(string database, string sql, string msg = "", int runType = 0);
        DataSet Query(string database, string sql);   // 簡便重載

        // ==================== 4. 儲存 DataTable 變更 ====================
        Task<string> SaveAsync(string database, DataSet ds, string tableName = "");
        string Save(string database, DataSet ds, string tableName = "");

        // ==================== 5. SQL 語法檢查（遠端暫不支援） ====================
        string CheckSyntax(string expression);

        // ==================== 6. 參數化版本（安全防注入） ====================
        Task<string> ExecParamAsync(string database, ParameterizedQuery query);
        string ExecParam(string database, ParameterizedQuery query);

        Task<string> ScalarParamAsync(string database, ParameterizedQuery query);
        string ScalarParam(string database, ParameterizedQuery query);

        Task<DataSet> QueryParamAsync(string database, ParameterizedQuery query, string msg = "", int runType = 0);
        DataSet QueryParam(string database, ParameterizedQuery query, string msg = "", int runType = 0);

        // 簡便重載：直接傳入 SQL 與參數陣列
        Task<string> ExecParamAsync(string database, string sql, params SqlParameter[] parameters);
        Task<string> ScalarParamAsync(string database, string sql, params SqlParameter[] parameters);
        Task<DataSet> QueryParamAsync(string database, string sql, string msg, int runType, params SqlParameter[] parameters);
    }
}