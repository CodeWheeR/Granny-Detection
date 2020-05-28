using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlexiblePlanes
{
	/// <summary>
	///     Для создания ROI
	/// </summary>
	public class MyLine
	{
		#region Fields

		#region Public

		public Brush linesBrush;

		#endregion

		#region Private

		private Brush currentColor;
		private readonly Dot d1;
		private readonly Dot d2;
		private Line line;
		private readonly Plane plane;
		private readonly int zIndex;

		#endregion

		#endregion

		#region .ctor

		public MyLine()
		{
		}

		public MyLine(Dot dot1, Dot dot2, Plane plane, int zIndex, Brush linesBrush = null)
		{
			this.zIndex = zIndex;
			this.plane = plane;
			this.linesBrush = linesBrush ?? Brushes.Red;
			d1 = dot1;
			d2 = dot2;
		}

		#endregion

		#region Public methods

		public Point GetCenter() => new Point((d1.relativeCord.X + d2.relativeCord.X) / 2.0, (d1.relativeCord.Y + d2.relativeCord.Y) / 2.0);
		public Dot GetFirstPoint() => d2;

		public void DrawLine()
		{
			if (line != null)
			{
				line.X1 = d1.absoluteCord.X;
				line.X2 = d2.absoluteCord.X;
				line.Y1 = d1.absoluteCord.Y;
				line.Y2 = d2.absoluteCord.Y;
				line.Stroke = currentColor ?? linesBrush;
			}
			else
			{
				line = new Line();
				line.StrokeThickness = 5;
				line.Stroke = linesBrush;
				line.X1 = d1.absoluteCord.X;
				line.X2 = d2.absoluteCord.X;
				line.Y1 = d1.absoluteCord.Y;
				line.Y2 = d2.absoluteCord.Y;
				line.MouseEnter += MouseEnter;
				line.MouseLeftButtonDown += MouseLeftBottonDown;
				line.MouseRightButtonDown += MouseRightBottonDown;
				Panel.SetZIndex(line, zIndex);
				line.MouseLeave += MouseLeave;
				plane.Canvas.Children.Add(line);
			}
		}

		public void newCord(Vector vector)
		{
			d1.AddAbsoluteCoordinates(new Point(vector.X, vector.Y));
			d2.AddAbsoluteCoordinates(new Point(vector.X, vector.Y));
		}

		public void MouseRightBottonDown(object sender, MouseEventArgs args)
		{
			line.ContextMenu = new ContextMenu();
			var mi4 = new MenuItem();
			mi4.Header = "Добавить точку";
			mi4.Click += Mi4_Click;
			line.ContextMenu.Items.Add(mi4);
			line.ContextMenu.IsEnabled = true;
		}

		public void MouseLeftBottonDown(object sender, MouseEventArgs args) => plane.ChangeLine = this;

		public void MouseLeftBottonUp()
		{
			SetRed();
			d1.SetRed();
			d2.SetRed();
		}

		public void MouseEnter(object sender, MouseEventArgs args)
		{
			if (plane.ChangeLine == null)
			{
				SetBlack();
				d1.SetBlack();
				d2.SetBlack();
			}
		}

		public void SetBlack()
		{
			line.Stroke = Brushes.Black;
			currentColor = Brushes.Black;
		}

		public void SetRed()
		{
			line.Stroke = linesBrush;
			currentColor = linesBrush;
		}

		public void remove() => plane.Canvas.Children.Remove(line);

		#endregion

		#region Private methods

		private void Mi4_Click(object sender, RoutedEventArgs e) => plane.addNewDots(this);

		private void MouseLeave(object sender, MouseEventArgs args)
		{
			if (plane.ChangeLine != this)
			{
				SetRed();
				d1.SetRed();
				d2.SetRed();
			}
		}

		#endregion
	}
}