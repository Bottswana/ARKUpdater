using System;
using SteamKit2;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections;
using ARKUpdater.Classes;

namespace ARKUpdater.Interfaces
{
	abstract class SteamInterface
	{
		protected ARKUpdater _Parent;
		protected string _ExecutablePath;
		public SteamInterface(ARKUpdater parent)
		{
			this._Parent = parent;
		}

		public abstract int GetGameInformation(uint appid);
		public abstract bool VerifySteamPath(string ExecutablePath);
		public abstract int GetGameBuildVersion(string ApplicationPath);
		public abstract void UpdateGame(string UpdateFile, bool ShowOutput);
	}

	class SteamInterfaceWindows : SteamInterface
	{
		public SteamInterfaceWindows(ARKUpdater parent) : base(parent) {}
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
		
		public override int GetGameInformation(uint appid)
		{
			using( var Steam3 = SteamKit.SpawnThread(_Parent) )
			{
				while( !Steam3.tClass.Ready && !Steam3.tClass.Failed ) Thread.Sleep(100);
				var WaitHandle = new AutoResetEvent(false);

				if( Steam3.tClass.Ready )
				{
					var returndata = -1;
					Steam3.tClass.RequestAppInfo(appid, (x) => {
						KeyValue appinfo = x.KeyValues;
						KeyValue DepotSection = appinfo.Children.Where( c => c.Name == "depots" ).FirstOrDefault();

						// Retrieve Public Branch
						KeyValue branches = DepotSection["branches"];
						KeyValue node = branches["public"];

						if( node != KeyValue.Invalid )
						{
							KeyValue buildid = node["buildid"];
							if( buildid != KeyValue.Invalid )
							{
								returndata = Convert.ToInt32(buildid.Value);
							}
						}

						// Clear wait handle
						WaitHandle.Set();
					});

					// Wait for Callback to finish
					WaitHandle.WaitOne();
					return returndata;
				}
			}

			return -1;
		}

		public override void UpdateGame(string UpdateFile, bool ShowOutput)
		{
			string ArgumentString = string.Format("+runscript {0}", UpdateFile);
			this._QuerySteamCmd(ArgumentString, (ShowOutput) ? false : true);
		}

		public override int GetGameBuildVersion(string ApplicationPath)
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
				_Parent.Log.ConsolePrint(Classes.LogLevel.Error, "Error opening manifest file for server {0}", Ex.Message);
				return -1;
			}

			var KV = KeyValue.LoadFromString(KVData);
			var Child = KV.Children.Where( x => x.Name == "buildid" ).First();
			return ( Child != null ) ? Convert.ToInt32(Child.Value) : -1;
		}
	}

	class SteamInterfaceUnix : SteamInterface
	{
		public SteamInterfaceUnix(ARKUpdater parent) : base(parent) {}
		private string _QuerySteamCmd(string CommandString)
		{
			throw new NotImplementedException();
		}

		public override bool VerifySteamPath(string ExecutablePath)
		{
			throw new NotImplementedException();
		}

		public override int GetGameInformation(uint appid)
		{
			throw new NotImplementedException();
		}

		public override void UpdateGame(string UpdateFile, bool ShowOutput)
		{
			throw new NotImplementedException();
		}

		public override int GetGameBuildVersion(string ApplicationPath)
		{
			throw new NotImplementedException();
		}
	}
}
