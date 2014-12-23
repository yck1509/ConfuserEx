using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Confuser.Core;
using Ookii.Dialogs.Wpf;

namespace ConfuserEx {
	/// <summary>
	///     Interaction logic for StackTraceDecoder.xaml
	/// </summary>
	public partial class StackTraceDecoder : Window {
		public StackTraceDecoder() {
			InitializeComponent();
		}

		readonly Dictionary<string, string> symMap = new Dictionary<string, string>();

		void PathBox_TextChanged(object sender, TextChangedEventArgs e) {
			if (File.Exists(PathBox.Text))
				LoadSymMap(PathBox.Text);
		}

		void LoadSymMap(string path) {
			string shortPath = path;
			if (path.Length > 35)
				shortPath = "..." + path.Substring(path.Length - 35, 35);

			try {
				symMap.Clear();
				using (var reader = new StreamReader(File.OpenRead(path))) {
					var line = reader.ReadLine();
					while (line != null) {
						int tabIndex = line.IndexOf('\t');
						if (tabIndex == -1)
							throw new FileFormatException();
						symMap.Add(line.Substring(0, tabIndex), line.Substring(tabIndex + 1));
						line = reader.ReadLine();
					}
				}
				status.Content = "Loaded symbol map from '" + shortPath + "' successfully.";
			}
			catch {
				status.Content = "Failed to load symbol map from '" + shortPath + "'.";
			}
		}

		void ChooseMapPath(object sender, RoutedEventArgs e) {
			var ofd = new VistaOpenFileDialog();
			ofd.Filter = "Symbol maps (*.map)|*.map|All Files (*.*)|*.*";
			if (ofd.ShowDialog() ?? false) {
				PathBox.Text = ofd.FileName;
			}
		}

		readonly Regex symbolMatcher = new Regex("=[a-zA-Z0-9]+=");

		void Decode_Click(object sender, RoutedEventArgs e) {
			var trace = stackTrace.Text;
			stackTrace.Text = symbolMatcher.Replace(trace, DecodeSymbol);
		}

		string DecodeSymbol(Match match) {
			var sym = match.Value;
			return symMap.GetValueOrDefault(sym, sym);
		}
	}
}