﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

using FlexiblePlanes;

using Microsoft.Win32;

using Brushes = System.Windows.Media.Brushes;

namespace ActionDetector
{
	/// <summary>
	/// Логика взаимодействия для MainWindow.xaml
	/// </summary>
	public partial class MainWindow : System.Windows.Window
	{
		private readonly SettingsSaver settingsSaver;

		public enum StreamSrc { USB_cam, Video, IP_cam}
		public enum ZoneMode { Allowing_zone, Forbidding_zone}

		/// <summary>
		/// Лист для хранения плоскостей, обозначающих зоны детекта
		/// </summary>
		List<Plane> planeList = new List<Plane>();
		/// <summary>
		/// Путь к файлу
		/// </summary>
		string filePath = "";
		/// <summary>
		/// Поток для работы обработчика видео
		/// </summary>
		Thread th;

        /// <summary>
        /// флаг, сообщающий что считывание идёт с камеры
        /// </summary>
        bool camM = false;

		/// <summary>
		/// Токен отмены операции детектирования.
		/// </summary>
		public CancellationTokenSource tokenSource;

		/// <summary>
		/// Режим работы с видеопотоком
		/// </summary>
		public static StreamSrc cameraMode = StreamSrc.Video;
		/// <summary>
		/// Режим работы выделенной зоны
		/// </summary>
		public static ZoneMode zoneMode = ZoneMode.Forbidding_zone;

        /// <summary>
        /// Класс, хранящий настройки
        /// </summary>
        static SettingsFields settFields;

        public static System.Windows.Media.Brush forbidFillingBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 150, 0, 0));
		public static System.Windows.Media.Brush forbidLinesBrush = Brushes.Red;

		public static System.Windows.Media.Brush allowFillingBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 0, 150, 0));
		public static System.Windows.Media.Brush allowLinesBrush = Brushes.Green;

		/// <summary>
		/// Конструктор
		/// </summary>  
		public MainWindow()
		{
			InitializeComponent();
			settingsSaver = new SettingsSaver();
			Stop.IsEnabled = false;
            SuperInter.Visibility = Visibility.Hidden;
            SuperOptions.Visibility = Visibility.Hidden;
			myCanvas.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0, 0, 150, 0));
            settFields = new SettingsFields();

			Closing += (sender, e) => SilentSaveSettings();

            string settFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");

            if (!File.Exists(settFilePath))
			{
				SilentSaveSettings();
			}
            else
            {
                Task.Run(async () =>
                {
                    await Task.Delay(500);
                    System.Windows.Application.Current?.Dispatcher?.BeginInvoke( new Action(() => { ReadSettings(true); }));
                });               
            }
		}
		/// <summary>
		/// Обработчик изменения размеров окна
		/// </summary>
		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			foreach (var plane in planeList)
			{
				plane.Resize();
			}
		}
		/// <summary>
		/// Открывает диалог выбора файла
		/// </summary>
		private void OpenFile_Click(object sender, RoutedEventArgs e)
		{
			Stop_Click( sender,  e);
			Microsoft.Win32.OpenFileDialog openFileDialog1 = new Microsoft.Win32.OpenFileDialog();
			openFileDialog1.Filter = "Файлы видео (*.mp4, *.cva)|*.mp4;*.cva";

			if (openFileDialog1.ShowDialog() == true)
			{
				filePath = openFileDialog1.FileName;
				GetFirstFrame();
				labelCurState.Content = "Чтение видео-файла";
			}
		}

		/// <summary>
		/// Загружает первый кадр в imageBox
		/// </summary>
		private void GetFirstFrame()
		{
			var v = OpenCvSharp.VideoCapture.FromFile(filePath);
			var r = new OpenCvSharp.Mat();
			v.Read(r);
			myImage.Source = OpenCvSharp.Extensions.WriteableBitmapConverter.ToWriteableBitmap(r);

			r.Dispose();
			v.Dispose();
		}

		/// <summary>
		/// Создёт поток для нейронки
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
		/// Функция обработки кадров
		/// </summary>
		private void ParsingVideoWork()
		{	
			tokenSource = new CancellationTokenSource();

			FrameAnalyzer parseVideo = new FrameAnalyzer(this, filePath, planeList.FirstOrDefault(), tokenSource.Token, camM);
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
		/// Открывает выбор камер
		/// </summary>
		private void CameraChanger_Click(object sender, RoutedEventArgs e)
		{
			if (Start.IsEnabled == false)
				Stop_Click(new object(), new RoutedEventArgs());

			if (cameraMode == StreamSrc.Video)
			{
				var f = new CameraSelectWindow().GetCam();
				if (f == "") return;

				try
				{
					OpenCvSharp.VideoCapture v;
					if (f == "USB")
					{
						cameraMode = StreamSrc.USB_cam;
						v = OpenCvSharp.VideoCapture.FromCamera(OpenCvSharp.CaptureDevice.Any);
                        filePath = "0";
                        camM = true;
					}
					else
					{
						cameraMode = StreamSrc.IP_cam;
						filePath = f;
						v = OpenCvSharp.VideoCapture.FromFile(filePath);
                        camM = true;
                    }

                    var r = new OpenCvSharp.Mat();
					v.Read(r);
					myImage.Source = OpenCvSharp.Extensions.WriteableBitmapConverter.ToWriteableBitmap(r);
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
		/// Останавливает поток с нейронкой
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
		/// Добавляет новую плоскость
		/// </summary>
		private void AddPlane_Click(object sender, RoutedEventArgs e)
		{
			if (planeList.Count > 0)
				return;
			planeList.Add(new Plane(myCanvas, this));
			planeCountLabel.Content = "Зон обнаружения: " + planeList.Count;
		}

		/// <summary>
		/// Удаляет последнюю добавленную плоскость
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
		/// Переподключение (к USB). При неудаче Выбор камеры (CameraChanger_Click)
		/// </summary>
		public void MakeReconnect()
		{
			var f = new CameraSelectWindow();
			f.ShowBusyBox();
			bool ff = f.imShown;

			Task.Run(() => {
				bool success = false;
				while (ff == true)
				{
					OpenCvSharp.VideoCapture v;
					if (cameraMode == StreamSrc.USB_cam)
						v = OpenCvSharp.VideoCapture.FromCamera(OpenCvSharp.CaptureDevice.Any);
					else 
						v = OpenCvSharp.VideoCapture.FromFile(filePath);

					OpenCvSharp.Mat r = new OpenCvSharp.Mat();
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
		/// Вызывает нажатие кнопок Stop и Start
		/// </summary>
		/// <param name="f">CameraSelectWindow который надо закрыть</param>
		void Restart(CameraSelectWindow f = null)
		{
			f?.Close();
			Stop_Click(new object(), new RoutedEventArgs());
			Thread.Sleep(1000);
			Start_Click(new object(), new RoutedEventArgs());
		}
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
				th?.Abort();
				System.Diagnostics.Process.GetCurrentProcess().Close();
		}

		/// <summary>
		/// Ошибка.Создаёт файл в папке logs проекта с временем ошибки и её данными
		/// </summary>
		/// <param name="err">Объект ошибки</param>
		/// <param name="echoOff">0 - вывод MessageBox, 1 - Нет</param>
		public static void WriteLogs(Exception err, bool echoOff = false)
		{
			if (!echoOff)
				MessageBox.Show($"В процессе детектирования произошла ошибка {err.GetType().ToString()}. {Environment.NewLine} Лог ошибки сохранен в папку logs");
			if (!Directory.Exists(@"logs"))
				Directory.CreateDirectory(@"logs");

			var sw = new StreamWriter($@"logs\{DateTime.Now.ToShortDateString()}_{DateTime.Now.ToLongTimeString()}.txt".Replace(":", "_"));
			sw.Write(err.ToString());
			sw.Close();
		}
		
		public static void PerformAct(Action<object, RoutedEventArgs> act)
		{
			act(new object(), new RoutedEventArgs());
		}

		private void ForbidRadBut_Checked(object sender, RoutedEventArgs e)
		{
			zoneMode = ZoneMode.Forbidding_zone;
			SetZoneMode();
		}

        private void AllowRadBut_Checked(object sender, RoutedEventArgs e)
		{
			zoneMode = ZoneMode.Allowing_zone;
			SetZoneMode();
		}
	   
		/// <summary>
		///Установка кистей и линий для плоскостей planeList
		/// </summary>
		private void SetZoneMode()
		{
			if (zoneMode == ZoneMode.Forbidding_zone)
			{
				foreach (var i in planeList)
					i.SetBrushes(forbidFillingBrush, forbidLinesBrush);
			}
			else
			{
				foreach (var i in planeList)
					i.SetBrushes(allowFillingBrush, allowLinesBrush);
			}
		}

        /// <summary>  Обработка нажатия кнопки "Сохранение настроек"</summary>
        private void btnDESerial_Click(object sender, RoutedEventArgs e)
		{			
            ReadSettings();         
        }

        /// <summary>  Обработка нажатия кнопки "Восстановление настроек"</summary>
		private void btnSerial_Click(object sender, RoutedEventArgs e)
		{
            SaveSettings();
        }

        /// <summary>Обрабатывает событие ValueChanged элемента управления binarizationSlider.</summary>
        private void binarizationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			binarizationLabel.Content = "Порог бинаризации: " + (int)binarizationSlider.Value;
		}
        public bool CheckThreshISUnchecked = false;
        /// <summary>Обрабатывает событие Unchecked элемента управления CheckThresh.</summary>>
        private void CheckThresh_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckThreshISUnchecked = true;
        }

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
        /// Восстановление настроек
        /// </summary>
        private void ReadSettings(bool silent = false)
        {
			settFields = !silent
				? settingsSaver.ReadSettings(settFields) 
				: settingsSaver.SilentReadSettings();

			txtUpdatePeriod.Text = settFields.updateInterval.ToString();
            WaitingTimeEdge.Text = settFields.timeBeforeFailure.ToString();
            binarizationSlider.Value = settFields.binarizThreshold;

			planeList.ForEach(x => x.remove());
			planeList.Clear();
			foreach (var i in settFields.dots)
            {
				planeList.Add(new Plane(myCanvas, this, i));
            }
            planeCountLabel.Content = "Зон обнаружения: " + planeList.Count;


            if (settFields.showThreshImg)
                checkThresh.IsChecked = true;
            else checkThresh.IsChecked = false;

            if (settFields.showAdvSettings)
                checkSuperOptions.IsChecked = true;
            else checkSuperOptions.IsChecked = false;
        }

        /// <summary>Сохранение настроек
        /// (по нажатию кнопки)</summary>
        private void SaveSettings()
        {      
            settFields.updateInterval = Convert.ToInt32(txtUpdatePeriod.Text);
            settFields.timeBeforeFailure = Convert.ToInt32(WaitingTimeEdge.Text);
            settFields.binarizThreshold = binarizationSlider.Value;

            if (checkThresh.IsChecked == true)
                settFields.showThreshImg = true;
            else settFields.showThreshImg = false;

            if (checkSuperOptions.IsChecked == true)
                settFields.showAdvSettings = true;
            else settFields.showAdvSettings = false;

            Microsoft.Win32.SaveFileDialog fd = new Microsoft.Win32.SaveFileDialog();
            fd.FileName = "settings";
            fd.DefaultExt = ".xml";

			settFields.dots = planeList.Select(plane => plane.dots.Select(x => x.relativeCord).ToArray()).ToList();

            var result = fd.ShowDialog();
            if (result == true)
			{
				var newSettings = settFields.Clone();
				settFields = settingsSaver.WriteSettings(newSettings, fd.FileName);
            }
        }

		private void SilentSaveSettings()
		{
			settFields.dots = planeList.Select(plane => plane.dots.Select(x => x.relativeCord).ToArray()).ToList();
			settingsSaver.WriteSettings(settFields);
		}
	}
}