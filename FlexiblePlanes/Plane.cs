using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlexiblePlanes
{
	/// <summary>
	///     Класс для работы плоскостей
	/// </summary>
	public class Plane
	{
		#region Fields

		#region Public

		public Dot ChangeDot;
		public MyLine ChangeLine;

		/// <summary>
		///     Массив точек
		/// </summary>
		public List<Dot> dots = new List<Dot>();

		public Brush fillingBrush = new SolidColorBrush(Color.FromArgb(50, 150, 0, 0));
		public Brush linesBrush = Brushes.Red;

		#endregion

		#region Private

		private Polygon ChangePolygone;
		private readonly List<MyLine> lines = new List<MyLine>();
		private Point oldMouseCord;

		/// <summary>
		///     Полигон
		/// </summary>
		private Polygon polygone;

		private readonly Window window;

		#endregion

		#endregion

		#region Properties

		#region Public

		public Canvas Canvas { get; }

		#endregion

		#endregion

		#region .ctor

		public Plane()
		{
		}

		/// <summary>
		///     Конструктор
		/// </summary>
		/// <param name="window">Родительское окно</param>
		public Plane(Canvas canvas, Window window, IEnumerable<Point> points = null)
		{
			this.window = window;
			Canvas = canvas;

			ChangeDot = null;

			Canvas.MouseLeave += UpMouse;
			Canvas.MouseUp += UpMouse;
			Canvas.MouseUp += (sender, args) => PlaneChanged?.Invoke(this, EventArgs.Empty);
			Canvas.MouseLeftButtonUp += UpMouse;
			Canvas.MouseMove += MoveMouse;

			if (points == null)
			{
				dots.Add(new Dot(new Point(0.4, 0.4), this, 2, linesBrush));
				dots.Add(new Dot(new Point(0.6, 0.4), this, 2, linesBrush));
				dots.Add(new Dot(new Point(0.6, 0.6), this, 2, linesBrush));
				dots.Add(new Dot(new Point(0.4, 0.6), this, 2, linesBrush));
			}
			else
			{
				foreach (var i in points)
				{
					dots.Add(new Dot(i, this, 2, linesBrush));
				}
			}

			foreach (var i in dots)
			{
				i.drawPoint();
			}

			redrawLine();
		}

		#endregion

		#region Public methods

		public Point absoluteToRelative(Point absolute)
		{
			absolute.X = (absolute.X - (absolute.X < Canvas.ActualWidth / 2 ? Dot.R / 2 : -Dot.R / 2)) / Canvas.ActualWidth;
			absolute.Y = (absolute.Y - (absolute.Y < Canvas.ActualHeight / 2 ? Dot.R / 2 : -Dot.R / 2)) / Canvas.ActualHeight;
			return absolute;
		}

		public Point relativeToAbsolute(Point relative)
		{
			relative.X = relative.X * Canvas.ActualWidth + (relative.X < 0.5 ? Dot.R / 2 : -Dot.R / 2);
			relative.Y = relative.Y * Canvas.ActualHeight + (relative.Y < 0.5 ? Dot.R / 2 : -Dot.R / 2);
			return relative;
		}

		public Point GetMouseCord() => Mouse.GetPosition(window);

		public bool inPlane(Point RelativePoint)
		{
			double angleSumm = 0;
			for (var i = 0; i < dots.Count; i++)
			{
				var v1 = new Vector(dots[i].relativeCord.X - RelativePoint.X, dots[i].relativeCord.Y - RelativePoint.Y);
				var v2 = new Vector(dots[(i + 1) % dots.Count].relativeCord.X - RelativePoint.X, dots[(i + 1) % dots.Count].relativeCord.Y - RelativePoint.Y);

				angleSumm += Vector.AngleBetween(v1, v2);
			}

			if (Math.Abs(Math.Abs(Math.Round(angleSumm)) - 360) < 0.01)
			{
				return true;
			}

			return false;
		}

		public void addNewDots(MyLine line)
		{
			var dot = new Dot(line.GetCenter(), this, 2);
			var index = dots.IndexOf(line.GetFirstPoint());
			dots.Insert(index, dot);
			dot.drawPoint();

			foreach (var i in lines)
			{
				i.remove();
			}

			lines.Clear();

			redrawLine();
		}

		public void removeDot(Dot dot)
		{
			dot.remove();
			dots.Remove(dot);
			foreach (var i in lines)
			{
				i.remove();
			}

			lines.Clear();
			redrawLine();
		}

		/// <summary>
		///     Возвращает строку с относительными координатами всех точек, относительно левого верхнего угла
		/// </summary>
		/// <returns></returns>
		public string GetCord()
		{
			var s = "";
			foreach (var i in dots)
			{
				s += $"x = {string.Format("{0:0.00}", i.relativeCord.X)}  y = {string.Format("{0:0.00}", i.relativeCord.Y)} \n";
			}

			return s;
		}

		/// <summary>
		///     Перерисовывает плоскость
		/// </summary>
		public void redrawLine()
		{
			if (polygone != null)
			{
				Canvas.Children.Remove(polygone);
			}

			polygone = new Polygon();
			polygone.Fill = fillingBrush;
			Panel.SetZIndex(polygone, 0);
			var myPointCollection = new PointCollection();
			foreach (var i in dots)
			{
				myPointCollection.Add(i.absoluteCord);
			}

			polygone.Points = myPointCollection;
			polygone.MouseEnter += onPolygone;
			polygone.MouseLeave += NotOnPolygone;
			polygone.MouseLeftButtonDown += ClickOnPolygone;
			polygone.MouseUp += UpMouse;
			Canvas.Children.Add(polygone);
			if (lines.Count == 0)
			{
				for (var i = 0; i < dots.Count; i++)
				{
					lines.Add(new MyLine(dots[i], dots[(i + 1) % dots.Count], this, 1, linesBrush));
				}
			}

			foreach (var i in lines)
			{
				i.DrawLine();
			}
		}

		/// <summary>
		///     Изменяет размер плоскость в зависимости от размеров канваса
		/// </summary>
		public void Resize()
		{
			foreach (var i in dots)
			{
				i.Resize();
			}

			redrawLine();
		}

		/// <summary>
		///     Удаляет плоскость с канваса
		/// </summary>
		public void remove()
		{
			Canvas.Children.Remove(polygone);
			foreach (var i in dots)
			{
				i.remove();
			}

			foreach (var i in lines)
			{
				i.remove();
			}
		}

		public void SetBrushes(Brush fillingBrush, Brush linesBrush)
		{
			this.fillingBrush = fillingBrush;
			this.linesBrush = linesBrush;
			foreach (var i in lines)
			{
				i.linesBrush = linesBrush;
			}

			foreach (var i in dots)
			{
				i.SetColor(linesBrush);
			}

			redrawLine();
		}

		#endregion

		#region Private methods

		private void MoveMouse(object sender, MouseEventArgs args)
		{
			var point = Mouse.GetPosition(window);
			if (ChangeDot != null)
			{
				var x = point.X;
				var y = point.Y;
				ChangeDot.SetAbsoluteCoordinates(new Point(x - Canvas.Margin.Left, y - Canvas.Margin.Top));
				redrawLine();
			}

			if (ChangeLine != null)
			{
				var delta = oldMouseCord - point;
				ChangeLine.newCord(-delta);
				redrawLine();
			}

			if (ChangePolygone != null)
			{
				var delta = oldMouseCord - point;
				foreach (var i in dots)
				{
					i.AddAbsoluteCoordinates(new Point(-delta.X, -delta.Y));
				}

				redrawLine();
			}

			oldMouseCord = point;
		}

		private void UpMouse(object sender, MouseEventArgs args)
		{
			if (ChangeDot != null)
			{
				ChangeDot = null;
			}

			if (ChangeLine != null)
			{
				ChangeLine.MouseLeftBottonUp();
				ChangeLine = null;
			}

			ChangePolygone = null;
		}

		private void onPolygone(object sender, MouseEventArgs args)
		{
			foreach (var i in lines)
			{
				i.SetBlack();
			}

			foreach (var i in dots)
			{
				i.SetBlack();
			}
		}

		private void NotOnPolygone(object sender, MouseEventArgs args)
		{
			foreach (var i in lines)
			{
				i.SetRed();
			}

			foreach (var i in dots)
			{
				i.SetRed();
			}
		}

		private void ClickOnPolygone(object sender, MouseEventArgs args) => ChangePolygone = polygone;

		#endregion

		public event EventHandler PlaneChanged;
	}
}