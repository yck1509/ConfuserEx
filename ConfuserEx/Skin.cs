using System;
using System.Windows;

namespace ConfuserEx {
	public class Skin {
		public static readonly DependencyProperty EmptyPromptProperty =
			DependencyProperty.RegisterAttached("EmptyPrompt", typeof (string), typeof (Skin), new UIPropertyMetadata(null));

		public static string GetEmptyPrompt(DependencyObject obj) {
			return (string)obj.GetValue(EmptyPromptProperty);
		}

		public static void SetEmptyPrompt(DependencyObject obj, string value) {
			obj.SetValue(EmptyPromptProperty, value);
		}
	}
}