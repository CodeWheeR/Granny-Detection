using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

using FlexiblePlanes;

using Microsoft.Win32;

using OpenCvSharp;
using OpenCvSharp.Extensions;

using Window = System.Windows.Window;

namespace ActionDetector
{
	/// <summary>
	///     Логика взаимодействия для MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		#region Enums

		public enum StreamSrc
		{
			USB_cam,
			Video,
			IP_cam
		}

		public enum ZoneMode
		{
			Allowing_zone,
			Forbidding_zone
		}

		#endregion

		#region Static Fiends and Constants

		#region Public

		/// <summary>
		///     Режим работы с видеопотоком
		/// </summary>
		public static StreamSrc cameraMode = StreamSrc.Video;

		/// <summary>
		///     Режим работы выделенной зоны
		/// </summary>
		public static ZoneMode zoneMode = ZoneMode.Forbidding_zone;

		public static Brush forbidFillingBrush = new SolidColorBrush(Color.FromArgb(30, 150, 0, 0));
		public static Brush forbidLinesBrush = Brushes.Red;
		public static Brush allowFillingBrush = new SolidColorBrush(Color.FromArgb(30, 0, 150, 0));
		public static Brush allowLinesBrush = Brushes.Green;

		#endregion

		#region Private

		/// <summary>
		///     Класс, хранящий настройки
		/// </summary>
		private static SettingsFields settFields;

		#endregion

		#endregion

		#region Fields

		#region Public

		public bool CheckThreshISUnchecked;

		/// <summary>
		///     Токен отмены операции детектирования.
		/// </summary>
		public CancellationTokenSource tokenSource;

		#endregion

		#region Private

		private readonly SettingsSaver settingsSaver;

		/// <summary>
		///     флаг, сообщающий что считывание идёт с камеры
		/// </summary>
		private bool camM;

		/// <summary>
		///     Путь к файлу
		/// </summary>
		private string filePath = "";

		/// <summary>
		///     Лист для хранения плоскостей, обозначающих зоны детекта
		/// </summary>
		private readonly List<Plane> planeList = new List<Plane>();

		/// <summary>
		///     Поток для работы обработчика видео
		/// </summary>
		private Thread th;

		#endregion

		#endregion

		#region .ctor

		/// <summary>
		///     Конструктор
		/// </summary>
		public MainWindow()
		{
			InitializeComponent();
			settingsSaver = new SettingsSaver();
			Stop.IsEnabled = false;
			SuperInter.Visibility = Visibility.Hidden;
			SuperOptions.Visibility = Visibility.Hidden;
			myCanvas.Background = new SolidColorBrush(Color.FromArgb(0, 0, 150, 0));
			settFields = new SettingsFields();

			Closing += (sender, e) => SilentSaveSettings();

			var settFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");

			if (!File.Exists(settFilePath))
			{
				SilentSaveSettings();
			}
			else
			{
				Task.Run(async () =>
				{
					await Task.Delay(500);
					Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
					{
						try
						{
							SilentReadSettings();
						}
						catch (IOException exception)
						{
							MessageBox.Show($"Не удалось загрузить сохраненные настройки приложения {Environment.NewLine}Файл: {Path.GetFileName(exception.Source)}");
						}
					}));
				});
			}
		}

		#endregion

		#region Public methods

		/// <summary>
		///     Переподключение (к USB). При неудаче Выбор камеры (CameraChanger_Click)
		/// </summary>
		public void MakeReconnect()
		{
			var f = new CameraSelectWindow();
			f.ShowBusyBox();
			var ff = f.imShown;

			Task.Run(() =>
			{
				var success = false;
				while (ff)
				{
					VideoCapture v;
					if (cameraMode == StreamSrc.USB_cam)
					{
						v = VideoCapture.FromCamera(CaptureDevice.Any);
					}
					else
					{
						v = VideoCapture.FromFile(filePath);
					}

					var r = new Mat();
					v.Read(r);

					if (r.Empty())
					{
						Thread.Sleep(1000);
					}
					else
					{
						Application.Current.Dispatcher.BeginInvoke(new Action(() => Restart(f)));
						success = true;
						r.Dispose();
						break;
					}

					r.Dispose();
					Application.Current.Dispatcher.BeginInvoke(new Action(() => ff = f.imShown));
				}

				if (!success)
				{
					Application.Current.Dispatcher.BeginInvoke(new Action(() => PerformAct(CameraChanger_Click)));
				}
			});
		}

		/// <summary>
		///     Ошибка.Создаёт файл в папке logs проекта с временем ошибки и её данными
		/// </summary>
		/// <param name="err">Объект ошибки</param>
		/// <param name="echoOff">0 - вывод MessageBox, 1 - Нет</param>
		public static void WriteLogs(Exception err, bool echoOff = false)
		{
			if (!echoOff)
			{
				MessageBox.Show($"В процессе детектирования произошла ошибка {err.GetType()}. {Environment.NewLine} Лог ошибки сохранен в папку logs");
			}

			if (!Directory.Exists(@"logs"))
			{
				Directory.CreateDirectory(@"logs");
			}

			var sw = new StreamWriter($@"logs\{DateTime.Now.ToShortDateString()}_{DateTime.Now.ToLongTimeString()}.txt".Replace(":", "_"));
			sw.Write(err.ToString());
			sw.Close();
		}

		public static void PerformAct(Action<object, RoutedEventArgs> act) => act(new object(), new RoutedEventArgs());

		#endregion

		#region Private methods

		/// <summary>
		///     Обработчик изменения размеров окна
		/// </summary>
		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			foreach (var plane in planeList)
			{
				plane.Resize();
			}
		}

		/// <summary>
		///     Открывает диалог выбора файла
		/// </summary>
		private void OpenFile_Click(object sender, RoutedEventArgs e)
		{
			Stop_Click(sender, e);
			var openFileDialog1 = new OpenFileDialog();
			openFileDialog1.Filter = "Файлы видео (*.mp4, *.cva)|*.mp4;*.cva";

			if (openFileDialog1.ShowDialog() == true)
			{
				filePath = openFileDialog1.FileName;
				GetFirstFrame();
				labelCurState.Content = "Чтение видео-файла";
			}
		}

		/// <summary>
		///     Загружает первый кадр в imageBox
		/// </summary>
		private void GetFirstFrame()
		{
			var v = VideoCapture.FromFile(filePath);
			var r = new Mat();
			v.Read(r);
			myImage.Source = WriteableBitmapConverter.ToWriteableBitmap(r);

			r.Dispose();
			v.Dispose();
		}

		/// <summary>
		///     Создёт поток для нейронки
		/// </summary>
		private void Start_Click(object sender, RoutedEventArgs e)
		{
			var plane = planeList.FirstOrDefault();
			if (plane == null)
			{
				MessageBox.Show("Необходима хотя бы одна зона детектирования!");
				return;
			}

			if (filePath == "" && cameraMode != StreamSrc.USB_cam)
			{
				MessageBox.Show("Сперва выберите файл или камеру");
				return;
			}

			th = new Thread(ParsingVideoWork);
			th.IsBackground = true;
			th.Start();
			Start.IsEnabled = false;
			Stop.IsEnabled = true;
		}

		/// <summary>
		///     Функция обработки кадров
		/// </summary>
		private void ParsingVideoWork()
		{
			tokenSource = new CancellationTokenSource();

			var parseVideo = new FrameAnalyzer(this, filePath, planeList.FirstOrDefault(), tokenSource.Token, camM);
			try
			{
				parseVideo.Start();
			}
			catch (Exception err)
			{
				WriteLogs(err);
			}
		}

		/// <summary>
		///     Открывает выбор камер
		/// </summary>
		private void CameraChanger_Click(object sender, RoutedEventArgs e)
		{
			if (Start.IsEnabled == false)
			{
				Stop_Click(new object(), new RoutedEventArgs());
			}

			if (cameraMode == StreamSrc.Video)
			{
				var f = new CameraSelectWindow().GetCam();
				if (f == "")
				{
					return;
				}

				try
				{
					VideoCapture v;
					if (f == "USB")
					{
						cameraMode = StreamSrc.USB_cam;
						v = VideoCapture.FromCamera(CaptureDevice.Any);
						filePath = "0";
						camM = true;
					}
					else
					{
						cameraMode = StreamSrc.IP_cam;
						filePath = f;
						v = VideoCapture.FromFile(filePath);
						camM = true;
					}

					var r = new Mat();
					v.Read(r);
					myImage.Source = WriteableBitmapConverter.ToWriteableBitmap(r);
					r.Dispose();
					v.Dispose();

					CameraChanger.Content = "Режим камеры активирован";
					CameraChanger.Background = Brushes.Green;

					labelCurState.Content = "Получение потока с камеры";
				}
				catch
				{
					MessageBox.Show("Камера недоступна");
					cameraMode = StreamSrc.Video;
				}
			}
			else
			{
				labelCurState.Content = "Чтение видео-файла";
				CameraChanger.Content = "Выбор камеры";
				CameraChanger.Background = Brushes.LightGray;
				cameraMode = StreamSrc.Video;
			}
		}

		/// <summary>
		///     Останавливает поток с нейронкой
		/// </summary>
		private void Stop_Click(object sender, RoutedEventArgs e)
		{
			if (th != null)
			{
				tokenSource.Cancel();
				tokenSource.Dispose();
				Start.IsEnabled = true;
				Stop.IsEnabled = false;
				th = null;
			}
		}

		/// <summary>
		///     Добавляет новую плоскость
		/// </summary>
		private void AddPlane_Click(object sender, RoutedEventArgs e)
		{
			if (planeList.Count > 0)
			{
				return;
			}

			planeList.Add(new Plane(myCanvas, this));
			planeCountLabel.Content = "Зон обнаружения: " + planeList.Count;
		}

		/// <summary>
		///     Удаляет последнюю добавленную плоскость
		/// </summary>
		private void RemovePlane_Click(object sender, RoutedEventArgs e)
		{
			if (planeList.Count > 0)
			{
				planeList[planeList.Count - 1].remove();
				planeList.RemoveAt(planeList.Count - 1);
				planeCountLabel.Content = "Зон обнаружения: " + planeList.Count;
			}
		}

		/// <summary>
		///     Вызывает нажатие кнопок Stop и Start
		/// </summary>
		/// <param name="f">CameraSelectWindow который надо закрыть</param>
		private void Restart(CameraSelectWindow f = null)
		{
			f?.Close();
			Stop_Click(new object(), new RoutedEventArgs());
			Thread.Sleep(1000);
			Start_Click(new object(), new RoutedEventArgs());
		}

		private void Window_Closing(object sender, CancelEventArgs e)
		{
			th?.Abort();
			Process.GetCurrentProcess().Close();
		}

		/// <summary>  Обработка нажатия кнопки "Сохранение настроек"</summary>
		private void btnDESerial_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				ReadSettings();
			}
			catch (IOException exception)
			{
				MessageBox.Show("Не удалось прочитать файл настроек " + exception.Source);
			}

		}

		/// <summary>  Обработка нажатия кнопки "Восстановление настроек"</summary>
		private void btnSerial_Click(object sender, RoutedEventArgs e) => SaveSettings();

		/// <summary>Обрабатывает событие Unchecked элемента управления CheckThresh.</summary>
		private void CheckThresh_Unchecked(object sender, RoutedEventArgs e) => CheckThreshISUnchecked = true;

		/// <summary>Обрабатывает событие Checked элемента управления CheckSuperOptions.</summary>
		private void CheckSuperOptions_Checked(object sender, RoutedEventArgs e)
		{
			SuperInter.Visibility = Visibility.Visible;
			SuperOptions.Visibility = Visibility.Visible;
		}

		/// <summary>Обрабатывает событие Unchecked элемента управления CheckSuperOptions.</summary>
		private void CheckSuperOptions_Unchecked(object sender, RoutedEventArgs e)
		{
			SuperInter.Visibility = Visibility.Hidden;
			SuperOptions.Visibility = Visibility.Hidden;
		}

		/// <summary>
		/// Чтение настроек
		/// </summary>
		private void ReadSettings()
		{
			settFields = settingsSaver.ReadSettings(settFields);
			SetSettingsToUI();
		}

		/// <summary>
		/// Восстановление настроек
		/// </summary>
		private void SilentReadSettings()
		{
			settFields = settingsSaver.SilentReadSettings();
			SetSettingsToUI();
		}

		/// <summary>
		///  Сохранение настроек
		///  (по нажатию кнопки)
		/// </summary>
		private void SaveSettings()
		{
			GetSettingsFromUI();

			var fd = new SaveFileDialog();
			fd.FileName = "settings";
			fd.DefaultExt = ".xml";

			var result = fd.ShowDialog();
			if (result == true)
			{
				var newSettings = settFields.Clone();
				settFields = settingsSaver.WriteSettings(newSettings, fd.FileName);
			}
		}


		/// <summary>
		/// Выполняет фоновое сохранение настроек.
		/// </summary>
		private void SilentSaveSettings()
		{
			GetSettingsFromUI();
			settingsSaver.WriteSettings(settFields);
		}

		/// <summary>
		///  Устанавливает текущие настройки в UI
		/// </summary>
		private void SetSettingsToUI()
		{
			txtUpdatePeriod.Text = settFields.updateInterval.ToString();
			WaitingTimeEdge.Text = settFields.timeBeforeFailure.ToString();
			binarizationSlider.Value = settFields.binarizThreshold;
			detectionSlider.Value = settFields.detectionEdge;

			planeList.ForEach(x => x.remove());
			planeList.Clear();
			foreach (var i in settFields.dots)
			{
				planeList.Add(new Plane(myCanvas, this, i));
			}

			planeCountLabel.Content = "Зон обнаружения: " + planeList.Count;
		}

		/// <summary>
		/// Обновляет сохраненные значения настроек в соответствии с параметрами на UI.
		/// </summary>
		private void GetSettingsFromUI()
		{
			settFields.updateInterval = Convert.ToInt32(txtUpdatePeriod.Text);
			settFields.timeBeforeFailure = Convert.ToInt32(WaitingTimeEdge.Text);
			settFields.binarizThreshold = binarizationSlider.Value;
			settFields.detectionEdge = detectionSlider.Value;
			settFields.dots = planeList.Select(plane => plane.dots.Select(x => x.relativeCord).ToArray()).ToList();
		}

		#endregion
	}
}