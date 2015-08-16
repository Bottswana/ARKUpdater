using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ARKUpdater.Classes
{
	class SettingsLoader
	{
		#region Static Methods
		public static SettingsLoader LoadConfiguration(string ConfigurationName, ConsoleLogger Log)
		{
			string json = null;
			SettingsLoader CastData = null;

			try
			{
				TextReader reader = new StreamReader(ConfigurationName);
				json = reader.ReadToEnd();
				reader.Close();
			}
			catch( Exception Ex )
			{
				Log.ConsolePrint(Classes.LogLevel.Error, "Error opening settings.json! {0}", Ex.Message);
				return null;
			}

			try
			{
				CastData = JsonConvert.DeserializeObject<SettingsLoader>(json);
			}
			catch( Newtonsoft.Json.JsonReaderException Ex )
			{
				Log.ConsolePrint(Classes.LogLevel.Error, "Error reading settings.json! {0}", Ex.Message);
				return null;
			}

			return CastData;
		}

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			var fields = this.GetType().GetProperties();

			foreach( var propInfo in fields )
			{
				sb.AppendFormat("{0} = {1}" + Environment.NewLine, propInfo.Name, propInfo.GetValue(this, null));
			}

			return sb.ToString();
		}
		#endregion Static Methods

		#region Configuration Top-Level
		public int PostponeUpdateWhenPlayersHigherThan { get; set; }
		public int UpdateWarningTimeInMinutes { get; set; }
		public int UpdatePollingInMinutes { get; set; }
		
		public string LogLevel { get; set; }
		public string SteamCMDPath { get; set; }

		public bool ShowSteamUpdateInConsole { get; set; }
		public bool UseServerNameInINIFile { get; set; }

		public BackupChild Backup { get; set; }
		public MessageChild Messages { get; set; }
		public ServerChild[] Servers { get; set; }
		#endregion Configuration Top-Level

		#region Child Classes
		public class BackupChild
		{
			public bool EnableBackup { get; set; }
			public int BackupIntervalInMinutes { get; set; }
			public int NumberOfBackupsToKeepPerServer {  get; set; }
		}

		public class MessageChild
		{
			public string ServerUpdateBroadcast { get; set; }
			public string ServerBackupBroadcast { get; set; }
			public string ServerShutdownBroadcast { get; set; }
		}

		public class ServerChild
		{
			public string ServerAdminPassword { get; set; }
			public string SteamUpdateScript { get; set; }
			public string BackupDirectory { get; set; }
			public string GameServerPath { get; set; }
			public string GameServerName { get; set; }
			public string ServerPassword { get; set; }
			public string GameServerMap { get; set; }

			public int MaxPlayers { get; set; }
			public int QueryPort { get; set; }
			public int RCONPort { get; set; }
			public int Port { get; set; }

			public bool ServerPVE { get; set; }
			public Dictionary<string, string> ServerParameters { get; set; }
		}
		#endregion Child Classes
	}
}
