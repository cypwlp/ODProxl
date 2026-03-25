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
    public class BoolToMenuIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                // 這些是 SVG 路徑數據（簡化的箭頭圖標）
                return isExpanded ?
                    "M15.41 16.59L10.83 12l4.58-4.59L14 6l-6 6 6 6 1.41-1.41z" : // 左箭頭
                    "M8.59 16.59L13.17 12 8.59 7.41 10 6l6 6-6 6-1.41-1.41z";   // 右箭頭
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
