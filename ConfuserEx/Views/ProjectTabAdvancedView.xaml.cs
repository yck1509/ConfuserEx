using System;
using System.Diagnostics;
using System.Windows;
using ConfuserEx.ViewModel;
using GalaSoft.MvvmLight.Command;
using Ookii.Dialogs.Wpf;

namespace ConfuserEx.Views {
	public partial class ProjectTabAdvancedView : Window {
		readonly ProjectVM project;

		public ProjectTabAdvancedView(ProjectVM project) {
			InitializeComponent();
			this.project = project;
			DataContext = project;
		}

		public override void OnApplyTemplate() {
			base.OnApplyTemplate();

			AddPlugin.Command = new RelayCommand(() => {
				var ofd = new VistaOpenFileDialog();
				ofd.Filter = ".NET assemblies (*.exe, *.dll)|*.exe;*.dll|All Files (*.*)|*.*";
				ofd.Multiselect = true;
				if (ofd.ShowDialog() ?? false) {
					foreach (string plugin in ofd.FileNames) {
						try {
							ComponentDiscovery.LoadComponents(project.Protections, project.Packers, plugin);
							project.Plugins.Add(new StringItem(plugin));
						}
						catch {
							MessageBox.Show("Failed to load plugin '" + plugin + "'.");
						}
					}
				}
			});

			RemovePlugin.Command = new RelayCommand(() => {
				int selIndex = PluginPaths.SelectedIndex;
				Debug.Assert(selIndex != -1);

				string plugin = project.Plugins[selIndex].Item;
				ComponentDiscovery.RemoveComponents(project.Protections, project.Packers, plugin);
				project.Plugins.RemoveAt(selIndex);

				PluginPaths.SelectedIndex = selIndex >= project.Plugins.Count ? project.Plugins.Count - 1 : selIndex;
			}, () => PluginPaths.SelectedIndex != -1);


			AddProbe.Command = new RelayCommand(() => {
				var fbd = new VistaFolderBrowserDialog();
				if (fbd.ShowDialog() ?? false)
					project.ProbePaths.Add(new StringItem(fbd.SelectedPath));
			});

			RemoveProbe.Command = new RelayCommand(() => {
				int selIndex = ProbePaths.SelectedIndex;
				Debug.Assert(selIndex != -1);
				project.ProbePaths.RemoveAt(selIndex);
				ProbePaths.SelectedIndex = selIndex >= project.ProbePaths.Count ? project.ProbePaths.Count - 1 : selIndex;
			}, () => ProbePaths.SelectedIndex != -1);
		}
	}
}