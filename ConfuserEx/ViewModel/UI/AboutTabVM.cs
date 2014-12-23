using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using GalaSoft.MvvmLight.Command;

namespace ConfuserEx.ViewModel {
	internal class AboutTabVM : TabViewModel {
		public AboutTabVM(AppVM app)
			: base(app, "About") {
			var decoder = new IconBitmapDecoder(new Uri("pack://application:,,,/ConfuserEx.ico"), BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnDemand);

			Icon = decoder.Frames.First(frame => frame.Width == 64);
		}

		public ICommand LaunchBrowser {
			get { return new RelayCommand<string>(site => Process.Start(site)); }
		}

		public BitmapSource Icon { get; private set; }
	}
}