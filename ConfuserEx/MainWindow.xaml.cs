using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ConfuserEx {
	public partial class MainWindow : Window {
		public MainWindow() {
			InitializeComponent();
		}

		private void OpenMenu(object sender, RoutedEventArgs e) {
			var btn = (Button)sender;
			ContextMenu menu = btn.ContextMenu;
			menu.PlacementTarget = btn;
			menu.Placement = PlacementMode.Bottom;
			menu.IsOpen = true;
		}
	}
}