using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Confuser.Core.Project;
using ConfuserEx.ViewModel;

namespace ConfuserEx {
	public partial class MainWindow : Window {
		public MainWindow() {
			InitializeComponent();

			var app = new AppVM();
			app.Project = new ProjectVM(new ConfuserProject());
			app.FileName = "Unnamed.crproj";

			app.Tabs.Add(new ProjectTabVM(app));
			app.Tabs.Add(new SettingsTabVM(app));
			app.Tabs.Add(new ProtectTabVM(app));
			app.Tabs.Add(new AboutTabVM(app));

			DataContext = app;
		}

		void OpenMenu(object sender, RoutedEventArgs e) {
			var btn = (Button)sender;
			ContextMenu menu = btn.ContextMenu;
			menu.PlacementTarget = btn;
			menu.Placement = PlacementMode.MousePoint;
			menu.IsOpen = true;
		}

		protected override void OnClosing(CancelEventArgs e) {
			base.OnClosing(e);
			e.Cancel = !((AppVM)DataContext).OnWindowClosing();
		}
	}
}