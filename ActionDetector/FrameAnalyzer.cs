using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using FlexiblePlanes;

using OpenCvSharp;

using Point = System.Windows.Point;
using Rect = OpenCvSharp.Rect;
using Size = OpenCvSharp.Size;

namespace ActionDetector
{
	internal class FrameAnalyzer : IDisposable
	{
		#region Fields

		#region Private

		//Счётчик кол-ва простоев
		private int alarmCount;

		///<summary>Нижняя граница трешхолда</summary>
		private int binaryEdge;
		/// <summary>
		/// Порог детектирования объекта (в % от площади кадра)
		/// </summary>
		private double detectionEdge;

		private readonly bool cameraMode;
		private DateTime curFailure; //время текущего простоя

		///<summary>Матрица с текущим кадром</summary>
		private readonly Mat curFrame = new Mat();

		//время простоя стана
		private double failureTimeMin;
		private Mat lastFrame;
		private DateTime lastTime;
		private readonly string logName; //Файл с логами
		private readonly MainWindow mw;

		///<summary>Ссылка на главный PictureBox</summary>
		private readonly Plane plane;

		///<summary>Матрица с предыдущим кадром</summary>
		private Mat prevFrame = new Mat();

		private int refrPerc = 5; //Период обновления процентов

		///<summary>Главный таймер, отвечающий за своевременное получение и обработку кадров</summary>
		private Timer timer;

		///<summary>Временная матрица с измененным кадром</summary>
		private readonly Mat tmpFrame = new Mat();

		private CancellationToken token;

		///<summary>Видеопоток</summary>
		private readonly VideoCapture videoStream;

		/// <summary>
		///     Время простоя
		/// </summary>
		private int waitingEdge = 60000;

		//время работы стана
		private double workTimeMin;

		#endregion

		#endregion

		#region .ctor

		public FrameAnalyzer(MainWindow mWw, string filePath, Plane pictureBox, CancellationToken TT, bool camM = false)
		{
			mw = mWw;
			token = TT;
			//Инициализуем все ссылочные переменные
			int a;
			if (int.TryParse(filePath, out a))
			{
				videoStream = VideoCapture.FromCamera(CaptureDevice.Any);
			}
			else
			{
				videoStream = VideoCapture.FromFile(filePath);
			}

			plane = pictureBox;
			lastTime = DateTime.Now;
			cameraMode = camM;

			logName = $@"{DateTime.Now.ToShortDateString()} {DateTime.Now.ToLongTimeString()}.txt".Replace(":", ".");

			WriteStateLogs("Начало работы", logName);
			WriteStateLogsInDB(); //Для записи в БД
		}

		#endregion

		#region Implementation of IDisposable

		public void Dispose() => Interupt();

		#endregion

		#region Public methods

		public void Start()
		{
			alarmCount = 0;
			//флаг наличия движение желтого объекта на стане в области интереса
			var grannyInTheROI = false;
			///<summary>Матрица с областью интереса текущего кадра</summary>
			Mat curRoi;

			//флаг наличия простоя
			var failure = false;

			//переменная, хранящая время начала простоя
			curFailure = DateTime.Now;

			var frameSize = new Size(videoStream.FrameWidth, videoStream.FrameHeight);
			var tmprect = CreateBoundBox(frameSize, plane.dots.ToArray());

			var lines = new float[plane.dots.Count][]; // это координаты ROI

			for (var i = 0; i < plane.dots.Count; i++)
			{
				var index1 = (i + 2) % plane.dots.Count;
				var index2 = (i + 3) % plane.dots.Count;

				lines[i] = CalcABC((int) (plane.dots[index1].relativeCord.X * frameSize.Width - tmprect.X),
								   (int) (plane.dots[index1].relativeCord.Y * frameSize.Height - tmprect.Y),
								   (int) (plane.dots[index2].relativeCord.X * frameSize.Width - tmprect.X),
								   (int) (plane.dots[index2].relativeCord.Y * frameSize.Height - tmprect.Y));
			}

			//Время для отслеживания простоя
			var time = DateTime.Now;
			//время для вывода информации о простое и работе в процентах
			var startTime = DateTime.Now;
			//время работы программы
			var totalTime = DateTime.Now;

			int frameDelta;

			if (!cameraMode)
			{
				frameDelta = (int) (1000 / videoStream.Fps);
			}
			else
			{
				frameDelta = 30;
			}

			videoStream.Read(curFrame);

			timer = new Timer(state =>
			{
				videoStream.Read(curFrame);
				var empty = curFrame.Empty();

				if (token.IsCancellationRequested || empty)
				{
					Extensions.BeginInvoke(() =>
					{
						mw.txtBlockAlarm.Visibility = Visibility.Hidden;
					});
					timer.Dispose();
					if (MainWindow.cameraMode != MainWindow.StreamSrc.Video && !token.IsCancellationRequested)
					{
						new Action(mw.MakeReconnect).MakeActInMainThread();
					}

					return;
				}

				//Считывание порога бинаризации и времени простоя
				Extensions.BeginInvoke(() =>
				{
					binaryEdge = (int) mw.binarizationSlider.Value;
					detectionEdge = (int)mw.detectionSlider.Value;

					if (int.TryParse(mw.WaitingTimeEdge.Text, out var a))
					{
						waitingEdge = a;
					}

					if (int.TryParse(mw.txtUpdatePeriod.Text, out var b))
					{
						refrPerc = b;
					}
				});

				//Если желтый объект на стане отсутствовал дольше допустимого времени и информационная надпись о простое скрыта
				if (mw.txtBlockAlarm.Visibility == Visibility.Hidden && DateTime.Now.Subtract(time).TotalSeconds > waitingEdge)
				{
					failure = true;

					curFailure = DateTime.Now;

					workTimeMin += DateTime.Now.Subtract(time).TotalMinutes; //В минутах потому, что это не слишком мало и не слишком много, переполнения не будет дольше.
					time = DateTime.Now;

					alarmCount++;

					//вывод надписи о простое и показ кол-ва простоев
					Extensions.BeginInvoke(() =>
					{
						mw.labelCountAlarm.Content = $"Количество простоев: {alarmCount}";
						mw.txtBlockAlarm.Visibility = Visibility.Visible;

						WriteStateLogs("Начало простоя", logName);
						WriteStateLogsInDB(); //Для записи в БД

						SaveImg(curFailure, curFrame);
						SaveImgInDB(); //Для записи в БД
					});
				}

				//Вывод информации о простое/работе через определённый интервал
				if (DateTime.Now.Subtract(startTime).TotalSeconds >= refrPerc)
				{
					Extensions.BeginInvoke(() =>
					{
						if (failure)
						{
							failureTimeMin += DateTime.Now.Subtract(startTime).TotalMinutes;
							startTime = DateTime.Now;
						}

						var tuple = TimeCount(workTimeMin, failureTimeMin);
						mw.LABLEZ.Content = $"Время работы: {tuple.workT}%, простоя: {tuple.failT}%, всего: {DateTime.Now.Subtract(totalTime).ToString("hh'.'mm'.'ss")}";
						startTime = DateTime.Now;
					});
				}

				//Если кадры изменились в потоке
				if (curFrame != prevFrame)
				{
					curRoi = curFrame[tmprect];

					// если МАТ для предыдущего файла пуст - копируем в него текущий
					if (lastFrame == null)
					{
						lastFrame = curRoi.Clone();
					}

					Extensions.BeginInvoke(() => mw.myImage.Source = curFrame.Clone().ToImage());

					//для увеличения производительности решено брать не каждый кадр
					if ((DateTime.Now - lastTime).TotalMilliseconds > 500)
					{
						try
						{
							Task.Run(() => Algorhitm(ref curRoi, ref time, lines, ref grannyInTheROI, ref failure));
						}
						catch
						{
						}

						;
					}
				}

				prevFrame.Dispose();
				//Делаем предыдущий кадр равным нынешнему
				prevFrame = curFrame.Clone();

				//Обязательно собираем мусор
				GC.Collect();
			}, null, 0, frameDelta);
		}

		/// <summary>Создаёт Rect-рамку ROI</summary>
		/// <param name="frameSize">Размер кадра</param>
		/// <param name="args">Аргументы</param>
		/// <returns></returns>
		public Rect CreateBoundBox(Size frameSize, Dot[] args)
		{
			//q[0] - x, 1 - y, 2 - Ширина, 3 - Высота
			var q = new int[4];
			q[0] = 99999999;
			q[1] = 99999999;
			foreach (var i in args)
			{
				var x = i.relativeCord.X * frameSize.Width;
				if (x < q[0])
				{
					q[0] = (int) x;
				}

				var y = i.relativeCord.Y * frameSize.Height;
				if (y < q[1])
				{
					q[1] = (int) y;
				}

				var w = i.relativeCord.X * frameSize.Width;
				if (w > q[2])
				{
					q[2] = (int) w;
				}

				var h = i.relativeCord.Y * frameSize.Height;
				if (h > q[3])
				{
					q[3] = (int) h;
				}
			}

			return new Rect(q[0], q[1], q[2] - q[0], q[3] - q[1]);
		}

		///<summary>Функция выгрузки класса из памяти</summary>
		public void Interupt()
		{
			videoStream.Dispose();
			timer.Dispose();
			curFrame.Dispose();
			prevFrame.Dispose();
			tmpFrame.Dispose();
		}

		/// <summary>Checks the dot.</summary>
		/// <param name="x">Координата Х точки</param>
		/// <param name="y">Координата У точки</param>
		/// <param name="q">Постоянные А, В, С уравнения прямой на плоскости для этой точки</param>
		/// <returns></returns>
		public float CheckDot(float x, float y, float[] q) => q[0] * x + q[1] * y + q[2];

		/// <summary>Находит минимальную по длине линию из двух переданных</summary>
		/// <param name="line1"> Массив точек первой линии</param>
		/// <param name="line2"> Массив точек втрой линии</param>
		/// <returns>Длина минимальной из двух линий</returns>
		public double DoSmallestLine(Point[] line1, Point[] line2)
		{
			var line1Length = DoLineLength(line1[0].X, line1[0].Y, line1[1].X, line1[1].Y);
			var line2Length = DoLineLength(line2[0].X, line2[0].Y, line2[1].X, line2[1].Y);

			if (line1Length > line2Length)
			{
				return line2Length;
			}

			return line1Length;
		}

		/// <summary>Считает длину отрезка по кординатам точки начала и точки конца</summary>
		/// <param name="x1">Координата X точки начала отрезка</param>
		/// <param name="y1">Координата Y точки начала отрезка</param>
		/// <param name="x2">Координата X точки конца отрезка</param>
		/// <param name="y2">Координата Y точки конца отрезка</param>
		/// <returns>Длина отрезка</returns>
		public double DoLineLength(double x1, double y1, double x2, double y2) => Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));

		/// <summary>Расчёт времени работы и простоя в процентах от прошедшего</summary>
		/// <param name="workTime">Суммарное время работы стана</param>
		/// <param name="failureTime">Суммарное время простоя стана</param>
		/// <returns>Кортеж со значениями времени работы и простоя в процентах от всего времени работы</returns>
		public (double workT, double failT) TimeCount(double workTime, double failureTime)
		{
			var totalTime = workTime + failureTime;
			if (Math.Abs(totalTime) > 0.001) //Fix floating point comparison
			{
				workTime /= totalTime / 100;
				failureTime /= totalTime / 100;
			}
			else
			{
				workTime = 0;
				failureTime = 0;
			}

			var tuple = (Math.Round(workTime, 1), Math.Round(failureTime, 1)); /*new double[2] { Math.Round(workTime, 1), Math.Round(failureTime, 1) }*/
			return tuple;
		}

		/// <summary>
		/// Записывает сообщение по пути Записи о простоях\[дата время начала работы програмы].txt в формате [дата время] сообщение
		/// </summary>
		/// <param name="state">Сообщение</param>
		/// <param name="logName\">Имя файла лога</param>
		public static void WriteStateLogs(string state, string logName)
		{
			var dirName = "Записи о простоях";
			if (!Directory.Exists(dirName))
			{
				Directory.CreateDirectory(dirName);
			}

			state = $"[{DateTime.Now.ToShortDateString()} {DateTime.Now.ToLongTimeString()}]" + state;

			var sw = new StreamWriter(dirName + $@"\{logName}", true);
			sw.WriteLine(state);
			sw.Close();
		}

		/// <summary>
		///     Метод для сохранения лога работы программы в БД
		/// </summary>
		public static void WriteStateLogsInDB()
		{
		}

		/// <summary>
		///     Сохранение первого кадра при возникновении простоя
		/// </summary>
		/// <param name="time">Время начала простоя</param>
		/// <param name="curFrame">Кадр с простоем</param>
		public static void SaveImg(DateTime time, Mat curFrame, bool downtimeEnd = false)
		{
			var folderName = "Сохраненные кадры работы";

			if (!Directory.Exists(folderName))
			{
				Directory.CreateDirectory(folderName);
			}

			var date = time.ToShortDateString();
			folderName += "\\" + time.ToShortDateString();

			if (!Directory.Exists(folderName))
			{
				Directory.CreateDirectory(folderName);
			}

			var n = new ImageEncodingParam(ImwriteFlags.JpegOptimize, 100);
			var s = time.ToLongTimeString().Replace(':', '.');

			Cv2.ImWrite($"{folderName}\\{s} ({(!downtimeEnd ? "Начало" : "Конец")} простоя).jpeg", curFrame, n);
		}

		/// <summary>
		///     Метод для сохранения картинки в БД
		/// </summary>
		public static void SaveImgInDB()
		{
		}

		#endregion

		#region Private methods

		/// <summary>Algorhitms the specified current roi.</summary>
		/// <param name="curRoi">Матрица с областью интереса текущего кадра</param>
		/// <param name="time">Время для отслеживания простоя</param>
		/// <param name="lines">  Линии ROI</param>
		/// <param name="grannyInTheROI">Флаг наличия жёлтого обьекта на стане в ROI</param>
		/// <param name="failure">  Флаг наличия простоя</param>
		private void Algorhitm(ref Mat curRoi, ref DateTime time, float[][] lines, ref bool grannyInTheROI, ref bool failure)
		{
			try
			{
				var tmp = new Mat();
				//вычитаем из предыдущего кадра текущий 
				Cv2.Subtract(curRoi, lastFrame, tmp);

				//Извлечение канала для улучшения детектинга бабки 
				var ins = tmp.CvtColor(ColorConversionCodes.BGR2GRAY);

				Cv2.Threshold(ins, ins, binaryEdge, 255, ThresholdTypes.Binary); // binaryEdge - С формы бегунок

				//Обрезание ROI по маске
				var mask = CreateMask(ref tmp, lines);
				Cv2.BitwiseAnd(ins, mask, ins);

				//нужен исключительно для демонстрации нарисованного контура выделения бабки, в решении не используется
				var tmpWithCont = tmp.Clone();
				tmp.Dispose();

				if (ins.CountNonZero() > detectionEdge)
				{
					mask.Dispose();
					// остановка времени отсчёта отсутствия бабки  

					if (failure)
					{
						failureTimeMin += DateTime.Now.Subtract(time).TotalMinutes;
						failure = false;

						Extensions.BeginInvoke(() =>
						{
							mw.txtBlockAlarm.Visibility = Visibility.Hidden;
							WriteStateLogs($"Конец простоя длительностью {Math.Round(DateTime.Now.Subtract(curFailure).TotalMinutes, 2)} мин.", logName);

							SaveImg(curFailure + DateTime.Now.Subtract(curFailure), curFrame, true);
							WriteStateLogsInDB(); //Для записи в БД
						});
					}
					else
					{
						workTimeMin += DateTime.Now.Subtract(time).TotalMinutes;
					}

					time = DateTime.Now;

					grannyInTheROI = true;

					//var rect = ToRect(rotRect);
					//tmpWithCont.Rectangle(rect, new Scalar(0, 255, 0), 3);
				}

				else if (grannyInTheROI)
				{
					workTimeMin += DateTime.Now.Subtract(time).TotalMinutes;
					time = DateTime.Now;
					grannyInTheROI = false;
				}

				//поиск контуров на фрейме
				//var contours = ins.FindContoursAsArray(RetrievalModes.External, ContourApproximationModes.ApproxSimple).Where(x => x.Count() > detectionEdge);

				//var count = contours.Count();

				//for (var i = 0; i < count; i++)
				//{
				//	//построение повёрнутого прямоугольника вокруг пятна
				//	//var rotRect = Cv2.MinAreaRect(contours.ElementAt(i));

				//	if (GrannyDetect(contours.ElementAt(i), ref ins, plane.dots))
				//	{
				//		mask.Dispose();
				//		// остановка времени отсчёта отсутствия бабки  

				//		if (failure)
				//		{
				//			failureTimeMin += DateTime.Now.Subtract(time).TotalMinutes;
				//			failure = false;

				//			Extensions.BeginInvoke(() =>
				//			{
				//				mw.txtBlockAlarm.Visibility = Visibility.Hidden;
				//				WriteStateLogs($"Конец простоя длительностью {Math.Round(DateTime.Now.Subtract(curFailure).TotalMinutes, 2)} мин.", logName);

				//				SaveImg(curFailure + DateTime.Now.Subtract(curFailure), curFrame, true);
				//				WriteStateLogsInDB(); //Для записи в БД
				//			});
				//		}
				//		else
				//		{
				//			workTimeMin += DateTime.Now.Subtract(time).TotalMinutes;
				//		}

				//		time = DateTime.Now;

				//		grannyInTheROI = true;

				//		//var rect = ToRect(rotRect);
				//		//tmpWithCont.Rectangle(rect, new Scalar(0, 255, 0), 3);
				//		break;
				//	}

				//	if (grannyInTheROI)
				//	{
				//		workTimeMin += DateTime.Now.Subtract(time).TotalMinutes;
				//		time = DateTime.Now;
				//		grannyInTheROI = false;
				//	}
				//}

				var detectTmp = tmpWithCont.ToImage();
				tmpWithCont.Dispose();
				detectTmp.Freeze();

				//показ трешхолднутого изображения, если понадобится
				Extensions.BeginInvoke(() =>
				{
					if (mw.checkThresh.IsChecked == true)
					{
						Cv2.ImShow("Threshold", ins.Clone());
					}
					else if (!(bool) mw.checkThresh.IsChecked)
					{
						Cv2.DestroyWindow("Threshold");
					}

					mw.myLittleImage.Source = detectTmp;
				});

				lastTime = DateTime.Now;
				lastFrame.Dispose();
				// Делаем предыдущий кадр равным текущему
				lastFrame = curRoi.Clone();
			}
			catch
			{

			}
		}

		/// <summary>Создание маски для отсечения помех</summary>
		/// <param name="roi"> ROI</param>
		/// <param name="lines">  Линии ROI</param>
		/// <returns>Матрица-маска</returns>
		private Mat CreateMask(ref Mat roi, float[][] lines)
		{
			var mask = Mat.Zeros(roi.Size(), MatType.CV_8UC1).ToMat();
			var roiVecInd = mask.GetGenericIndexer<Vec3b>();

			for (var i = 0; i < roi.Height; i++)
			{
				for (var j = 0; j < roi.Width; j++)
				{
					var inside = true;
					//Проходит по всем линиям
					for (var k = 0; k < lines.Length; k++)
					{
						//Проверяет все точки Roi на наличие их в Plane
						var tmp2 = CheckDot(j, i, lines[k]);
						if (tmp2 <= 0)
						{
							inside = false;
							break;
						}
					}

					if (inside)
					{
						roiVecInd[i, j] = new Vec3b(255, 255, 255);
					}
				}
			}

			return mask;
		}

		/// <summary>Повёрнутый прямоугольник в обычный с отслеживанием чтобы он не вышел за границы матрицы</summary>
		/// <param name="s">Преобразуемый RotatedRect</param>
		/// <returns>Rect, преобразованный из RotatedRect</returns>
		private Rect ToRect(RotatedRect s)
		{
			var rect = s.BoundingRect();
			if (rect.X < 0)
			{
				rect.Width += rect.X;
				rect.X = 0;
			}

			if (rect.Y < 0)
			{
				rect.Height += rect.Y;
				rect.Y = 0;
			}

			return rect;
		}

		/// <summary>Расчёт постоянных А, В и С для уравнения прямой на плоскости</summary>
		/// <param name="x1">Координата Х первой точки</param>
		/// <param name="y1">Координата У первой точки</param>
		/// <param name="x2">Координата Х второй точки</param>
		/// <param name="y2">Координата У второй точки</param>
		/// <returns></returns>
		private float[] CalcABC(float x1, float y1, float x2, float y2)
		{
			var q = new float[3];
			q[0] = y1 - y2;
			q[1] = x2 - x1;
			q[2] = x1 * y2 - x2 * y1;
			return q;
		}

		/// <summary>Алгоритм поиска жёлтого объекта на стане в области интереса</summary>
		/// <param name="contour">  Найденный в области интереса контур</param>
		/// <param name="roi">  Матрица области интереса</param>
		/// <param name="dots">  Крайние точки выделенной области</param>
		/// <returns>
		///     true - если контур занимает всю высоту/ширину выделенной области и квадратный, то есть это контур жёлтого объекта,
		///     иначе - false
		/// </returns>
		private bool GrannyDetect(OpenCvSharp.Point[] contour, ref Mat roi, List<Dot> dots)
		{
			var rotRect = Cv2.MinAreaRect(contour);

			var minXPoint = contour[0];
			var minYPoint = contour[0];

			for (var z = 0; z < contour.Count(); z++)
			{
				if (contour[z].X < minXPoint.X)
				{
					minXPoint = contour[z];
				}

				if (contour[z].Y < minYPoint.Y)
				{
					minYPoint = contour[z];
				}
			}

			var rectHeight = DoLineLength(minXPoint.X, minXPoint.Y, minYPoint.X, minYPoint.Y);

			if (roi.Height < roi.Width)
			{
				var roiHeight = DoSmallestLine(new[] { dots[0].absoluteCord, dots[3].absoluteCord }, new[] { dots[1].absoluteCord, dots[2].absoluteCord });
				if (rectHeight >= roiHeight)
				{
					return true;
				}

				return false;
			}

			var roiWidth = DoSmallestLine(new[] { dots[0].absoluteCord, dots[1].absoluteCord }, new[] { dots[2].absoluteCord, dots[3].absoluteCord });
			if (rectHeight >= roi.Width)
			{
				return true;
			}

			return false;
		}

		#endregion
	}
}