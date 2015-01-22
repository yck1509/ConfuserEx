using System.IO;
using System.Windows;
using System.Xml;
using Confuser.Core.Project;
using ConfuserEx.ViewModel;

namespace ConfuserEx {
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			AppVM app = null;

			if (e.Args.Length == 1 && File.Exists(e.Args[0]))
			{
				string filename = e.Args[0];

				try
				{
					var xmlDoc = new XmlDocument();
					xmlDoc.Load(filename);

					var proj = new ConfuserProject();
					proj.Load(xmlDoc);

					app = new AppVM(false);
					app.Project = new ProjectVM(proj);
					app.FileName = filename;
				}
				catch
				{
					MessageBox.Show("Project file could not be loaded!", "ConfuserEx", MessageBoxButton.OK, MessageBoxImage.Error);
					this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
					this.Shutdown();
					return;
				}
			}
			else
			{
				app = new AppVM();
				app.Project = new ProjectVM(new ConfuserProject());
				app.FileName = "Unnamed.crproj";
			}

			app.Tabs.Add(new ProjectTabVM(app));
			app.Tabs.Add(new SettingsTabVM(app));
			app.Tabs.Add(new ProtectTabVM(app));
			app.Tabs.Add(new AboutTabVM(app));

			var window = new MainWindow()
			{
				DataContext = app
			};
			window.Closing += (s, ea) =>
			{
				ea.Cancel = !app.OnWindowClosing();
			};

			window.ShowDialog();
		}
	}
}