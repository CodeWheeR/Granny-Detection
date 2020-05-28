using System.ComponentModel;
using System.IO;
using System.Windows;

namespace ActionDetector
{
	/// <summary>
	///     Логика взаимодействия для CameraSelectWindow.xaml
	/// </summary>
	public partial class CameraSelectWindow : Window
	{
		#region Fields

		#region Public

		public bool imShown;

		#endregion

		#region Private

		private string address = "";

		#endregion

		#endregion

		#region .ctor

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

		#endregion

		#region Public methods

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

		#endregion

		#region Private methods

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			address = "USB";
			DialogResult = true;
		}

		private void Button_Click_1(object sender, RoutedEventArgs e) => Tabs.SelectedIndex = 1;

		private void Button_Click_2(object sender, RoutedEventArgs e)
		{
			address = adres.Text;
			var sw = new StreamWriter("lastIP");
			sw.WriteLine(adres.Text);
			sw.Close();
			DialogResult = true;
		}

		private void Window_Closing(object sender, CancelEventArgs e) => imShown = false;

		#endregion
	}
}