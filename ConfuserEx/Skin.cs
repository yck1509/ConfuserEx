using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace ConfuserEx {
	public class Skin {
		public static readonly DependencyProperty EmptyPromptProperty =
			DependencyProperty.RegisterAttached("EmptyPrompt", typeof (string), typeof (Skin), new UIPropertyMetadata(null));

		public static readonly DependencyProperty TabsDisabledProperty =
			DependencyProperty.RegisterAttached("TabsDisabled", typeof (bool), typeof (Skin), new UIPropertyMetadata(false));

		public static readonly DependencyProperty RTBDocumentProperty =
			DependencyProperty.RegisterAttached("RTBDocument", typeof (FlowDocument), typeof (Skin), new FrameworkPropertyMetadata(null, OnRTBDocumentChanged));

		public static string GetEmptyPrompt(DependencyObject obj) {
			return (string)obj.GetValue(EmptyPromptProperty);
		}

		public static void SetEmptyPrompt(DependencyObject obj, string value) {
			obj.SetValue(EmptyPromptProperty, value);
		}

		public static bool GetTabsDisabled(DependencyObject obj) {
			return (bool)obj.GetValue(TabsDisabledProperty);
		}

		public static void SetTabsDisabled(DependencyObject obj, bool value) {
			obj.SetValue(TabsDisabledProperty, value);
		}

		public static void OnRTBDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs dpe) {
			var rtb = (RichTextBox)d;
			rtb.Document = (FlowDocument)dpe.NewValue;
			rtb.TextChanged += (sender, e) => rtb.ScrollToEnd();
		}

		public static FlowDocument GetRTBDocument(DependencyObject obj) {
			return (FlowDocument)obj.GetValue(RTBDocumentProperty);
		}

		public static void SetRTBDocument(DependencyObject obj, FlowDocument value) {
			obj.SetValue(RTBDocumentProperty, value);
		}
	}
}