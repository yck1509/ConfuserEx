using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Confuser.Core;
using Confuser.Core.Project;

namespace ConfuserEx.ViewModel {
	public class ProjectVM : ViewModelBase, IViewModel<ConfuserProject>, IRuleContainer {
		readonly ConfuserProject proj;
		bool modified;
		ProjectSettingVM<Packer> packer;

		public ProjectVM(ConfuserProject proj, string fileName) {
			this.proj = proj;
			FileName = fileName;

			ObservableCollection<ProjectModuleVM> modules = Utils.Wrap(proj, module => new ProjectModuleVM(this, module));
			modules.CollectionChanged += (sender, e) => IsModified = true;
			Modules = modules;

			ObservableCollection<StringItem> plugins = Utils.Wrap(proj.PluginPaths, path => new StringItem(path));
			plugins.CollectionChanged += (sender, e) => IsModified = true;
			Plugins = plugins;

			ObservableCollection<StringItem> probePaths = Utils.Wrap(proj.ProbePaths, path => new StringItem(path));
			probePaths.CollectionChanged += (sender, e) => IsModified = true;
			ProbePaths = probePaths;

			ObservableCollection<ProjectRuleVM> rules = Utils.Wrap(proj.Rules, rule => new ProjectRuleVM(this, rule));
			rules.CollectionChanged += (sender, e) => IsModified = true;
			Rules = rules;

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

		public ProjectSettingVM<Packer> Packer {
			get {
				if (proj.Packer == null)
					packer = null;
				else
					packer = new ProjectSettingVM<Packer>(this, proj.Packer);
				return packer;
			}
			set {
				var vm = (IViewModel<SettingItem<Packer>>)value;
				bool changed = (vm == null && proj.Packer != null) || (vm != null && proj.Packer != vm.Model);
				SetProperty(changed, val => proj.Packer = val == null ? null : val.Model, vm, "Packer");
			}
		}

		public IList<ProjectModuleVM> Modules { get; private set; }
		public IList<StringItem> Plugins { get; private set; }
		public IList<StringItem> ProbePaths { get; private set; }

		public ObservableCollection<ConfuserComponent> Protections { get; private set; }
		public ObservableCollection<ConfuserComponent> Packers { get; private set; }
		public IList<ProjectRuleVM> Rules { get; private set; }

		public string FileName { get; set; }

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