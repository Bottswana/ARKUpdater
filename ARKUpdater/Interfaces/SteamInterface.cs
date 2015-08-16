using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using SteamKit2;
using System.Diagnostics;
using System.Collections;
using ARKUpdater.Classes;

namespace ARKUpdater.Interfaces
{
	abstract class SteamInterface
	{
		public abstract bool VerifySteamPath(string ExecutablePath);
		public abstract void UpdateGame(string UpdateFile, bool ShowOutput);

		public abstract int GetGameInformation(uint appid, ARKUpdater parent);
		public abstract int GetGameBuildVersion(string ApplicationPath, ConsoleLogger Log);
	}

	class SteamInterfaceWindows : SteamInterface
	{
		private string _ExecutablePath;
		private string _QuerySteamCmd(string CommandString, bool Return = true)
		{
			var ProcessElement = new ProcessStartInfo()
			{
				FileName = string.Format("{0}\\steamcmd.exe", _ExecutablePath),
				Arguments = CommandString,

				RedirectStandardOutput = Return,
				UseShellExecute = false
			};

			string ReturnData = null;
			using( var Proc = Process.Start(ProcessElement) )
			{
				if( Return )
				{
					var ReturnStr = new StringBuilder();
					while( !Proc.StandardOutput.EndOfStream ) 
					{
						string line = Proc.StandardOutput.ReadLine();
						ReturnStr.AppendLine( line );
						Console.WriteLine(line);
					}

					ReturnData = ReturnStr.ToString();
				}
				else
				{
					Proc.WaitForExit();
				}
			}

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
		
		/*
        public KeyValue GetSteam3AppSection( uint appId, EAppInfoSection section )
        {
            Steam3Session steam3 = new Steam3Session();
			while( !steam3.bConnected ) continue;
			


            if (steam3 == null || steam3.AppInfo == null)
            {
                return null;
            }

            SteamApps.PICSProductInfoCallback.PICSProductInfo app;
            if ( !steam3.AppInfo.TryGetValue( appId, out app ) || app == null )
            {
                return null;
            }

            KeyValue appinfo = app.KeyValues;
            string section_key;

            switch (section)
            {
                case EAppInfoSection.Common:
                    section_key = "common";
                    break;
                case EAppInfoSection.Extended:
                    section_key = "extended";
                    break;
                case EAppInfoSection.Config:
                    section_key = "config";
                    break;
                case EAppInfoSection.Depots:
                    section_key = "depots";
                    break;
                default:
                    throw new NotImplementedException();
            }
            
            KeyValue section_kv = appinfo.Children.Where(c => c.Name == section_key).FirstOrDefault();
            return section_kv;
        } */

		public void CallbackFn(SteamApps.PICSProductInfoCallback.PICSProductInfo data)
		{
            KeyValue appinfo = data.KeyValues;
            KeyValue DepotSection = appinfo.Children.Where( c => c.Name == "depots" ).FirstOrDefault();

			// Retrieve Public Branch
			KeyValue branches = DepotSection["branches"];
            KeyValue node = branches["public"];

            if( node == KeyValue.Invalid )
			{
				return;
			}

            KeyValue buildid = node["buildid"];

            if( buildid == KeyValue.Invalid )
			{
				return;
			}

			var ready = Convert.ToInt32(buildid.Value);
		}

		public override int GetGameInformation(uint appid, ARKUpdater parent)
		{
			//   KeyValue depots = GetSteam3AppSection(appid, EAppInfoSection.Depots);

			// Connect to Steam3 and wait for connection to establish
			var Steam3 = SteamKit.SpawnThread(parent);
			while( !Steam3.tClass.Ready && !Steam3.tClass.Failed ) continue;

			if( !Steam3.tClass.Ready )
			{
				parent.Log.ConsolePrint(LogLevel.Debug, "Unable to successfully retrieve build info. No connection to Steam3");
				return -1;
			}

			Steam3.tClass.RequestAppInfo(appid, CallbackFn);
			while(true) continue;

			// Retrieve Depot information for our appid
			/*
            SteamApps.PICSProductInfoCallback.PICSProductInfo app;
            if ( !Steam3.tClass.AppInfo.TryGetValue(appid, out app) || app == null )
            {
                return -1;
            }

            KeyValue appinfo = app.KeyValues;
            KeyValue DepotSection = appinfo.Children.Where( c => c.Name == "depots" ).FirstOrDefault();

			// Retrieve Public Branch
			KeyValue branches = DepotSection["branches"];
            KeyValue node = branches["public"];

            if( node == KeyValue.Invalid )
			{
				return -1;
			}

            KeyValue buildid = node["buildid"];

            if( buildid == KeyValue.Invalid )
			{
				return -1;
			}

			return Convert.ToInt32(buildid.Value */

			/*
			 * This method is currently broken due to the SteamCMD stdredirect buffer error on windows.
			 * Superseeded with HTTP request to arkdedicated for now, but left here for legacy reasons.
			 * 
			string ArgumentString = string.Format("+login anonymous +app_info_update 1 +app_info_print {0} +quit", appid);
			string ReturnData = this._QuerySteamCmd(ArgumentString);
			if( ReturnData != null )
			{
				int FirstPos = ReturnData.IndexOf('{');
				int LastPos = ReturnData.LastIndexOf('}');
				string KVData = ReturnData.Substring(FirstPos, (LastPos - FirstPos)+1);

				try
				{
					// Parse Valve KeyValues Format
					var KV = KVLib.KVParser.ParseKeyValueText(KVData);

					// Retrieve BuildID using Linq
					var Child = KV.Children.Where( x => x.Key == "depots" ).First()
								  .Children.Where( x => x.Key == "branches" ).First()
								  .Children.Where( x => x.Key == "public" ).First()
								  .Children.Where( x => x.Key == "buildid" ).First();
					return Child.GetInt();
				} 
				catch( Sprache.ParseException )
				{
					return -1;
				}
			}*/
			

			return -1;
		}

		public override void UpdateGame(string UpdateFile, bool ShowOutput)
		{
			string ArgumentString = string.Format("+runscript {0}", UpdateFile);
			this._QuerySteamCmd(ArgumentString, (ShowOutput) ? false : true);
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
			//KeyValue Child = KV.Children.Where( x => x.Key == "buildid" ).First();
			//return ( Child != null ) ? Child.GetInt() : -1;
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

		public override int GetGameInformation(uint appid, ARKUpdater parent)
		{
			throw new NotImplementedException();
		}

		public override void UpdateGame(string UpdateFile, bool ShowOutput)
		{
			throw new NotImplementedException();
		}

		public override int GetGameBuildVersion(string ApplicationPath, ConsoleLogger Log)
		{
			throw new NotImplementedException();
		}
	}
}
