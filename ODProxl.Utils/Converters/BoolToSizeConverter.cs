using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Data.Converters;
using Avalonia.Layout;
namespace ODProxl.Utils.Converters
{
    public class BoolToSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded && parameter is string param)
            {
                var parts = param.Split(',');
                if (parts.Length == 2)
                {
                    return isExpanded ?
                        double.Parse(parts[0].Trim()) :
                        double.Parse(parts[1].Trim());
                }
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
