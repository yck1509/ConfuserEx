using System;
using System.Windows;
using System.Windows.Controls;
using ConfuserEx.ViewModel;
using Ookii.Dialogs.Wpf;

namespace ConfuserEx.Views {
	public partial class ProjectModuleView : Window {
		readonly ProjectModuleVM module;

		public ProjectModuleView(ProjectModuleVM module) {
			InitializeComponent();
			this.module = module;
			DataContext = module;
			PwdBox.IsEnabled = !string.IsNullOrEmpty(PathBox.Text);
		}

		void Done(object sender, RoutedEventArgs e) {
			DialogResult = true;
		}

		void PathBox_TextChanged(object sender, TextChangedEventArgs e) {
			PwdBox.IsEnabled = !string.IsNullOrEmpty(PathBox.Text);
		}

		void ChooseSNKey(object sender, RoutedEventArgs e) {
			var ofd = new VistaOpenFileDialog();
			ofd.Filter = "Supported Key Files (*.snk, *.pfx)|*.snk;*.pfx|All Files (*.*)|*.*";
			if (ofd.ShowDialog() ?? false) {
				module.SNKeyPath = ofd.FileName;
			}
		}
	}
}