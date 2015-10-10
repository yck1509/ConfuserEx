using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ConfuserEx {
	internal class BoolToVisibilityConverter : IValueConverter {
		public static readonly BoolToVisibilityConverter Instance = new BoolToVisibilityConverter();
		BoolToVisibilityConverter() { }

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			Debug.Assert(value is bool);
			Debug.Assert(targetType == typeof(Visibility));
			return (bool)value ? Visibility.Visible : Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			throw new NotSupportedException();
		}
	}
}