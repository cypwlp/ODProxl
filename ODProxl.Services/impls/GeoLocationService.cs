using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ODProxl.Services.impls
{
    public class GeoLocationService : IGeoLocationService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        public async Task<string> GetCountryCodeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetStringAsync("https://api.ip.sb/geoip", cancellationToken);
                using var doc = JsonDocument.Parse(response);
                if (doc.RootElement.TryGetProperty("country_code", out var codeElement))
                {
                    return codeElement.GetString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IP 归属地查询失败: {ex.Message}");
            }
            return null;
        }
    }
}
