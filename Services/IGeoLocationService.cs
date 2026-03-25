using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.Services
{
    public interface IGeoLocationService
    {
        Task<string> GetCountryCodeAsync(CancellationToken cancellationToken = default);
    }
}
