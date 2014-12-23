using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Confuser.Core;
using Confuser.Core.Project;
using GalaSoft.MvvmLight.Command;

namespace ConfuserEx.ViewModel {
	internal class ProtectTabVM : TabViewModel, ILogger {
		readonly Paragraph documentContent;
		CancellationTokenSource cancelSrc;
		double? progress = 0;
		bool? result;

		public ProtectTabVM(AppVM app)
			: base(app, "Protect!") {
			documentContent = new Paragraph();
			LogDocument = new FlowDocument();
			LogDocument.Blocks.Add(documentContent);
		}

		public ICommand ProtectCmd {
			get { return new RelayCommand(DoProtect, () => !App.NavigationDisabled); }
		}

		public ICommand CancelCmd {
			get { return new RelayCommand(DoCancel, () => App.NavigationDisabled); }
		}

		public double? Progress {
			get { return progress; }
			set { SetProperty(ref progress, value, "Progress"); }
		}

		public FlowDocument LogDocument { get; private set; }

		public bool? Result {
			get { return result; }
			set { SetProperty(ref result, value, "Result"); }
		}

		void DoProtect() {
			var parameters = new ConfuserParameters();
			parameters.Project = ((IViewModel<ConfuserProject>)App.Project).Model;
			if (File.Exists(App.FileName))
				Environment.CurrentDirectory = Path.GetDirectoryName(App.FileName);
			parameters.Logger = this;

			documentContent.Inlines.Clear();
			cancelSrc = new CancellationTokenSource();
			Result = null;
			Progress = null;
			begin = DateTime.Now;
			App.NavigationDisabled = true;

			ConfuserEngine.Run(parameters, cancelSrc.Token)
			              .ContinueWith(_ =>
			                            Application.Current.Dispatcher.BeginInvoke(new Action(() => {
				                            Progress = 0;
				                            App.NavigationDisabled = false;
				                            CommandManager.InvalidateRequerySuggested();
			                            })));
		}

		void DoCancel() {
			cancelSrc.Cancel();
		}

		void AppendLine(string format, Brush foreground, params object[] args) {
			Application.Current.Dispatcher.BeginInvoke(new Action(() => {
				documentContent.Inlines.Add(new Run(string.Format(format, args)) { Foreground = foreground });
				documentContent.Inlines.Add(new LineBreak());
			}));
		}

		#region Logger Impl

		DateTime begin;

		void ILogger.Debug(string msg) {
			AppendLine("[DEBUG] {0}", Brushes.Gray, msg);
		}

		void ILogger.DebugFormat(string format, params object[] args) {
			AppendLine("[DEBUG] {0}", Brushes.Gray, string.Format(format, args));
		}

		void ILogger.Info(string msg) {
			AppendLine(" [INFO] {0}", Brushes.White, msg);
		}

		void ILogger.InfoFormat(string format, params object[] args) {
			AppendLine(" [INFO] {0}", Brushes.White, string.Format(format, args));
		}

		void ILogger.Warn(string msg) {
			AppendLine(" [WARN] {0}", Brushes.Yellow, msg);
		}

		void ILogger.WarnFormat(string format, params object[] args) {
			AppendLine(" [WARN] {0}", Brushes.Yellow, string.Format(format, args));
		}

		void ILogger.WarnException(string msg, Exception ex) {
			AppendLine(" [WARN] {0}", Brushes.Yellow, msg);
			AppendLine("Exception: {0}", Brushes.Yellow, ex);
		}

		void ILogger.Error(string msg) {
			AppendLine("[ERROR] {0}", Brushes.Red, msg);
		}

		void ILogger.ErrorFormat(string format, params object[] args) {
			AppendLine("[ERROR] {0}", Brushes.Red, string.Format(format, args));
		}

		void ILogger.ErrorException(string msg, Exception ex) {
			AppendLine("[ERROR] {0}", Brushes.Red, msg);
			AppendLine("Exception: {0}", Brushes.Red, ex);
		}

		void ILogger.Progress(int progress, int overall) {
			Progress = (double)progress / overall;
		}

		void ILogger.EndProgress() {
			Progress = null;
		}

		void ILogger.Finish(bool successful) {
			DateTime now = DateTime.Now;
			string timeString = string.Format(
				"at {0}, {1}:{2:d2} elapsed.",
				now.ToShortTimeString(),
				(int)now.Subtract(begin).TotalMinutes,
				now.Subtract(begin).Seconds);
			if (successful)
				AppendLine("Finished {0}", Brushes.Lime, timeString);
			else
				AppendLine("Failed {0}", Brushes.Red, timeString);
			Result = successful;
		}

		#endregion
	}
}