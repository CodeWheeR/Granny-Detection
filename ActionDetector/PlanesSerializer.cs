using System;
using System.Collections.Generic;
using System.Windows;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace ActionDetector
{

	/// <summary>
	///Класс для тёмной магии сериализации
	/// </summary>
	static class PьlanesSerializer
	{
		static XmlSerializer serializer = new XmlSerializer(typeof(List<Point[]>));

		public static void Serialize(List<Plane> planes, string file)
		{
			List<Point[]> dots = new List<Point[]>();
			foreach(var i in planes)
			{
				dots.Add(i.dots.Select(x => x.relativeCord).ToArray());
			}

			using (FileStream fs = new FileStream(file, FileMode.Create))
				serializer.Serialize(fs, dots);
		}

		public static List<Point[]> Deserialize(string file)
		{
			List<Point[]> planes;
			if (!File.Exists(file))
				return new List<Point[]>();

			using (FileStream fs = new FileStream(file, FileMode.Open))
				planes = (List<Point[]>)serializer.Deserialize(fs);
			return planes;
		}
	}
}
