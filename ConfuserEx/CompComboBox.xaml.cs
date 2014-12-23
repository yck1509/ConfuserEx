using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Confuser.Core;

namespace ConfuserEx {
	public partial class CompComboBox : UserControl {
		public static readonly DependencyProperty ComponentsProperty = DependencyProperty.Register("Components", typeof(IEnumerable<ConfuserComponent>), typeof(CompComboBox), new UIPropertyMetadata(null));
		public static readonly DependencyProperty SelectedComponentProperty = DependencyProperty.Register("SelectedComponent", typeof(ConfuserComponent), typeof(CompComboBox), new UIPropertyMetadata(null));
		public static readonly DependencyProperty ArgumentsProperty = DependencyProperty.Register("Arguments", typeof(Dictionary<string, string>), typeof(CompComboBox), new UIPropertyMetadata(null));

		public CompComboBox() {
			InitializeComponent();
		}

		public IEnumerable<ConfuserComponent> Components {
			get { return (IEnumerable<ConfuserComponent>)GetValue(ComponentsProperty); }
			set { SetValue(ComponentsProperty, value); }
		}

		public ConfuserComponent SelectedComponent {
			get { return (ConfuserComponent)GetValue(SelectedComponentProperty); }
			set { SetValue(SelectedComponentProperty, value); }
		}

		public Dictionary<string, string> Arguments {
			get { return (Dictionary<string, string>)GetValue(ArgumentsProperty); }
			set { SetValue(ArgumentsProperty, value); }
		}
	}
}