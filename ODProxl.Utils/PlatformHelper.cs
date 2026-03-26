using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ODProxl.Utils
{
    public class PlatformHelper
    {
        public static string GetCurrentRid()
        {
            if (OperatingSystem.IsWindows()) return "win-x64";

            if (OperatingSystem.IsMacOS())
            {
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                    ? "osx-arm64"
                    : "osx-x64";
            }

            if (OperatingSystem.IsLinux()) return "linux-x64";

            return "win-x64"; // 預設
        }
    }
}
