﻿using System.Collections.Generic;
using System.Windows;

namespace ActionDetector
{
	/// <summary>
	///     Класс, хранящий поля настроек
	/// </summary>
	public class SettingsFields
	{
		#region Fields

		#region Public

		public double binarizThreshold = 90;
		public List<Point[]> dots = new List<Point[]>();
		public int timeBeforeFailure = 10;
		public int updateInterval = 5;
		public double detectionEdge = 150;

		#endregion

		#endregion

		#region Public methods

		public SettingsFields Clone() => (SettingsFields) MemberwiseClone();

		#endregion
	}
}