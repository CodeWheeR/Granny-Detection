using System;
using System.IO;
using System.Xml.Serialization;

using Microsoft.Win32;

namespace ActionDetector
{
	/// <summary>
	///     Класс, сохраняющий/восстанавливающий настройки
	/// </summary>
	internal class SettingsSaver
	{
		#region Fields

		#region Private

		private readonly string settingsLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");

		#endregion

		#endregion

		#region Public methods

		/// <summary>
		///     Запись настроек
		/// </summary>
		public SettingsFields WriteSettings(SettingsFields settFields, string path = null)
		{
			var ser = new XmlSerializer(typeof(SettingsFields));

			using (var writer = File.OpenWrite(path ?? settingsLocation))
			{
				ser.Serialize(writer, settFields);
			}

			return settFields;
		}

		/// <summary>
		///     Чтение настроек
		/// </summary>
		public SettingsFields ReadSettings(SettingsFields settFields)
		{
			var fd = new OpenFileDialog();
			fd.Filter = "Файлы настроек|*.xml";
			var result = fd.ShowDialog();
			if (result == true)
			{
				var ser = new XmlSerializer(typeof(SettingsFields));

				using (var reader = new FileStream(fd.FileName, FileMode.Open))
				{
					try
					{
						settFields = ser.Deserialize(reader) as SettingsFields;
					}
					catch
					{
						throw new IOException {Source = fd.FileName};
					}
				}
			}

			return settFields;
		}

		/// <summary>
		///     Фоновое чтение настроек
		/// </summary>
		public SettingsFields SilentReadSettings()
		{
			var ser = new XmlSerializer(typeof(SettingsFields));

			//Тоже, что и new FileStream(fd.FileName, FileMode.Open))
			using (var reader = File.OpenRead(settingsLocation))
			{
				try
				{
					return ser.Deserialize(reader) as SettingsFields;
				}
				catch
				{
					throw new IOException {Source = settingsLocation};
				}
			}
		}

		#endregion
	}
}