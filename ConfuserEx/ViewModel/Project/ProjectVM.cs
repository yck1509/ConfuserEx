using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Confuser.Core;
using Confuser.Core.Project;

namespace ConfuserEx.ViewModel {
	public class ProjectVM : ViewModelBase, IViewModel<ConfuserProject> {
		private readonly ConfuserProject proj;
		private bool modified;

		public ProjectVM(ConfuserProject proj) {
			this.proj = proj;

			ObservableCollection<ProjectModuleVM> modules = Utils.Wrap(proj, module => new ProjectModuleVM(this, module));
			modules.CollectionChanged += (sender, e) => IsModified = true;
			Modules = modules;

			var plugins = Utils.Wrap(proj.PluginPaths, path => new StringItem(path));
			plugins.CollectionChanged += (sender, e) => IsModified = true;
			Plugins = plugins;

			ObservableCollection<StringItem> probePaths = Utils.Wrap(proj.ProbePaths, path => new StringItem(path));
			probePaths.CollectionChanged += (sender, e) => IsModified = true;
			ProbePaths = probePaths;

			Protections = new ObservableCollection<ConfuserComponent>();
			Packers = new ObservableCollection<ConfuserComponent>();
			ComponentDiscovery.LoadComponents(Protections, Packers, Assembly.Load("Confuser.Protections").Location);
			ComponentDiscovery.LoadComponents(Protections, Packers, Assembly.Load("Confuser.Renamer").Location);
		}

		public ConfuserProject Project {
			get { return proj; }
		}

		public bool IsModified {
			get { return modified; }
			set { SetProperty(ref modified, value, "IsModified"); }
		}

		public string Seed {
			get { return proj.Seed; }
			set { SetProperty(proj.Seed != value, val => proj.Seed = val, value, "Seed"); }
		}

		public bool Debug {
			get { return proj.Debug; }
			set { SetProperty(proj.Debug != value, val => proj.Debug = val, value, "Debug"); }
		}

		public string BaseDirectory {
			get { return proj.BaseDirectory; }
			set { SetProperty(proj.BaseDirectory != value, val => proj.BaseDirectory = val, value, "BaseDirectory"); }
		}

		public string OutputDirectory {
			get { return proj.OutputDirectory; }
			set { SetProperty(proj.OutputDirectory != value, val => proj.OutputDirectory = val, value, "OutputDirectory"); }
		}

		public IList<ProjectModuleVM> Modules { get; private set; }
		public IList<StringItem> Plugins { get; private set; }
		public IList<StringItem> ProbePaths { get; private set; }

		public ObservableCollection<ConfuserComponent> Protections { get; private set; }
		public ObservableCollection<ConfuserComponent> Packers { get; private set; }

		ConfuserProject IViewModel<ConfuserProject>.Model {
			get { return proj; }
		}

		protected override void OnPropertyChanged(string property) {
			base.OnPropertyChanged(property);
			if (property != "IsModified")
				IsModified = true;
		}
	}
}