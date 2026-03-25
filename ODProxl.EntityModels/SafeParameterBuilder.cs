using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.EntityModels
{
    /// <summary>
    /// 安全參數建構器（與原始 RemoteDBTools 完全一致）
    /// </summary>
    public static class SafeParameterBuilder
    {
        public static SqlParameter CreateStringParameter(string name, string value, int size = -1)
        {
            var param = new SqlParameter(name, SqlDbType.NVarChar);
            if (size > 0) param.Size = size;
            param.Value = string.IsNullOrEmpty(value) ? DBNull.Value : value;
            return param;
        }

        public static SqlParameter CreateIntParameter(string name, int? value)
        {
            var param = new SqlParameter(name, SqlDbType.Int);
            param.Value = value.HasValue ? (object)value.Value : DBNull.Value;
            return param;
        }

        public static SqlParameter CreateDateTimeParameter(string name, DateTime? value)
        {
            var param = new SqlParameter(name, SqlDbType.DateTime);
            param.Value = value.HasValue ? (object)value.Value : DBNull.Value;
            return param;
        }

        public static SqlParameter CreateDecimalParameter(string name, decimal? value)
        {
            var param = new SqlParameter(name, SqlDbType.Decimal);
            param.Value = value.HasValue ? (object)value.Value : DBNull.Value;
            return param;
        }

        public static SqlParameter CreateBooleanParameter(string name, bool? value)
        {
            var param = new SqlParameter(name, SqlDbType.Bit);
            param.Value = value.HasValue ? (object)value.Value : DBNull.Value;
            return param;
        }
    }
}
