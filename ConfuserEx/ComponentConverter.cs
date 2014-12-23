using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Confuser.Core;

namespace ConfuserEx {
	internal class ComponentConverter : Freezable, IValueConverter {
		public static readonly DependencyProperty ComponentsProperty = DependencyProperty.Register("Components", typeof(IList<ConfuserComponent>), typeof(ComponentConverter), new UIPropertyMetadata(null));

		public IList<ConfuserComponent> Components {
			get { return (IList<ConfuserComponent>)GetValue(ComponentsProperty); }
			set { SetValue(ComponentsProperty, value); }
		}

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			Debug.Assert(value is string || value == null);
			Debug.Assert(targetType == typeof(ConfuserComponent));
			Debug.Assert(Components != null);

			if (value == null) return null;
			return Components.Single(comp => comp.Id == (string)value);
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			Debug.Assert(value is ConfuserComponent || value == null);
			Debug.Assert(targetType == typeof(string));

			if (value == null) return null;
			return ((ConfuserComponent)value).Id;
		}

		protected override Freezable CreateInstanceCore() {
			return new ComponentConverter();
		}
	}
}