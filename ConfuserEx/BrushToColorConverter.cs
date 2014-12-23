using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ConfuserEx {
	public class BrushToColorConverter : IValueConverter {
		public static readonly BrushToColorConverter Instance = new BrushToColorConverter();
		BrushToColorConverter() { }

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			var brush = value as SolidColorBrush;
			if (brush != null)
				return brush.Color;
			return null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}
	}
}