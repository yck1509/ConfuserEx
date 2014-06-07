using System;

namespace ConfuserEx.ViewModel {
	public abstract class TabViewModel : ViewModelBase {
		protected TabViewModel(AppVM app, string header) {
			Header = header;
		}

		public AppVM App { get; private set; }
		public string Header { get; private set; }
	}
}