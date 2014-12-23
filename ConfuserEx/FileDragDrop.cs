using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ConfuserEx.ViewModel;
using GalaSoft.MvvmLight.Command;

namespace ConfuserEx {
	public class FileDragDrop {
		public static readonly DependencyProperty CommandProperty =
			DependencyProperty.RegisterAttached("Command", typeof(ICommand), typeof(FileDragDrop), new UIPropertyMetadata(null, OnCommandChanged));

		public static ICommand FileCmd = new DragDropCommand(
			data => {
				Debug.Assert(data.Item2.GetDataPresent(DataFormats.FileDrop));
				if (data.Item1 is TextBox) {
					string file = ((string[])data.Item2.GetData(DataFormats.FileDrop))[0];
					Debug.Assert(File.Exists(file));
					((TextBox)data.Item1).Text = file;
				}
				else if (data.Item1 is ListBox) {
					var files = (string[])data.Item2.GetData(DataFormats.FileDrop);
					Debug.Assert(files.All(file => File.Exists(file)));
					var list = (IList<StringItem>)((ListBox)data.Item1).ItemsSource;
					foreach (string file in files)
						list.Add(new StringItem(file));
				}
				else
					throw new NotSupportedException();
			}, data => {
				if (!data.Item2.GetDataPresent(DataFormats.FileDrop))
					return false;
				return ((string[])data.Item2.GetData(DataFormats.FileDrop)).All(file => File.Exists(file));
			});


		public static ICommand DirectoryCmd = new DragDropCommand(
			data => {
				Debug.Assert(data.Item2.GetDataPresent(DataFormats.FileDrop));
				if (data.Item1 is TextBox) {
					string dir = ((string[])data.Item2.GetData(DataFormats.FileDrop))[0];
					Debug.Assert(Directory.Exists(dir));
					((TextBox)data.Item1).Text = dir;
				}
				else if (data.Item1 is ListBox) {
					var dirs = (string[])data.Item2.GetData(DataFormats.FileDrop);
					Debug.Assert(dirs.All(dir => Directory.Exists(dir)));
					var list = (IList<StringItem>)((ListBox)data.Item1).ItemsSource;
					foreach (string dir in dirs)
						list.Add(new StringItem(dir));
				}
				else
					throw new NotSupportedException();
			}, data => {
				if (!data.Item2.GetDataPresent(DataFormats.FileDrop))
					return false;
				return ((string[])data.Item2.GetData(DataFormats.FileDrop)).All(dir => Directory.Exists(dir));
			});

		public static ICommand GetCommand(DependencyObject obj) {
			return (ICommand)obj.GetValue(CommandProperty);
		}

		public static void SetCommand(DependencyObject obj, ICommand value) {
			obj.SetValue(CommandProperty, value);
		}

		static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			var elem = (UIElement)d;
			if (e.NewValue != null) {
				elem.AllowDrop = true;
				elem.PreviewDragOver += OnDragOver;
				elem.PreviewDrop += OnDrop;
			}
			else {
				elem.AllowDrop = false;
				elem.PreviewDragOver -= OnDragOver;
				elem.PreviewDrop -= OnDrop;
			}
		}

		static void OnDragOver(object sender, DragEventArgs e) {
			ICommand cmd = GetCommand((DependencyObject)sender);
			e.Effects = DragDropEffects.None;
			if (cmd is DragDropCommand) {
				if (cmd.CanExecute(Tuple.Create((UIElement)sender, e.Data)))
					e.Effects = DragDropEffects.Link;
			}
			else {
				if (cmd.CanExecute(e.Data))
					e.Effects = DragDropEffects.Link;
			}
			e.Handled = true;
		}

		static void OnDrop(object sender, DragEventArgs e) {
			ICommand cmd = GetCommand((DependencyObject)sender);
			if (cmd is DragDropCommand) {
				if (cmd.CanExecute(Tuple.Create((UIElement)sender, e.Data)))
					cmd.Execute(Tuple.Create((UIElement)sender, e.Data));
			}
			else {
				if (cmd.CanExecute(e.Data))
					cmd.Execute(e.Data);
			}
			e.Handled = true;
		}


		class DragDropCommand : RelayCommand<Tuple<UIElement, IDataObject>> {
			public DragDropCommand(Action<Tuple<UIElement, IDataObject>> execute, Func<Tuple<UIElement, IDataObject>, bool> canExecute)
				: base(execute, canExecute) { }
		}
	}
}