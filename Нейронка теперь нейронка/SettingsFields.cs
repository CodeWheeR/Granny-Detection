using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ActionDetector
{
	/// <summary>
	/// Класс, хранящий поля настроек
	/// </summary>
	public class SettingsFields
	{
		public List<Point[]> dots = new List<Point[]>();

		public int updateInterval = 5;
		public int timeBeforeFailure = 10;
		public bool showThreshImg;
		public bool showAdvSettings;
		public double binarizThreshold = 90;

		public SettingsFields Clone() => (SettingsFields)MemberwiseClone();
	}
}
