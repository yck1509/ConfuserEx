using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;

namespace ConfuserEx {
	internal class InvertBoolConverter : IValueConverter {
		public static readonly InvertBoolConverter Instance = new InvertBoolConverter();
		InvertBoolConverter() { }

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			Debug.Assert(value is bool);
			Debug.Assert(targetType == typeof(bool));
			return !(bool)value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			throw new NotSupportedException();
		}
	}
}