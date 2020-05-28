using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace FlexiblePlanes
{
	/// <summary>
	///     Класс статических расширений
	/// </summary>
	public static class Extensions
	{
		#region Public methods

		public static void BeginInvoke(Action act) => Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
		{
			act();
		}));

		[DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
		public static extern void CopyMemory(IntPtr Destination, IntPtr Source, uint Length);

		public static BitmapSource FromNativePointer(IntPtr pData, int w, int h, int ch)
		{
			var format = PixelFormats.Default;

			if (ch == 1)
			{
				format = PixelFormats.Gray8; //grey scale image 0-255
			}

			if (ch == 3)
			{
				format = PixelFormats.Bgr24; //RGB
			}

			if (ch == 4)
			{
				format = PixelFormats.Bgr32; //RGB + alpha
			}

			var wbm = new WriteableBitmap(w, h, 96, 96, format, null);
			CopyMemory(wbm.BackBuffer, pData, (uint) (w * h * ch));

			wbm.Lock();
			wbm.AddDirtyRect(new Int32Rect(0, 0, wbm.PixelWidth, wbm.PixelHeight));
			wbm.Unlock();

			return wbm;
		}

		public static BitmapSource FromArray(byte[] data, int w, int h, int ch)
		{
			var format = PixelFormats.Default;

			if (ch == 1)
			{
				format = PixelFormats.Gray8; //grey scale image 0-255
			}

			if (ch == 3)
			{
				format = PixelFormats.Bgr24; //RGB
			}

			if (ch == 4)
			{
				format = PixelFormats.Bgr32; //RGB + alpha
			}

			//WriteableBitmap wbm = new WriteableBitmap(w, h, 96, 96, format, null);
			//wbm.WritePixels(new Int32Rect(0, 0, w, h), data, ch * w, 0);
			//wbm.Freeze();
			var bms = BitmapSource.Create(w, h, 96, 96, format, null, data, ch * w);

			bms.Freeze();

			return bms;
		}

		/// <summary>
		///     Преобразует Mat в BitmapImage
		/// </summary>
		/// <param name="mat"></param>
		/// <returns></returns>
		public static BitmapImage ToImage(this Mat mat)
		{
			var image = new BitmapImage();
			try
			{
				using (var mem = new MemoryStream(mat.ToBytes()))
				{
					mat.Dispose();
					image.BeginInit();
					mem.Position = 0;
					image.CacheOption = BitmapCacheOption.OnLoad;
					image.StreamSource = mem;
					image.EndInit();
				}
			}
			catch (Exception e)
			{
				Trace.Write(e.ToString());
			}

			image.Freeze();
			return image;
		}

		[DllImport("gdi32.dll")]
		public static extern bool DeleteObject(IntPtr hObject);

		/// <summary>
		///     Преобразует Bitmap в BitmapImage
		/// </summary>
		/// <param name="mat"></param>
		/// <returns></returns>
		public static BitmapImage ToImageSource(this Bitmap bitmap)
		{
			var bitmapimage = new BitmapImage();
			using (var memory = new MemoryStream())
			{
				bitmap.Save(memory, ImageFormat.Bmp);
				bitmap.Dispose();
				bitmapimage.BeginInit();
				bitmapimage.StreamSource = memory;
				bitmapimage.CacheOption = BitmapCacheOption.None;
				bitmapimage.EndInit();
				bitmapimage.Freeze();
			}

			return bitmapimage;
		}

		public static WriteableBitmap ToWriteableBitmap(this Mat mat)
		{
			/*WriteableBitmap wrb = new WriteableBitmap(mat.Width, mat.Height, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null);
			var bytes = mat.ToBytes();
			wrb.WritePixels(new System.Windows.Int32Rect(0, 0, mat.Width, mat.Height), bytes, 3, 0);*/
			//return wrb;
			/*var ms = new MemoryStream(mat.ToBytes());
			System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(ms);*/

			var img = WriteableBitmapConverter.ToWriteableBitmap(mat);
			img.Freeze();
			return img;
		}

		/// <summary>
		///     Преобразует BitmapImage в Mat
		/// </summary>
		/// <param name="bitmapImage">Исходное изображение</param>
		/// <returns></returns>
		public static Mat ToMat(this BitmapImage bitmapImage)
		{
			byte[] data;
			var encoder = new JpegBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
			using (var ms = new MemoryStream())
			{
				encoder.Save(ms);
				data = ms.ToArray();
			}

			return Cv2.ImDecode(data, ImreadModes.Unchanged);
		}

		/// <summary>
		///     Быстрый способ вывести Action в главный поток
		/// </summary>
		/// <param name="act">Делегат с действиями для выполения</param>
		public static void MakeActInMainThread(this Action act) => Application.Current?.Dispatcher?.BeginInvoke(act);

		/// <summary>
		///     Сохранение BitmapImage по выбранному пути
		/// </summary>
		/// <param name="bmp">Изображение</param>
		/// <param name="path">Путь сохранения</param>
		public static void Save(this BitmapImage bmp, string path)
		{
			if (File.Exists($@"{path}.jpg"))
			{
				return;
			}

			var encoder = new JpegBitmapEncoder();
			encoder.Frames.Add(BitmapFrame.Create(bmp));
			using (var filestream = new FileStream($"{path}.jpg", FileMode.Create))
			{
				encoder.Save(filestream);
			}
		}

		public static void Save(this Mat bmp, string path)
		{
			if (File.Exists(path + ".jpg"))
			{
				return;
			}

			Cv2.ImWrite(path + ".jpg", bmp, new ImageEncodingParam(ImwriteFlags.JpegOptimize, 100));
		}

		public static void SaveTo(this Image bmp, string path)
		{
			if (File.Exists(path + ".jpg"))
			{
				return;
			}

			bmp.Save(path + ".jpg", ImageFormat.Jpeg);
		}

		public static void Save(this BitmapSource bmp, string path) => BitmapSourceConverter.ToMat(bmp).Save(path);

		public static BitmapSource OpenBitmapSource(string path)
		{
			var sw = new Stopwatch();
			sw.Start();
			BitmapSource source;
			using (var bmp = new Bitmap(path))
			{
				var hbm = bmp.GetHbitmap();
				source = Imaging.CreateBitmapSourceFromHBitmap(
					hbm, IntPtr.Zero, Int32Rect.Empty,
					BitmapSizeOptions.FromEmptyOptions());
				source.Freeze();
				DeleteObject(hbm);
			}

			return source;
		}

		public static void WriteMat(this WriteableBitmap img, Mat mat)
		{
			img.Lock();
			img.WritePixels(new Int32Rect(0, 0, mat.Width, mat.Height), mat.Data, (int) (mat.DataEnd.ToInt64() - mat.Data.ToInt64()), (int) mat.Step());
			img.Unlock();
		}

		#endregion
	}
}