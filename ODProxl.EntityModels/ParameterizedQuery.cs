using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ODProxl.EntityModels
{
    /// <summary>
    /// 參數化查詢包裝類
    /// 已修正 nullable 參考型別所有警告
    /// </summary>
    public class ParameterizedQuery
    {
        public string SQL { get; set; } = string.Empty;

        public List<SqlParameter> Parameters { get; private set; }

        public ParameterizedQuery()
        {
            Parameters = new List<SqlParameter>();
        }

        public ParameterizedQuery(string sql) : this()
        {
            SQL = sql ?? string.Empty;
        }

        public void AddParameter(string name, object value)
        {
            Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
        }

        public void AddParameter(string name, SqlDbType dbType, object value)
        {
            var param = new SqlParameter(name, dbType)
            {
                Value = value ?? DBNull.Value
            };
            Parameters.Add(param);
        }

        public string ToParameterlessSQL()
        {
            if (Parameters.Count == 0)
                return SQL;

            string result = SQL;

            // 按參數名長度降序排序，避免 @ID 被 @ID2 部分替換
            var sortedParams = Parameters
                .Where(p => p.ParameterName != null)
                .OrderByDescending(p => p.ParameterName!.Length)
                .ToList();

            foreach (var param in sortedParams)
            {
                string paramValue = GetParameterValueAsSQL(param);
                result = result.Replace(param.ParameterName!, paramValue);
            }

            return result;
        }

        private string GetParameterValueAsSQL(SqlParameter param)
        {
            if (param.Value == null || param.Value == DBNull.Value)
                return "NULL";

            // 使用 ! 寬恕運算子，因為前面已明確排除 null
            string valueStr = param.Value!.ToString()!;

            switch (param.SqlDbType)
            {
                case SqlDbType.NVarChar:
                case SqlDbType.NChar:
                case SqlDbType.NText:
                    return $"N'{valueStr.Replace("'", "''")}'";

                case SqlDbType.VarChar:
                case SqlDbType.Char:
                case SqlDbType.Text:
                    return $"'{valueStr.Replace("'", "''")}'";

                case SqlDbType.DateTime:
                case SqlDbType.SmallDateTime:
                case SqlDbType.Date:
                    if (param.Value is DateTime dt)
                        return $"'{dt:yyyy-MM-dd HH:mm:ss}'";
                    return $"'{valueStr}'";

                case SqlDbType.Bit:
                    return (bool)param.Value ? "1" : "0";

                default:
                    return valueStr;
            }
        }
    }
}