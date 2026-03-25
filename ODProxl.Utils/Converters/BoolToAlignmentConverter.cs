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
    public class BoolToAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded && parameter is string param)
            {
                var parts = param.Split(',');
                if (parts.Length == 2)
                {
                    return isExpanded ?
                        Avalonia.Layout.HorizontalAlignment.Left :
                        Avalonia.Layout.HorizontalAlignment.Stretch;
                }
            }
            return Avalonia.Layout.HorizontalAlignment.Stretch;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
