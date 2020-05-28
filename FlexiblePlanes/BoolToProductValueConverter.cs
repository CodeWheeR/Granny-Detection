using System;
using System.Globalization;
using System.Windows.Data;

namespace FlexiblePlanes
{
	public class BoolToProductValueConverter : IValueConverter
	{
		#region Implementation of IValueConverter

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !(bool) value ? "брак" : "стандарт";
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value.ToString().Contains("брак") ? false : true;

		#endregion
	}
}