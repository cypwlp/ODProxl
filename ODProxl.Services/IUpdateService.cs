using ODProxl.EntityModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.Services
{
    public interface IUpdateService
    {
        Task UpdateODProxlAsync(string countryCode);
    }
}
