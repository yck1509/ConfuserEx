using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Confuser.Core.Project;
using ConfuserEx.Views;
using GalaSoft.MvvmLight.Command;
using Ookii.Dialogs.Wpf;

namespace ConfuserEx.ViewModel {
	public class ProjectTabVM : TabViewModel {
		private int selIndex = -1;

		public ProjectTabVM(AppVM app)
			: base(app, "Project") { }

		public ICommand DragOver {
			get {
				return new RelayCommand<DragEventArgs>(e => {
					e.Effects = DragDropEffects.None;
					if (e.Data.GetDataPresent(DataFormats.FileDrop))
						e.Effects = DragDropEffects.Link;
				});
			}
		}

		public ICommand Drop {
			get {
				return new RelayCommand<DragEventArgs>(e => {
					foreach (string file in (string[])e.Data.GetData(DataFormats.FileDrop))
						AddModule(file);
				});
			}
		}

		public int SelectedIndex {
			get { return selIndex; }
			set { SetProperty(ref selIndex, value, "SelectedIndex"); }
		}

		public ICommand ChooseBaseDir {
			get {
				return new RelayCommand(() => {
					var fbd = new VistaFolderBrowserDialog();
					fbd.SelectedPath = App.Project.BaseDirectory;
					if (fbd.ShowDialog() ?? false) {
						App.Project.BaseDirectory = fbd.SelectedPath;
						App.Project.OutputDirectory = Path.Combine(App.Project.BaseDirectory, "Confused");
					}
				});
			}
		}

		public ICommand ChooseOutputDir {
			get {
				return new RelayCommand(() => {
					var fbd = new VistaFolderBrowserDialog();
					fbd.SelectedPath = App.Project.OutputDirectory;
					if (fbd.ShowDialog() ?? false) {
						App.Project.OutputDirectory = fbd.SelectedPath;
					}
				});
			}
		}

		public ICommand Add {
			get {
				return new RelayCommand(() => {
					var ofd = new VistaOpenFileDialog();
					ofd.Filter = ".NET assemblies (*.exe, *.dll)|*.exe;*.dll|All Files (*.*)|*.*";
					if (ofd.ShowDialog() ?? false) {
						AddModule(ofd.FileName);
					}
				});
			}
		}

		public ICommand Remove {
			get {
				return new RelayCommand(() => {
					Debug.Assert(SelectedIndex != -1);
					ProjectModuleVM module = App.Project.Modules[SelectedIndex];
					string msg = string.Format("Are you sure to remove module '{0}'?\r\nAll settings specific to it would be lost!", module.Path);
					if (MessageBox.Show(msg, "ConfuserEx", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
						App.Project.Modules.RemoveAt(SelectedIndex);
				}, () => SelectedIndex != -1);
			}
		}

		public ICommand Edit {
			get {
				return new RelayCommand(() => {
					Debug.Assert(SelectedIndex != -1);
					var dialog = new ProjectModuleView(App.Project.Modules[SelectedIndex]);
					dialog.Owner = Application.Current.MainWindow;
					dialog.ShowDialog();
				}, () => SelectedIndex != -1);
			}
		}

		public ICommand Advanced {
			get { return new RelayCommand(() => { }); }
		}

		private void AddModule(string file) {
			if (!File.Exists(file)) {
				MessageBox.Show(string.Format("File '{0}' does not exists!", file), "ConfuserEx", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			if (string.IsNullOrEmpty(App.Project.BaseDirectory)) {
				string directory = Path.GetDirectoryName(file);
				App.Project.BaseDirectory = directory;
				App.Project.OutputDirectory = Path.Combine(directory, "Confused");
			}
			var module = new ProjectModuleVM(App.Project, new ProjectModule());
			module.Path = Confuser.Core.Utils.GetRelativePath(file, App.Project.BaseDirectory);
			App.Project.Modules.Add(module);
		}
	}
}