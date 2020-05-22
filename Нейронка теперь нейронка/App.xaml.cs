using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;

namespace Нейронка_теперь_нейронка
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
		private Exception lastHandledException = null;

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
			AppDomain.CurrentDomain.UnhandledException += (x, y) => SaveException((Exception)y.ExceptionObject);
			Current.Dispatcher.UnhandledException += (x, y) => SaveException(y.Exception);
			Current.DispatcherUnhandledException += (x, y) => SaveException(y.Exception);
		}

		private void SaveException(Exception e)
		{
			if (e == lastHandledException)
				return;

			if (!Directory.Exists("logs"))
				Directory.CreateDirectory("logs");

			var path = Path.Combine(Directory.GetCurrentDirectory(), $@"logs\{DateTime.Now.ToShortDateString()}_{DateTime.Now.ToLongTimeString().Replace(':', '.')}.log");


			using (var sw = new StreamWriter(path))
			{
				sw.Write(e.ToString());
			}

			MessageBox.Show("Возникло необработанное исключение. Подробности сохранены в папку /logs.", "Необработанное исключение.");

			lastHandledException = e;
			Application.Current.Shutdown();
		}
	}
}
