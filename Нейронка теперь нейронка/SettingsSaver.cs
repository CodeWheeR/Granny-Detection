using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.IO;
using System.Windows;

namespace ActionDetector
{
	/// <summary>
    /// Класс, сохраняющий/восстанавливающий настройки
    /// </summary>
    class SettingsSaver
	{
		private string settingsLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");

		/// <summary>
		/// Запись настроек
		/// </summary>
		public SettingsFields WriteSettings(SettingsFields settFields, string path = null)
        {        
            XmlSerializer ser = new XmlSerializer(typeof(SettingsFields));

			using (var writer = File.OpenWrite(path ?? settingsLocation))
			{
				ser.Serialize(writer, settFields);
			}

			return settFields;
        }
        
        /// <summary>
        /// Чтение настроек
        /// </summary>
        public SettingsFields ReadSettings(SettingsFields settFields)
        {
			Microsoft.Win32.OpenFileDialog fd = new Microsoft.Win32.OpenFileDialog();
			fd.Filter = "Файлы настроек|*.xml";
			var result = fd.ShowDialog();
			if (result == true)
			{
				XmlSerializer ser = new XmlSerializer(typeof(SettingsFields));

				using (var reader = new FileStream(fd.FileName, FileMode.Open))
				{
					settFields = ser.Deserialize(reader) as SettingsFields;
				}
			}

			return settFields;
        }

		/// <summary>
		/// Фоновое чтение настроек
		/// </summary>
		public SettingsFields SilentReadSettings()
		{
			XmlSerializer ser = new XmlSerializer(typeof(SettingsFields));

			//Тоже, что и new FileStream(fd.FileName, FileMode.Open))
			using (var reader = File.OpenRead(settingsLocation))
			{
				return ser.Deserialize(reader) as SettingsFields;
			}
		}
	}
}
