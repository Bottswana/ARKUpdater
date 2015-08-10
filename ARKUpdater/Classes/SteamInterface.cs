using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections;
using Newtonsoft.Json;
using KVLib;

namespace ARKUpdater.Classes
{
	abstract class SteamInterface
	{
		public abstract bool VerifySteamPath(string ExecutablePath);
		public abstract bool UpdateGame(string UpdateFile);

		public abstract int GetGameInformation(int appid);
		public abstract int GetGameBuildVersion(string ApplicationPath, ConsoleLogger Log);
	}

	class SteamInterfaceWindows : SteamInterface
	{
		private string _ExecutablePath;
		private string _QuerySteamCmd(string CommandString)
		{
			var ProcessElement = new ProcessStartInfo()
			{
				Arguments = string.Format("+login anonymous {0} +quit", CommandString),
				FileName = string.Format("{0}\\steamcmd.exe", _ExecutablePath),
				RedirectStandardOutput = true,
				UseShellExecute = false
			};

			var ReturnStr = new StringBuilder();
			using( var Proc = Process.Start(ProcessElement) )
			{
				while( !Proc.StandardOutput.EndOfStream ) 
				{
					ReturnStr.AppendLine( Proc.StandardOutput.ReadLine() );
				}
			}

			string ReturnData = ReturnStr.ToString();
			return ( (ReturnData != null) && (ReturnData.Length > 1) ) ? ReturnData : null;
		}

		public override bool VerifySteamPath(string ExecutablePath)
		{
			if( File.Exists(string.Format("{0}\\steamcmd.exe", ExecutablePath)) )
			{
				this._ExecutablePath = ExecutablePath;
				return true;
			}

			return false;
		}

		public override int GetGameInformation(int appid)
		{
			string ArgumentString = string.Format("+app_info_update 1 +app_info_print {0}", appid);
			string ReturnData = this._QuerySteamCmd(ArgumentString);
			if( ReturnData != null )
			{
				int FirstPos = ReturnData.IndexOf('{');
				int LastPos = ReturnData.LastIndexOf('}');

				string KVData = ReturnData.Substring(FirstPos, (LastPos - FirstPos)+1);
				var KV = KVLib.KVParser.ParseKeyValueText(KVData);

				// Begin VALve Inception.
				int PublicBuildID = 0;
				foreach( var Child in KV.Children )
				{
					if( Child.Key != "depots" || !Child.HasChildren ) continue;
					foreach( var Child2 in Child.Children )
					{
						if( Child2.Key != "branches" || !Child2.HasChildren ) continue;
						foreach( var Child3 in Child2.Children )
						{
							if( Child3.Key != "public" || !Child3.HasChildren ) continue;
							foreach( var Child4 in Child3.Children )
							{
								if( Child4.Key == "buildid" )
								{
									PublicBuildID = Child4.GetInt();
									break;
								}
							}
						}
					}
				}

				return PublicBuildID;
			}

			return -1;
		}

		public override bool UpdateGame(string UpdateFile)
		{
			throw new NotImplementedException();
		}

		public override int GetGameBuildVersion(string ApplicationPath, ConsoleLogger Log)
		{
			string ManifestPath = string.Format("{0}\\steamapps\\appmanifest_376030.acf", ApplicationPath);
			string KVData = null;

			try
			{
				TextReader reader = new StreamReader(ManifestPath);
				KVData = reader.ReadToEnd();
				reader.Close();
			}
			catch( Exception Ex )
			{
				Log.ConsolePrint(Classes.LogLevel.Error, "Error opening manifest file for server {0}", Ex.Message);
				return -1;
			}

			var KV = KVLib.KVParser.ParseKeyValueText(KVData);
			foreach( var Child in KV.Children )
			{
				if( Child.Key == "buildid" )
				{
					return Child.GetInt();
				}
			}

			return -1;
		}
	}

	class SteamInterfaceUnix : SteamInterface
	{
		private string _ExecutablePath;
		private string _QuerySteamCmd(string CommandString)
		{
			throw new NotImplementedException();
		}

		public override bool VerifySteamPath(string ExecutablePath)
		{
			throw new NotImplementedException();
		}

		public override int GetGameInformation(int appid)
		{
			throw new NotImplementedException();
		}

		public override bool UpdateGame(string UpdateFile)
		{
			throw new NotImplementedException();
		}

		public override int GetGameBuildVersion(string ApplicationPath, ConsoleLogger Log)
		{
			throw new NotImplementedException();
		}
	}
}
