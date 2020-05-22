using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ActionDetector
{
	/// <summary>
	/// Логика взаимодействия для CameraSelectWindow.xaml
	/// </summary>
	public partial class CameraSelectWindow : Window
	{
		string address = "";
		public bool imShown = false;

		public CameraSelectWindow()
		{
			InitializeComponent();
			if (File.Exists("lastIP"))
			{
				var sr = new StreamReader("lastIP");
				adres.Text = sr.ReadLine();
				sr.Close();

			}
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			address = "USB";
			DialogResult = true;
		}

		private void Button_Click_1(object sender, RoutedEventArgs e)
		{
			Tabs.SelectedIndex = 1;
		}

		public string GetCam()
		{
			Tabs.SelectedIndex = 0;
			ShowDialog();
			return address;
		}

		public void ShowBusyBox()
		{
			Tabs.SelectedIndex = 2;
			Height = 100;
			Width = 200;
			ReconnectAttempt.IsBusy = true;
			imShown = true;
			Show();
		}

		private void Button_Click_2(object sender, RoutedEventArgs e)
		{
			address = adres.Text;
			var sw = new StreamWriter("lastIP");
			sw.WriteLine(adres.Text);
			sw.Close();
			DialogResult = true;
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			imShown = false;
		}
	}
}
