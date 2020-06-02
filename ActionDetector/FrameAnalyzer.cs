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

		//������� ���-�� ��������
		private int alarmCount;

		///<summary>������ ������� ���������</summary>
		private int binaryEdge;
		/// <summary>
		/// ����� �������������� ������� (� % �� ������� �����)
		/// </summary>
		private double detectionEdge;

		private readonly bool cameraMode;
		private DateTime curFailure; //����� �������� �������

		///<summary>������� � ������� ������</summary>
		private readonly Mat curFrame = new Mat();

		//����� ������� �����
		private double failureTimeMin;
		private Mat lastFrame;
		private DateTime lastTime;
		private readonly string logName; //���� � ������
		private readonly MainWindow mw;

		///<summary>������ �� ������� PictureBox</summary>
		private readonly Plane plane;

		///<summary>������� � ���������� ������</summary>
		private Mat prevFrame = new Mat();

		private int refrPerc = 5; //������ ���������� ���������

		///<summary>������� ������, ���������� �� ������������� ��������� � ��������� ������</summary>
		private Timer timer;

		///<summary>��������� ������� � ���������� ������</summary>
		private readonly Mat tmpFrame = new Mat();

		private CancellationToken token;

		///<summary>����������</summary>
		private readonly VideoCapture videoStream;

		/// <summary>
		///     ����� �������
		/// </summary>
		private int waitingEdge = 60000;

		//����� ������ �����
		private double workTimeMin;

		#endregion

		#endregion

		#region .ctor

		public FrameAnalyzer(MainWindow mWw, string filePath, Plane pictureBox, CancellationToken TT, bool camM = false)
		{
			mw = mWw;
			token = TT;
			//������������ ��� ��������� ����������
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

			WriteStateLogs("������ ������", logName);
			WriteStateLogsInDB(); //��� ������ � ��
		}

		#endregion

		#region Implementation of IDisposable

		public void Dispose() => Interupt();

		#endregion

		#region Public methods

		public void Start()
		{
			alarmCount = 0;
			//���� ������� �������� ������� ������� �� ����� � ������� ��������
			var grannyInTheROI = false;
			///<summary>������� � �������� �������� �������� �����</summary>
			Mat curRoi;

			//���� ������� �������
			var failure = false;

			//����������, �������� ����� ������ �������
			curFailure = DateTime.Now;

			var frameSize = new Size(videoStream.FrameWidth, videoStream.FrameHeight);
			var tmprect = CreateBoundBox(frameSize, plane.dots.ToArray());

			var lines = new float[plane.dots.Count][]; // ��� ���������� ROI

			for (var i = 0; i < plane.dots.Count; i++)
			{
				var index1 = (i + 2) % plane.dots.Count;
				var index2 = (i + 3) % plane.dots.Count;

				lines[i] = CalcABC((int) (plane.dots[index1].relativeCord.X * frameSize.Width - tmprect.X),
								   (int) (plane.dots[index1].relativeCord.Y * frameSize.Height - tmprect.Y),
								   (int) (plane.dots[index2].relativeCord.X * frameSize.Width - tmprect.X),
								   (int) (plane.dots[index2].relativeCord.Y * frameSize.Height - tmprect.Y));
			}

			//����� ��� ������������ �������
			var time = DateTime.Now;
			//����� ��� ������ ���������� � ������� � ������ � ���������
			var startTime = DateTime.Now;
			//����� ������ ���������
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

				//���������� ������ ����������� � ������� �������
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

				//���� ������ ������ �� ����� ������������ ������ ����������� ������� � �������������� ������� � ������� ������
				if (mw.txtBlockAlarm.Visibility == Visibility.Hidden && DateTime.Now.Subtract(time).TotalSeconds > waitingEdge)
				{
					failure = true;

					curFailure = DateTime.Now;

					workTimeMin += DateTime.Now.Subtract(time).TotalMinutes; //� ������� ������, ��� ��� �� ������� ���� � �� ������� �����, ������������ �� ����� ������.
					time = DateTime.Now;

					alarmCount++;

					//����� ������� � ������� � ����� ���-�� ��������
					Extensions.BeginInvoke(() =>
					{
						mw.labelCountAlarm.Content = $"���������� ��������: {alarmCount}";
						mw.txtBlockAlarm.Visibility = Visibility.Visible;

						WriteStateLogs("������ �������", logName);
						WriteStateLogsInDB(); //��� ������ � ��

						SaveImg(curFailure, curFrame);
						SaveImgInDB(); //��� ������ � ��
					});
				}

				//����� ���������� � �������/������ ����� ����������� ��������
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
						mw.LABLEZ.Content = $"����� ������: {tuple.workT}%, �������: {tuple.failT}%, �����: {DateTime.Now.Subtract(totalTime).ToString("hh'.'mm'.'ss")}";
						startTime = DateTime.Now;
					});
				}

				//���� ����� ���������� � ������
				if (curFrame != prevFrame)
				{
					curRoi = curFrame[tmprect];

					// ���� ��� ��� ����������� ����� ���� - �������� � ���� �������
					if (lastFrame == null)
					{
						lastFrame = curRoi.Clone();
					}

					Extensions.BeginInvoke(() => mw.myImage.Source = curFrame.Clone().ToImage());

					//��� ���������� ������������������ ������ ����� �� ������ ����
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
				//������ ���������� ���� ������ ���������
				prevFrame = curFrame.Clone();

				//����������� �������� �����
				GC.Collect();
			}, null, 0, frameDelta);
		}

		/// <summary>������ Rect-����� ROI</summary>
		/// <param name="frameSize">������ �����</param>
		/// <param name="args">���������</param>
		/// <returns></returns>
		public Rect CreateBoundBox(Size frameSize, Dot[] args)
		{
			//q[0] - x, 1 - y, 2 - ������, 3 - ������
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

		///<summary>������� �������� ������ �� ������</summary>
		public void Interupt()
		{
			videoStream.Dispose();
			timer.Dispose();
			curFrame.Dispose();
			prevFrame.Dispose();
			tmpFrame.Dispose();
		}

		/// <summary>Checks the dot.</summary>
		/// <param name="x">���������� � �����</param>
		/// <param name="y">���������� � �����</param>
		/// <param name="q">���������� �, �, � ��������� ������ �� ��������� ��� ���� �����</param>
		/// <returns></returns>
		public float CheckDot(float x, float y, float[] q) => q[0] * x + q[1] * y + q[2];

		/// <summary>������� ����������� �� ����� ����� �� ���� ����������</summary>
		/// <param name="line1"> ������ ����� ������ �����</param>
		/// <param name="line2"> ������ ����� ����� �����</param>
		/// <returns>����� ����������� �� ���� �����</returns>
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

		/// <summary>������� ����� ������� �� ���������� ����� ������ � ����� �����</summary>
		/// <param name="x1">���������� X ����� ������ �������</param>
		/// <param name="y1">���������� Y ����� ������ �������</param>
		/// <param name="x2">���������� X ����� ����� �������</param>
		/// <param name="y2">���������� Y ����� ����� �������</param>
		/// <returns>����� �������</returns>
		public double DoLineLength(double x1, double y1, double x2, double y2) => Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));

		/// <summary>������ ������� ������ � ������� � ��������� �� ����������</summary>
		/// <param name="workTime">��������� ����� ������ �����</param>
		/// <param name="failureTime">��������� ����� ������� �����</param>
		/// <returns>������ �� ���������� ������� ������ � ������� � ��������� �� ����� ������� ������</returns>
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
		/// ���������� ��������� �� ���� ������ � ��������\[���� ����� ������ ������ ��������].txt � ������� [���� �����] ���������
		/// </summary>
		/// <param name="state">���������</param>
		/// <param name="logName\">��� ����� ����</param>
		public static void WriteStateLogs(string state, string logName)
		{
			var dirName = "������ � ��������";
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
		///     ����� ��� ���������� ���� ������ ��������� � ��
		/// </summary>
		public static void WriteStateLogsInDB()
		{
		}

		/// <summary>
		///     ���������� ������� ����� ��� ������������� �������
		/// </summary>
		/// <param name="time">����� ������ �������</param>
		/// <param name="curFrame">���� � ��������</param>
		public static void SaveImg(DateTime time, Mat curFrame, bool downtimeEnd = false)
		{
			var folderName = "����������� ����� ������";

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

			Cv2.ImWrite($"{folderName}\\{s} ({(!downtimeEnd ? "������" : "�����")} �������).jpeg", curFrame, n);
		}

		/// <summary>
		///     ����� ��� ���������� �������� � ��
		/// </summary>
		public static void SaveImgInDB()
		{
		}

		#endregion

		#region Private methods

		/// <summary>Algorhitms the specified current roi.</summary>
		/// <param name="curRoi">������� � �������� �������� �������� �����</param>
		/// <param name="time">����� ��� ������������ �������</param>
		/// <param name="lines">  ����� ROI</param>
		/// <param name="grannyInTheROI">���� ������� ������ ������� �� ����� � ROI</param>
		/// <param name="failure">  ���� ������� �������</param>
		private void Algorhitm(ref Mat curRoi, ref DateTime time, float[][] lines, ref bool grannyInTheROI, ref bool failure)
		{
			try
			{
				var tmp = new Mat();
				//�������� �� ����������� ����� ������� 
				Cv2.Subtract(curRoi, lastFrame, tmp);

				//���������� ������ ��� ��������� ���������� ����� 
				var ins = tmp.CvtColor(ColorConversionCodes.BGR2GRAY);

				Cv2.Threshold(ins, ins, binaryEdge, 255, ThresholdTypes.Binary); // binaryEdge - � ����� �������

				//��������� ROI �� �����
				var mask = CreateMask(ref tmp, lines);
				Cv2.BitwiseAnd(ins, mask, ins);

				//����� ������������� ��� ������������ ������������� ������� ��������� �����, � ������� �� ������������
				var tmpWithCont = tmp.Clone();
				tmp.Dispose();

				if (ins.CountNonZero() > detectionEdge)
				{
					mask.Dispose();
					// ��������� ������� ������� ���������� �����  

					if (failure)
					{
						failureTimeMin += DateTime.Now.Subtract(time).TotalMinutes;
						failure = false;

						Extensions.BeginInvoke(() =>
						{
							mw.txtBlockAlarm.Visibility = Visibility.Hidden;
							WriteStateLogs($"����� ������� ������������� {Math.Round(DateTime.Now.Subtract(curFailure).TotalMinutes, 2)} ���.", logName);

							SaveImg(curFailure + DateTime.Now.Subtract(curFailure), curFrame, true);
							WriteStateLogsInDB(); //��� ������ � ��
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

				//����� �������� �� ������
				//var contours = ins.FindContoursAsArray(RetrievalModes.External, ContourApproximationModes.ApproxSimple).Where(x => x.Count() > detectionEdge);

				//var count = contours.Count();

				//for (var i = 0; i < count; i++)
				//{
				//	//���������� ���������� �������������� ������ �����
				//	//var rotRect = Cv2.MinAreaRect(contours.ElementAt(i));

				//	if (GrannyDetect(contours.ElementAt(i), ref ins, plane.dots))
				//	{
				//		mask.Dispose();
				//		// ��������� ������� ������� ���������� �����  

				//		if (failure)
				//		{
				//			failureTimeMin += DateTime.Now.Subtract(time).TotalMinutes;
				//			failure = false;

				//			Extensions.BeginInvoke(() =>
				//			{
				//				mw.txtBlockAlarm.Visibility = Visibility.Hidden;
				//				WriteStateLogs($"����� ������� ������������� {Math.Round(DateTime.Now.Subtract(curFailure).TotalMinutes, 2)} ���.", logName);

				//				SaveImg(curFailure + DateTime.Now.Subtract(curFailure), curFrame, true);
				//				WriteStateLogsInDB(); //��� ������ � ��
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

				//����� �������������� �����������, ���� �����������
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
				// ������ ���������� ���� ������ ��������
				lastFrame = curRoi.Clone();
			}
			catch
			{

			}
		}

		/// <summary>�������� ����� ��� ��������� �����</summary>
		/// <param name="roi"> ROI</param>
		/// <param name="lines">  ����� ROI</param>
		/// <returns>�������-�����</returns>
		private Mat CreateMask(ref Mat roi, float[][] lines)
		{
			var mask = Mat.Zeros(roi.Size(), MatType.CV_8UC1).ToMat();
			var roiVecInd = mask.GetGenericIndexer<Vec3b>();

			for (var i = 0; i < roi.Height; i++)
			{
				for (var j = 0; j < roi.Width; j++)
				{
					var inside = true;
					//�������� �� ���� ������
					for (var k = 0; k < lines.Length; k++)
					{
						//��������� ��� ����� Roi �� ������� �� � Plane
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

		/// <summary>��������� ������������� � ������� � ������������� ����� �� �� ����� �� ������� �������</summary>
		/// <param name="s">������������� RotatedRect</param>
		/// <returns>Rect, ��������������� �� RotatedRect</returns>
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

		/// <summary>������ ���������� �, � � � ��� ��������� ������ �� ���������</summary>
		/// <param name="x1">���������� � ������ �����</param>
		/// <param name="y1">���������� � ������ �����</param>
		/// <param name="x2">���������� � ������ �����</param>
		/// <param name="y2">���������� � ������ �����</param>
		/// <returns></returns>
		private float[] CalcABC(float x1, float y1, float x2, float y2)
		{
			var q = new float[3];
			q[0] = y1 - y2;
			q[1] = x2 - x1;
			q[2] = x1 * y2 - x2 * y1;
			return q;
		}

		/// <summary>�������� ������ ������ ������� �� ����� � ������� ��������</summary>
		/// <param name="contour">  ��������� � ������� �������� ������</param>
		/// <param name="roi">  ������� ������� ��������</param>
		/// <param name="dots">  ������� ����� ���������� �������</param>
		/// <returns>
		///     true - ���� ������ �������� ��� ������/������ ���������� ������� � ����������, �� ���� ��� ������ ������ �������,
		///     ����� - false
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