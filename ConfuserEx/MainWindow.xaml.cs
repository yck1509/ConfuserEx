using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Xml;
using Confuser.Core.Project;
using ConfuserEx.ViewModel;

namespace ConfuserEx {
	public partial class MainWindow : Window {
		public MainWindow() {
			InitializeComponent();

			var app = new AppVM();
			app.Project = new ProjectVM(new ConfuserProject(), null);
			app.FileName = "Unnamed.crproj";

			app.Tabs.Add(new ProjectTabVM(app));
			app.Tabs.Add(new SettingsTabVM(app));
			app.Tabs.Add(new ProtectTabVM(app));
			app.Tabs.Add(new AboutTabVM(app));

			LoadProj(app);

			DataContext = app;
		}

		void OpenMenu(object sender, RoutedEventArgs e) {
			var btn = (Button)sender;
			ContextMenu menu = btn.ContextMenu;
			menu.PlacementTarget = btn;
			menu.Placement = PlacementMode.MousePoint;
			menu.IsOpen = true;
		}

		void LoadProj(AppVM app) {
			var args = Environment.GetCommandLineArgs();
			if (args.Length != 2 || !File.Exists(args[1]))
				return;

			string fileName = Path.GetFullPath(args[1]);
			try {
				var xmlDoc = new XmlDocument();
				xmlDoc.Load(fileName);
				var proj = new ConfuserProject();
				proj.Load(xmlDoc);
				app.Project = new ProjectVM(proj, fileName);
				app.FileName = fileName;
			}
			catch {
				MessageBox.Show("Invalid project!", "ConfuserEx", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		protected override void OnClosing(CancelEventArgs e) {
			base.OnClosing(e);
			e.Cancel = !((AppVM)DataContext).OnWindowClosing();
		}
	}
}