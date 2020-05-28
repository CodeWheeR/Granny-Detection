using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlexiblePlanes
{
	public class Dot
	{
		#region Static Fiends and Constants

		#region Public

		public static float R = 8;

		#endregion

		#endregion

		#region Fields

		#region Public

		public Point absoluteCord;
		public Brush color = Brushes.Red;
		public Point relativeCord;

		#endregion

		#region Private

		private readonly Plane plane;
		private readonly int zIndex;
		private Ellipse el;

		#endregion

		#endregion

		#region .ctor

		public Dot(){}

		/// <summary>
		/// Конструктор
		/// </summary>
		/// <param name="RelativeCord">Относительная координата</param>
		/// <param name="plane">Плоскость содержащая точку</param>
		/// 
		public Dot(Point RelativeCord, Plane plane, int zIndex)
		{
			relativeCord = RelativeCord;
			this.plane = plane;
			this.zIndex = zIndex;
			checkRelativeCord();
			absoluteCord = plane.relativeToAbsolute(RelativeCord);

		}

		public Dot(Point RelativeCord, Plane plane, int zIndex, Brush color)
		{
			relativeCord = RelativeCord;
			this.plane = plane;
			this.zIndex = zIndex;
			checkRelativeCord();
			absoluteCord = plane.relativeToAbsolute(RelativeCord);
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Удаляет точку с канваса
		/// </summary>
		public void remove() => plane.Canvas.Children.Remove(el);

		//###############################_Устанавливает_Красный_и_Чёрный+Цвета в Эллипс_########################################
		public void SetRed() => el.Stroke = Brushes.Red;
		public void SetBlack() => el.Stroke = Brushes.Black;

		public void SetColor(Brush b)
		{
			el.Fill = b;
			el.Stroke = b;
			color = b;
		}

		public void SetAbsoluteCoordinates(Point absoluteCord)
		{
			if (el != null)
			{
				this.absoluteCord = absoluteCord;
				checkAbsoluteCord();
				relativeCord = plane.absoluteToRelative(this.absoluteCord);
				Canvas.SetTop(el, this.absoluteCord.Y - R / 2);
				Canvas.SetLeft(el, this.absoluteCord.X - R / 2);
			}
		}

		public void AddAbsoluteCoordinates(Point absoluteCord)
		{
			this.absoluteCord.X += absoluteCord.X;
			this.absoluteCord.Y += absoluteCord.Y;
			checkAbsoluteCord();
			relativeCord = plane.absoluteToRelative(this.absoluteCord);
			Canvas.SetTop(el, this.absoluteCord.Y - R / 2);
			Canvas.SetLeft(el, this.absoluteCord.X - R / 2);
		}

		/// <summary>
		/// Отрисовывает точку на канвасе
		/// </summary>
		public void drawPoint()
		{
			if(el != null)
			{
				plane.Canvas.Children.Remove(el);
			}

			el = new Ellipse();
			el.Width = R;
			el.Height = el.Width;
			el.Fill = color;
			el.Stroke = color;
			el.StrokeThickness = 3;
			el.MouseDown += MouseDownFunc;
			el.MouseEnter += MouseEnterFunc;
			el.MouseLeave += MouseLeaveFunc;
			el.MouseRightButtonDown += MouseRightBottonDown;
			Canvas.SetTop(el, absoluteCord.Y - R / 2);
			Canvas.SetLeft(el, absoluteCord.X - R / 2);
			Panel.SetZIndex(el, zIndex);
			plane.Canvas.Children.Add(el);            
		}

		/// <summary>
		/// Перерисовывает точку при изменении размеров канваса
		/// </summary>
		public void Resize()
		{
			absoluteCord = plane.relativeToAbsolute(relativeCord);
			checkAbsoluteCord();
			Canvas.SetTop(el, absoluteCord.Y - R / 2);
			Canvas.SetLeft(el, absoluteCord.X - R / 2);
		}

		/// <summary>
		/// Обработчик нажатия кнопки мыши
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		public void MouseDownFunc(object sender, MouseEventArgs args) => plane.ChangeDot = this;

		public void MouseEnterFunc(object sender, MouseEventArgs args) => SetBlack();
		public void MouseLeaveFunc(object sender, MouseEventArgs args) => SetRed();

		public void MouseRightBottonDown(object sender, MouseEventArgs args)
		{
			el.ContextMenu = new ContextMenu();
			var mi4 = new MenuItem();
			mi4.Header = "Удалить точку";
			mi4.Click += Mi4_Click; ;
		   el.ContextMenu.Items.Add(mi4);
			el.ContextMenu.IsEnabled = true;

		}

		public void checkAbsoluteCord()
		{
			if (absoluteCord.Y < R / 2)
			{
				absoluteCord.Y = R / 2;
			}
			else if (absoluteCord.Y > plane.Canvas.ActualHeight - R / 2)
			{
				absoluteCord.Y = plane.Canvas.ActualHeight - R / 2;
			}

			if (absoluteCord.X < R / 2)
			{
				absoluteCord.X = R / 2;
			}
			else if (absoluteCord.X > plane.Canvas.ActualWidth - R / 2)
			{
				absoluteCord.X = plane.Canvas.ActualWidth - R / 2;
			}
		}

		public void checkRelativeCord()
		{
			var absolute = plane.relativeToAbsolute(relativeCord);

			if (absolute.Y < R / 2)
			{
				relativeCord.Y = plane.absoluteToRelative(new Point(0, R / 2)).Y;
			}
			else if (absolute.Y > plane.Canvas.ActualHeight - R / 2)
			{
				relativeCord.Y = plane.absoluteToRelative(new Point(0, plane.Canvas.ActualHeight - R / 2)).Y;
			}
			if (absolute.X < R / 2)
			{
				relativeCord.X = plane.absoluteToRelative(new Point(R / 2, 0)).X;
			}
			else if (absolute.X > plane.Canvas.ActualWidth - R / 2)
			{
				relativeCord.X = plane.absoluteToRelative(new Point(plane.Canvas.ActualWidth - R / 2, 0)).X;
			}
		}

		#endregion

		#region Private methods

		private void Mi4_Click(object sender, RoutedEventArgs e) => plane.removeDot(this);

		#endregion
	}
}