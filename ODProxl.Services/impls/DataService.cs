using Microsoft.Data.SqlClient;
using ODProxl.EntityModels;
using ODProxl.Services;
using RemoteService;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ODProxl.Services.impls
{
    public class DataService : IDataService
    {
        #region 字段
        private Service1SoapClient? _soapClient;
        private bool _isAuthenticated;
        private readonly string _serviceUrl;
        private string _userName = string.Empty;
        private string _password = string.Empty;
        private string _database = string.Empty;
        private string _lastMessage = "";
        private string _language = System.Globalization.CultureInfo.InstalledUICulture.Name;
        private int _timeOut = 30;
        #endregion

        #region 屬性
        public string Server { get; private set; } = string.Empty;
        public string Database => _database;
        public string UserName => _userName;
        public string Password => _password;
        public bool IsAuthenticated => _isAuthenticated;
        public string LastMessage => _lastMessage;
        public bool LocalLogin => false;
        public bool Integrate => false;
        public string Language
        {
            get => _language;
            set => _language = value ?? string.Empty;
        }
        public int TimeOut
        {
            get => _timeOut;
            set => _timeOut = value;
        }
        public string UPS => Encrypt($"{UserName}@{Password}", DateTime.Now.ToString("yyyyMMdd"));
        #endregion

        #region 建構函式
        public DataService(string serviceUrl, string server, string database, string userName, string password)
        {
            _serviceUrl = serviceUrl ?? string.Empty;
            Server = server ?? string.Empty;
            _database = database ?? string.Empty;
            _userName = userName ?? string.Empty;
            _password = password ?? string.Empty;
        }

        public DataService(string serviceUrl)
        {
            _serviceUrl = serviceUrl ?? string.Empty;
        }
        #endregion

        #region 認證
        public async Task<bool> InitializeAsync(string username, string password, string database)
        {
            _userName = username ?? string.Empty;
            _password = password ?? string.Empty;
            _database = database ?? string.Empty;

            try
            {
                var uri = new Uri(_serviceUrl);
                Server = uri.Host;
            }
            catch
            {
                Server = string.Empty;
            }

            return await WebServiceAuthenticateAsync();
        }

        public bool Authenticate()
        {
            return Task.Run(() => WebServiceAuthenticateAsync()).Result;
        }

        private async Task<bool> WebServiceAuthenticateAsync()
        {
            var binding = new BasicHttpBinding
            {
                Security = {
                    Mode = BasicHttpSecurityMode.TransportCredentialOnly,
                    Transport = { ClientCredentialType = HttpClientCredentialType.Basic }
                },
                MaxReceivedMessageSize = int.MaxValue,
                MaxBufferSize = int.MaxValue,
                ReaderQuotas = System.Xml.XmlDictionaryReaderQuotas.Max,
                AllowCookies = true,
                SendTimeout = TimeSpan.FromSeconds(_timeOut),
                ReceiveTimeout = TimeSpan.FromSeconds(_timeOut)
            };

            var endpoint = new EndpointAddress(_serviceUrl);
            _soapClient = new Service1SoapClient(binding, endpoint);
            _soapClient.ClientCredentials.UserName.UserName = _userName;
            _soapClient.ClientCredentials.UserName.Password = _password;

            try
            {
                await _soapClient.SetDataBaseAsync(_database);
                var comstr = await _soapClient.CommSecurityStringAsync();
                var decryptedKey = Decrypt(comstr, "19283746");
                var encryptedCredentials = Encrypt($"{_userName}@{_password}", decryptedKey);
                _isAuthenticated = await _soapClient.SetCommStringAsync(encryptedCredentials);
                return _isAuthenticated;
            }
            catch (Exception ex)
            {
                _lastMessage = ex.Message;
                _isAuthenticated = false;
                return false;
            }
        }

        public async Task<LoginInfo> GetLoginInfoAsync()
        {
            await EnsureAuthenticatedAsync();
            return await _soapClient!.GetLoginInfoAsync();
        }
        #endregion

        #region 確保連線與資料庫切換
        private async Task EnsureClientAndDatabaseAsync(string database)
        {
            if (_soapClient == null)
            {
                var binding = new BasicHttpBinding
                {
                    Security = {
                        Mode = BasicHttpSecurityMode.TransportCredentialOnly,
                        Transport = { ClientCredentialType = HttpClientCredentialType.Basic }
                    },
                    MaxReceivedMessageSize = int.MaxValue,
                    MaxBufferSize = int.MaxValue,
                    ReaderQuotas = System.Xml.XmlDictionaryReaderQuotas.Max,
                    AllowCookies = true,
                    SendTimeout = TimeSpan.FromSeconds(_timeOut),
                    ReceiveTimeout = TimeSpan.FromSeconds(_timeOut)
                };

                var endpoint = new EndpointAddress(_serviceUrl);
                _soapClient = new Service1SoapClient(binding, endpoint);
                _soapClient.ClientCredentials.UserName.UserName = _userName;
                _soapClient.ClientCredentials.UserName.Password = _password;
            }

            if (!string.IsNullOrEmpty(database))
            {
                await _soapClient.SetDataBaseAsync(database);
                _database = database;   // 更新目前使用的資料庫
            }
        }

        private async Task EnsureAuthenticatedAsync(string database = null)
        {
            if (!_isAuthenticated || _soapClient == null)
            {
                await WebServiceAuthenticateAsync();
            }

            await EnsureClientAndDatabaseAsync(database ?? _database);
        }
        #endregion

        #region 核心資料操作（支援動態切換資料庫）
        public async Task<string> ExecuteNonQueryAsync(string database, string sqlCommand)
        {
            await EnsureAuthenticatedAsync(database);
            try
            {
                return await _soapClient!.ExecuteNonQueryAsync(sqlCommand);
            }
            catch (Exception ex)
            {
                _lastMessage = ex.Message;
                return "0 " + ex.Message;
            }
        }

        public string ExecuteNonQuery(string database, string sqlCommand)
            => Task.Run(() => ExecuteNonQueryAsync(database, sqlCommand)).Result;

        public async Task<string> ExecuteScalarAsync(string database, string sqlCommand)
        {
            await EnsureAuthenticatedAsync(database);
            try
            {
                return await _soapClient!.ExecuteScalarAsync(sqlCommand);
            }
            catch (Exception ex)
            {
                _lastMessage = ex.Message;
                return "";
            }
        }

        public string ExecuteScalar(string database, string sqlCommand)
            => Task.Run(() => ExecuteScalarAsync(database, sqlCommand)).Result;

        public async Task<DataSet> GetSelectResultAsync(string database, string selectCommand, string message = "", int runType = 0)
        {
            await EnsureAuthenticatedAsync(database);
            try
            {
                var request = new GetSelectResultRequest
                {
                    strSelectCommand = selectCommand,
                    Message = message ?? "",
                    RunType = runType
                };
                var response = await _soapClient!.GetSelectResultAsync(request);
                return ConvertXmlToDataSet(response.GetSelectResultResult);
            }
            catch (Exception ex)
            {
                _lastMessage = ex.Message;
                return new DataSet();
            }
        }

        public DataSet GetSelectResult(string database, string selectCommand, string message = "", int runType = 0)
            => Task.Run(() => GetSelectResultAsync(database, selectCommand, message, runType)).Result;

        public DataSet GetSelectResult(string database, string selectCommand)
            => GetSelectResult(database, selectCommand, "", 0);

        public async Task<string> UpdateDataTableAsync(string database, DataSet dsChangeDataSet, string tableName = "")
        {
            await EnsureAuthenticatedAsync(database);

            if (dsChangeDataSet == null || dsChangeDataSet.Tables.Count == 0)
                return "0 資料集為空";

            if (string.IsNullOrEmpty(tableName))
            {
                var firstTable = dsChangeDataSet.Tables.Cast<DataTable>().FirstOrDefault();
                tableName = firstTable?.TableName ?? "";
            }

            if (string.IsNullOrEmpty(tableName))
                return "0 不能修改沒有名稱的表";

            try
            {
                var array = ConvertDataSetToArrayOfXElement(dsChangeDataSet, tableName);
                return await _soapClient!.UpdateDataTableAsync(array, tableName);
            }
            catch (Exception ex)
            {
                _lastMessage = ex.Message;
                return "0 " + ex.Message;
            }
        }

        public string UpdateDataTable(string database, DataSet dsChangeDataSet, string tableName = "")
            => Task.Run(() => UpdateDataTableAsync(database, dsChangeDataSet, tableName)).Result;

        public string CheckGrammar(string expression)
            => "1 遠端連接暫時不能執行資料語法檢查!";
        #endregion

        #region 參數化查詢完整實作
        public async Task<string> ExecuteParameterizedQueryAsync(string database, ParameterizedQuery query)
        {
            string sql = query.ToParameterlessSQL();
            return await ExecuteNonQueryAsync(database, sql);
        }

        public string ExecuteParameterizedQuery(string database, ParameterizedQuery query)
            => Task.Run(() => ExecuteParameterizedQueryAsync(database, query)).Result;

        public async Task<string> ExecuteScalarParameterizedQueryAsync(string database, ParameterizedQuery query)
        {
            string sql = query.ToParameterlessSQL();
            return await ExecuteScalarAsync(database, sql);
        }

        public string ExecuteScalarParameterizedQuery(string database, ParameterizedQuery query)
            => Task.Run(() => ExecuteScalarParameterizedQueryAsync(database, query)).Result;

        public async Task<DataSet> GetSelectResultParameterizedQueryAsync(string database, ParameterizedQuery query, string message = "", int runType = 0)
        {
            string sql = query.ToParameterlessSQL();
            return await GetSelectResultAsync(database, sql, message, runType);
        }

        public DataSet GetSelectResultParameterizedQuery(string database, ParameterizedQuery query, string message = "", int runType = 0)
            => Task.Run(() => GetSelectResultParameterizedQueryAsync(database, query, message, runType)).Result;

        // 簡便重載
        public async Task<string> ExecuteParameterizedQueryAsync(string database, string sql, params SqlParameter[] parameters)
        {
            var query = new ParameterizedQuery(sql);
            foreach (var p in parameters)
                query.Parameters.Add(p);
            return await ExecuteParameterizedQueryAsync(database, query);
        }

        public async Task<string> ExecuteScalarParameterizedQueryAsync(string database, string sql, params SqlParameter[] parameters)
        {
            var query = new ParameterizedQuery(sql);
            foreach (var p in parameters)
                query.Parameters.Add(p);
            return await ExecuteScalarParameterizedQueryAsync(database, query);
        }

        public async Task<DataSet> GetSelectResultParameterizedQueryAsync(string database, string sql, string message, int runType, params SqlParameter[] parameters)
        {
            var query = new ParameterizedQuery(sql);
            foreach (var p in parameters)
                query.Parameters.Add(p);
            return await GetSelectResultParameterizedQueryAsync(database, query, message, runType);
        }
        #endregion

        #region 輔助方法
        private void EnsureAuthenticated() // 保留舊版供內部使用（非同步版本優先）
        {
            if (!_isAuthenticated || _soapClient == null)
                throw new InvalidOperationException("請先呼叫 InitializeAsync 或 Authenticate 完成登入。");
        }

        private DataSet ConvertXmlToDataSet(ArrayOfXElement? arrayOfXElement)
        {
            if (arrayOfXElement?.Nodes == null || arrayOfXElement.Nodes.Count == 0)
                return new DataSet();

            var doc = new XDocument(new XElement("Root", arrayOfXElement.Nodes));
            using var reader = doc.CreateReader();
            var ds = new DataSet();
            ds.ReadXml(reader);
            return ds;
        }

        private ArrayOfXElement ConvertDataSetToArrayOfXElement(DataSet ds, string tableName)
        {
            var array = new ArrayOfXElement();
            if (!ds.Tables.Contains(tableName))
                return array;

            var dt = ds.Tables[tableName];
            foreach (DataRow row in dt.Rows)
            {
                var elem = new XElement("row");
                foreach (DataColumn col in dt.Columns)
                {
                    elem.Add(new XElement(col.ColumnName, row[col] ?? DBNull.Value));
                }
                array.Nodes.Add(elem);
            }
            return array;
        }
        #endregion

        #region 加密解密
        public static string Decrypt(string pToDecrypt, string sKey)
            => Decrypt(pToDecrypt, sKey, Encoding.UTF8);

        public static string Decrypt(string pToDecrypt, string sKey, Encoding coder)
        {
            try
            {
                using var des = DES.Create();
                int len = pToDecrypt.Length / 2;
                byte[] inputByteArray = new byte[len];
                for (int x = 0; x < len; x++)
                    inputByteArray[x] = Convert.ToByte(pToDecrypt.Substring(x * 2, 2), 16);

                des.Key = Encoding.ASCII.GetBytes(sKey);
                des.IV = Encoding.ASCII.GetBytes(sKey);

                using var ms = new MemoryStream();
                using (var cs = new CryptoStream(ms, des.CreateDecryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(inputByteArray, 0, inputByteArray.Length);
                    cs.FlushFinalBlock();
                }
                return coder.GetString(ms.ToArray());
            }
            catch
            {
                return "";
            }
        }

        public static string Encrypt(string pToEncrypt, string sKey)
            => Encrypt(pToEncrypt, sKey, Encoding.UTF8);

        public static string Encrypt(string pToEncrypt, string sKey, Encoding coder)
        {
            try
            {
                using var des = DES.Create();
                byte[] inputByteArray = coder.GetBytes(pToEncrypt);
                des.Key = Encoding.ASCII.GetBytes(sKey);
                des.IV = Encoding.ASCII.GetBytes(sKey);

                using var ms = new MemoryStream();
                using (var cs = new CryptoStream(ms, des.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(inputByteArray, 0, inputByteArray.Length);
                    cs.FlushFinalBlock();
                }

                var ret = new StringBuilder();
                foreach (byte b in ms.ToArray())
                    ret.AppendFormat("{0:X2}", b);
                return ret.ToString();
            }
            catch
            {
                return "";
            }
        }
        #endregion
    }
}