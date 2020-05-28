using OpenCvSharp;
using OpenCvSharp.UserInterface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NeuroStudio;
using System.Threading;

namespace ActionDetector
{
    class FrameAnalyzer : IDisposable
    {
        #region PrivateFields
        ///<summary>Видеопоток</summary>
        private VideoCapture videoStream;
        bool cameraMode = false;
        ///<summary>Ссылка на главный PictureBox</summary>
        private Plane plane;
        ///<summary>Главный таймер, отвечающий за своевременное получение и обработку кадров</summary>
        private Timer timer;
        ///<summary>Матрица с предыдущим кадром</summary>
        private Mat prevFrame = new Mat();
        ///<summary>Матрица с текущим кадром</summary>
        private Mat curFrame = new Mat();
        ///<summary>Временная матрица с измененным кадром</summary>
        private Mat tmpFrame = new Mat();
        private DateTime lastTime;
        private Mat lastFrame;
        private CancellationToken token;
        ///<summary>Нижняя граница трешхолда</summary>
        private int binaryEdge;
        /// <summary>
        /// Время простоя
        /// </summary>
        private int waitingEdge = 60000;
        private MainWindow mw;
        //Счётчик кол-ва простоев
        private int alarmCount = 0;
        //время работы стана
        private double workTimeMin = 0;
        //время простоя стана
        private double failureTimeMin = 0;
        private int refrPerc = 5; //Период обновления процентов
        #endregion

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
                videoStream = VideoCapture.FromFile(filePath);

            plane = pictureBox;
            lastTime = DateTime.Now;
            cameraMode = camM;
        }

        public void Start()
        {
            alarmCount = 0;
            //флаг наличия движение желтого объекта на стане в области интереса
            bool grannyInTheROI = false;
            ///<summary>Матрица с областью интереса текущего кадра</summary>
            Mat curRoi;

            //флаг наличия простоя
            bool failure = false;


            var frameSize = new OpenCvSharp.Size(videoStream.FrameWidth, videoStream.FrameHeight);
            Rect tmprect = CreateBoundBox(frameSize, plane.dots.ToArray());

            float[][] lines = new float[plane.dots.Count][]; // это координаты ROI

            for (int i = 0; i < plane.dots.Count; i++)
            {
                var index1 = (i + 2) % plane.dots.Count;
                var index2 = (i + 3) % plane.dots.Count;

                lines[i] = CalcABC((int)(plane.dots[index1].relativeCord.X * frameSize.Width - tmprect.X),
                                   (int)(plane.dots[index1].relativeCord.Y * frameSize.Height - tmprect.Y),
                                   (int)(plane.dots[index2].relativeCord.X * frameSize.Width - tmprect.X),
                                   (int)(plane.dots[index2].relativeCord.Y * frameSize.Height - tmprect.Y));
            }

            //Время для отслеживания простоя
            var time = DateTime.Now;
            //время для вывода информации о простое и работе в процентах
            var startTime = DateTime.Now;
            //время работы программы
            var totalTime = DateTime.Now;

            int frameDelta;

            if (!cameraMode)
                frameDelta = (int)(1000 / videoStream.Fps);
            else frameDelta = 30;


            videoStream.Read(curFrame);

            timer = new Timer((object state) =>
            {
                videoStream.Read(curFrame);
                bool empty = curFrame.Empty();

                if (token.IsCancellationRequested || empty)
                {
                    Extensions.BeginInvoke(new Action(() => {mw.txtBlockAlarm.Visibility = System.Windows.Visibility.Hidden;}));//@
                    timer.Dispose();
                    return;
                }

                //Считывание порога бинаризации и времени простоя
                Extensions.BeginInvoke(new Action(() =>
                {
                    binaryEdge = (int)mw.binarizationSlider.Value;
                    if (int.TryParse(mw.WaitingTimeEdge.Text, out var a))
                    {
                        waitingEdge = a;
                    }
                    if (int.TryParse(mw.txtUpdatePeriod.Text, out var b))
                    {
                        refrPerc = b;
                    }
                }));

                //лучше бы на минуты переделать, честно говоря
                //Если желтый объект на стане отсутствовал дольше допустимого времени и информационная надпись о простое скрыта
                if (mw.txtBlockAlarm.Visibility == System.Windows.Visibility.Hidden && DateTime.Now.Subtract(time).TotalSeconds > waitingEdge)
                {
                    failure = true;

                    workTimeMin += DateTime.Now.Subtract(time).TotalMinutes; //В минутах потому, что это не слишком мало и не слишком много, переполнения не будет долго.
                    time = DateTime.Now;

                    alarmCount++;

                    //вывод надписи о простое и показ кол-ва простоев
                    Extensions.BeginInvoke(new Action(() =>
                    {
                        mw.labelCountAlarm.Content = $"Количество простоев: {alarmCount}";
                        mw.txtBlockAlarm.Visibility = System.Windows.Visibility.Visible;
                    }));
                }

                //Вывод информации о простое/работе через определённый интервал
                if (DateTime.Now.Subtract(startTime).TotalSeconds >= refrPerc)
                    Extensions.BeginInvoke(new Action(() =>
                    {
                        if (failure)
                        {
                            failureTimeMin += DateTime.Now.Subtract(startTime).TotalMinutes;
                            startTime = DateTime.Now;
                        }
                                               
                        var tuple = TimeCount(workTimeMin, failureTimeMin);
                        mw.LABLEZ.Content = $"Время работы: {tuple.workT}%, простоя: {tuple.failT}%, всего: {DateTime.Now.Subtract(totalTime).ToString("hh'.'mm'.'ss")}";
                        startTime = DateTime.Now;
                    })); 


                //Если кадры изменились в потоке
                if (curFrame != prevFrame)
                {
                    curRoi = curFrame[tmprect];

                    // если МАТ для предыдущего файла пуст - копируем в него текущий
                    if (lastFrame == null)
                        lastFrame = curRoi.Clone();

                    Extensions.BeginInvoke(() => mw.myImage.Source = curFrame.Clone().ToImage());

                    //для увеличения производительности решено брать не каждый кадр
                    if ((DateTime.Now - lastTime).TotalMilliseconds > 500)
                    {
                        try
                        {
                            Task.Run(() => Algorhitm(ref curRoi, ref time, lines, ref grannyInTheROI, ref failure));
                        }
                        catch { };
                    }

                }

                prevFrame.Dispose();
                //Делаем предыдущий кадр равным нынешнему
                prevFrame = curFrame.Clone();

                //Обязательно собираем мусор
                GC.Collect();

            }, null, 0, frameDelta);
        }

        private void Algorhitm(ref Mat curRoi, ref DateTime time, float[][] lines, ref bool grannyInTheROI, ref bool failure)
        {
            try
            {
                Mat tmp = new Mat();
                //вычитаем из предыдущего кадра текущий 
                Cv2.Subtract(curRoi, lastFrame, tmp);

                //Извлечение канала для улучшения детектинга бабки 
                var ins = tmp.CvtColor(ColorConversionCodes.BGR2GRAY);

                Cv2.Threshold(ins, ins, binaryEdge, 255, ThresholdTypes.Binary);//@ binaryEdge - С формы бегунок

                //Обрезание ROI по маске
                var mask = CreateMask(ref tmp, lines);
                Cv2.BitwiseAnd(ins, mask, ins);


                //нужен исключительно для демонстрации нарисованного контура выделения бабки, в решении не используется
                var tmpWithCont = tmp.Clone();
                tmp.Dispose();//@

                //поиск контуров на фрейме
                var contours = ins.FindContoursAsArray(RetrievalModes.External, ContourApproximationModes.ApproxSimple).Where(x => x.Count() > 150); //@

                var count = contours.Count();

                for (int i = 0; i < count; i++)
                {
                    //построение повёрнутого прямоугольника вокруг пятна
                    var rotRect = Cv2.MinAreaRect(contours.ElementAt(i));

                    if (GrannyDetect(contours.ElementAt(i), ref ins, plane.dots))
                    {
                        mask.Dispose();
                        // остановка времени отсчёта отсутствия бабки  

                        if (failure)
                        {
                            failureTimeMin += DateTime.Now.Subtract(time).TotalMinutes;

                            failure = false;

                            Extensions.BeginInvoke(() =>
                            {
                                mw.txtBlockAlarm.Visibility = System.Windows.Visibility.Hidden;
                            });
                        }
                        else
                        {
                            workTimeMin += DateTime.Now.Subtract(time).TotalMinutes;
                        }
                        time = DateTime.Now;

                        grannyInTheROI = true;

                        var rect = ToRect(rotRect);
                        tmpWithCont.Rectangle(rect, new Scalar(0, 255, 0), 3);
                        break;
                    }
                    else if (grannyInTheROI)
                    {
                        workTimeMin += DateTime.Now.Subtract(time).TotalMinutes;
                        time = DateTime.Now;
                        grannyInTheROI = false;
                    }
                }

                var DetectTMP = tmpWithCont.ToImage();
                DetectTMP.Freeze();

                //показ трешхолднутого изображения, если понадобится
                Extensions.BeginInvoke(() =>
                {
                    if ((bool)mw.checkThresh.IsChecked)
                        Cv2.ImShow("Threshold", ins.Clone());
                    else
                    if (!(bool)mw.checkThresh.IsChecked)
                    {
                        Cv2.DestroyWindow("Threshold");
                    }
                    mw.myLittleImage.Source = DetectTMP;
                });


                lastTime = DateTime.Now;
                lastFrame.Dispose();
                // Делаем предыдущий кадр равным текущему
                lastFrame = curRoi.Clone();
            }
            catch { };
        }

        /// <summary>
        /// Создание маски для отсечения помех
        /// </summary>
        private Mat CreateMask(ref Mat roi, float[][] lines)
        {
            var mask = Mat.Zeros(roi.Size(), MatType.CV_8UC1).ToMat();
            Mat.Indexer<Vec3b> roiVecInd = mask.GetGenericIndexer<Vec3b>();

            for (int i = 0; i < roi.Height; i++)
            {
                for (int j = 0; j < roi.Width; j++)
                {
                    bool inside = true;
                    //Проходит по всем линиям
                    for (int k = 0; k < lines.Length; k++)
                    {
                        //Проверяет все точки Roi на наличие их в Plane
                        float tmp2 = CheckDot(j, i, lines[k]);
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

        public Rect CreateBoundBox(OpenCvSharp.Size frameSize, Dot[] args)
        {
            //q[0] - x, 1 - y, 2 - Ширина, 3 - Высота
            int[] q = new int[4];
            q[0] = 99999999;
            q[1] = 99999999;
            foreach (var i in args)
            {
                var x = i.relativeCord.X * frameSize.Width;
                if (x < q[0])
                {
                    q[0] = (int)(x);
                }

                var y = i.relativeCord.Y * frameSize.Height;
                if (y < q[1])
                {
                    q[1] = (int)(y);
                }

                var w = i.relativeCord.X * frameSize.Width;
                if (w > q[2])
                {
                    q[2] = (int)(w);
                }

                var h = i.relativeCord.Y * frameSize.Height;
                if (h > q[3])
                {
                    q[3] = (int)(h);
                }
            }

            return new Rect(q[0], q[1], q[2] - q[0], q[3] - q[1]);
        }

        public void Dispose()
        {
            Interupt();
        }

        ///<summary>Фунция выгрузки класса из памяти</summary>
        public void Interupt()
        {
            videoStream.Dispose();
            timer.Dispose();
            curFrame.Dispose();
            prevFrame.Dispose();
            tmpFrame.Dispose();
        }

        /// <summary>
        /// Повёрнутый прямоугольник в обычный с отслеживанием чтобы он не вышел за границы матрицы
        /// </summary>
        private OpenCvSharp.Rect ToRect(RotatedRect s)
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
        private float[] CalcABC(float x1, float y1, float x2, float y2)
        {
            float[] q = new float[3];
            q[0] = y1 - y2;
            q[1] = x2 - x1;
            q[2] = (x1 * y2) - (x2 * y1);
            return q;
        }

        public float CheckDot(float x, float y, float[] q)
        {
            return q[0] * x + q[1] * y + q[2];
        }

        /// <summary>
        /// Алгоритм поиса жёлтого объекта на стане в области интереса
        /// </summary>
        /// <param name="contour">найденный в области интереса контур</param>
        /// <param name="roi">матрица области интереса</param>
        /// <param name="dots">крайние точки выделенной области</param>
        /// <returns></returns>
        private bool GrannyDetect(OpenCvSharp.Point[] contour, ref Mat roi, List<Dot> dots)
        {
            var rotRect = Cv2.MinAreaRect(contour);

            var minXPoint = contour[0];
            var minYPoint = contour[0];

            for (int z = 0; z < contour.Count(); z++)
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
                var roiHeight = DoSmallestLine(new System.Windows.Point[] { dots[0].absoluteCord, dots[3].absoluteCord }, new System.Windows.Point[] { dots[1].absoluteCord, dots[2].absoluteCord });
                if (rectHeight >= roiHeight && IsSqrtRect(rotRect))
                {
                    return true;
                }
                else return false;
            }
            else
            {
                var roiWidth = DoSmallestLine(new System.Windows.Point[] { dots[0].absoluteCord, dots[1].absoluteCord }, new System.Windows.Point[] { dots[2].absoluteCord, dots[3].absoluteCord });
                if (rectHeight >= roi.Width && IsSqrtRect(rotRect))
                {
                    return true;
                }
                else return false;
            }
        }

        /// <summary>
        /// Функция, проверяющая что найденный контур +- квадратный (примерно габаритов желтого объекта на стане)
        /// </summary>
        public bool IsSqrtRect(RotatedRect rect)
        {
            var minRect = rect.Points();

            var rectHeight = DoLineLength(minRect[0].X, minRect[0].Y, minRect[1].X, minRect[1].Y);
            var rectWidth = DoLineLength(minRect[1].X, minRect[1].Y, minRect[2].X, minRect[2].Y);

            if (rectHeight > rectWidth)
                return rectWidth >= rectHeight / 2;
            else return rectHeight >= rectWidth / 2;
        }

        /// <summary>
        /// Находит менимальную по длине линию из двух переданных
        /// </summary>
        public double DoSmallestLine(System.Windows.Point[] line1, System.Windows.Point[] line2)
        {
            var line1Length = DoLineLength(line1[0].X, line1[0].Y, line1[1].X, line1[1].Y);
            var line2Length = DoLineLength(line2[0].X, line2[0].Y, line2[1].X, line2[1].Y);

            if (line1Length > line2Length) return line2Length;
            else return line1Length;
        }


        /// <summary>
        /// Считает длину отрезка по кординатам точки начала и точки конца
        /// </summary>
        /// <returns></returns>
        public double DoLineLength(double x1, double y1, double x2, double y2)
        {
            return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
        }

        /// <summary>
        /// Расчёт времени работы и простоя в процентах от прошедшего
        /// </summary>
        public (double workT, double failT) TimeCount(double workTime, double failureTime)
        {
            double totalTime = workTime + failureTime;
            if (totalTime != 0)
            {
                workTime /= totalTime / 100;
                failureTime /= totalTime / 100;
            }
            else
            {
                workTime = 0;
                failureTime = 0;
            }
            var tuple = (Math.Round(workTime, 1), Math.Round(failureTime, 1));
            return tuple;
        }

    }
}
