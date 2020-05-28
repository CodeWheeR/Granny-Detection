using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Markup;

namespace ActionDetector
{
	/// <summary>
	///     Логика взаимодействия для App.xaml
	/// </summary>
	public partial class App
	{
		#region Fields

		#region Private

		private Exception lastHandledException;

		#endregion

		#endregion

		#region .ctor

		static App()
		{
			Thread.CurrentThread.CurrentCulture = CultureInfo.CurrentCulture;
			Thread.CurrentThread.CurrentUICulture = CultureInfo.CurrentCulture;
			FrameworkElement.LanguageProperty.OverrideMetadata(
				typeof(FrameworkElement),
				new FrameworkPropertyMetadata(
					XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
		}

		public App()
		{
			AppDomain.CurrentDomain.UnhandledException += (x, y) =>
			{
				SaveException((Exception) y.ExceptionObject);
			};
			Current.Dispatcher.UnhandledException += (x, y) =>
			{
				SaveException(y.Exception);
			};
			Current.DispatcherUnhandledException += (x, y) =>
			{
				SaveException(y.Exception);
			};

			var window = new MainWindow();
			window.Show();
		}

		#endregion

		#region Private methods

		private void SaveException(Exception e)
		{
			if (e == lastHandledException)
			{
				return;
			}

			if (!Directory.Exists("logs"))
			{
				Directory.CreateDirectory("logs");
			}

			var path = Path.Combine(Directory.GetCurrentDirectory(), $@"logs\{DateTime.Now.ToShortDateString()}_{DateTime.Now.ToLongTimeString().Replace(':', '.')}.log");

			using (var sw = new StreamWriter(path))
			{
				sw.Write(e.ToString());
			}

			MessageBox.Show("Возникло необработанное исключение. Подробности сохранены в папку /logs.", "Необработанное исключение.");

			lastHandledException = e;
			Current.Shutdown();
		}

		#endregion
	}
}