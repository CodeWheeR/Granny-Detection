﻿using System;
using System.Collections.Generic;

using OpenCvSharp;

namespace FlexiblePlanes
{
	public static class FrameExtensions
	{
		#region Public methods

		public static Rect GetRoiRect(Size frameSize, List<Dot> dots, out float[][] lines)
		{
			var tmprect = CreateBoundBox(frameSize, dots.ToArray());

			lines = new float[dots.Count][]; // это координаты ROI

			for (var i = 0; i < dots.Count; i++)
			{
				var index1 = (i + 2) % dots.Count;
				var index2 = (i + 3) % dots.Count;

				lines[i] = CalcABC((int) (dots[index1].relativeCord.X * frameSize.Width - tmprect.X),
								   (int) (dots[index1].relativeCord.Y * frameSize.Height - tmprect.Y),
								   (int) (dots[index2].relativeCord.X * frameSize.Width - tmprect.X),
								   (int) (dots[index2].relativeCord.Y * frameSize.Height - tmprect.Y));
			}

			return tmprect;
		}

		public static Rect CreateBoundBox(Size frameSize, Dot[] args)
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

		/// <summary>
		///     Создание маски для отсечения помех
		/// </summary>
		public static Mat CreateMask(Mat roi, float[][] lines)
		{
			var mask = Mat.Zeros(roi.Size(), roi.Type()).ToMat();
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

		public static float CheckDot(float x, float y, float[] q) => q[0] * x + q[1] * y + q[2];

		#endregion

		#region Private methods

		private static float[] CalcABC(float x1, float y1, float x2, float y2)
		{
			var q = new float[3];
			q[0] = y1 - y2;
			q[1] = x2 - x1;
			q[2] = x1 * y2 - x2 * y1;
			return q;
		}

		#endregion
	}
}