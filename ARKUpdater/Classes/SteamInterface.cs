using KVLib;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections;

namespace ARKUpdater.Classes
{
	abstract class SteamInterface
	{
		public abstract bool VerifySteamPath(string ExecutablePath);
		public abstract void UpdateGame(string UpdateFile, bool ShowOutput);

		public abstract int GetGameInformation(int appid);
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
			if( Return )
			{
				var ReturnStr = new StringBuilder();
				using( var Proc = Process.Start(ProcessElement) )
				{
					while( !Proc.StandardOutput.EndOfStream ) 
					{
						ReturnStr.AppendLine( Proc.StandardOutput.ReadLine() );
					}
				}

				ReturnData = ReturnStr.ToString();
			}
			else
			{
				using( var Proc = Process.Start(ProcessElement) )
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

		public override int GetGameInformation(int appid)
		{
			string ArgumentString = string.Format("+login anonymous +app_info_update 1 +app_info_print {0} +quit", appid);
			string ReturnData = this._QuerySteamCmd(ArgumentString);
			if( ReturnData != null )
			{
				int FirstPos = ReturnData.IndexOf('{');
				int LastPos = ReturnData.LastIndexOf('}');

				string KVData = ReturnData.Substring(FirstPos, (LastPos - FirstPos)+1);
				var KV = KVLib.KVParser.ParseKeyValueText(KVData);

				// Retrieve BuildID using Linq
				var Child = KV.Children.Where( x => x.Key == "depots" ).First()
							  .Children.Where( x => x.Key == "branches" ).First()
							  .Children.Where( x => x.Key == "public" ).First()
							  .Children.Where( x => x.Key == "buildid" ).First();

				return Child.GetInt();
			}

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
			KeyValue Child = KV.Children.Where( x => x.Key == "buildid" ).First();
			return ( Child != null ) ? Child.GetInt() : -1;
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
