using System;
using System.Linq;
using System.Text;
using System.Threading;
using ARKUpdater.Classes;
using System.Collections.Generic;

namespace ARKUpdater
{
	#region Program Stub
	class Program
	{
		static void Main(string[] args)
		{
			var System = new ARKUpdater();
			try { Console.WindowWidth = 140; } catch ( NotSupportedException ) {}

			// Because a little fun is compulsory
			string[] ConsoleTitles = {"Queen of Dragons. Oh, wait. Dinos", "Jack of all servers, Master of none.", "Server Happiness Assurance."};
			Console.Title = string.Format("ARKUpdater: {0}", ConsoleTitles[ (new Random()).Next(0, ConsoleTitles.Length-1) ]);

			Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e) {
				e.Cancel = true; System.Shutdown();
			};

			System.Run();
		}
	}
	#endregion Program Stub

	#region Global Methods
	static class Helpers
	{
		public static string Base64Decode(string EncodedData)
		{
			var ByteArray = Convert.FromBase64String(EncodedData);
			return Encoding.UTF8.GetString(ByteArray);
		}

		public static string FindLocalEndpoint(System.Net.IPEndPoint remote)
		{
			var testSocket = new System.Net.Sockets.Socket(remote.AddressFamily, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
			testSocket.Connect(remote);

			return ((System.Net.IPEndPoint)testSocket.LocalEndPoint).Address.ToString();
		}

		public static bool IsUnixPlatform()
		{
			int PlatformVersion = (int) System.Environment.OSVersion.Platform;
			return ( (PlatformVersion == 4) || (PlatformVersion == 6) || (PlatformVersion == 128) ) ? true : false;
		}

		public static void ExitWithError()
		{
			Console.WriteLine("Press any key to exit...");
			Console.ReadKey();

			System.Environment.Exit(1);
		}

		public static string GetApplicationVersion()
		{
			System.Reflection.Assembly execAssembly = System.Reflection.Assembly.GetCallingAssembly();
			System.Reflection.AssemblyName name = execAssembly.GetName();
			return string.Format("{0:0}.{1:0} (.NET {2})",
				name.Version.Major.ToString(),
				name.Version.Minor.ToString(),
				execAssembly.ImageRuntimeVersion
			);
		}

		static readonly DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0);
		public static DateTime FromUnixStamp(int secondsSinceepoch)
		{
			return epochStart.AddSeconds(secondsSinceepoch);
		}
		
		public static int ToUnixStamp(DateTime dateTime)
		{
			return (int)( dateTime - epochStart ).TotalSeconds;
		}

		public static int CurrentUnixStamp
		{
			get 
			{
				return (int)( DateTime.UtcNow - epochStart ).TotalSeconds;
			}
		}
	}

	class ServerClass
	{
		public int ProcessID;
		public int MinutesRemaining;

		public int LastUpdated;
		public int LastBackedUp;
		public SettingsLoader.ServerChild ServerData;

		public ServerClass(SettingsLoader.ServerChild Data)
		{
			this.ProcessID = 0;
			this.MinutesRemaining = 0;

			this.LastUpdated = 0;
			this.LastBackedUp = 0;

			this.ServerData = Data;
		}
	}
	#endregion Global Methods

	class ARKUpdater
	{
		public SettingsLoader ARKConfiguration;
		public ServerInterface Server;
		public SteamInterface Steam;
		public ConsoleLogger Log;

		private ManualResetEvent _Sleeper;
		private ServerClass[] _Servers;
		private bool _Running;

		public ARKUpdater()
		{
			this._Sleeper = new ManualResetEvent(false);
			this._Running = true;

			// Initialise Console Logging
			this.Log = new ConsoleLogger(LogLevel.Debug);
			Log.ConsolePrint(LogLevel.Info, "ARKUpdater Starting (Version: {0})", Helpers.GetApplicationVersion());
			
			// Load configuration from settings.json
			this.ARKConfiguration = SettingsLoader.LoadConfiguration("settings.json", Log);
			if( this.ARKConfiguration == null )
			{
				Helpers.ExitWithError();
			}

			// Configure Logger
			if( this.ARKConfiguration.LogLevel.Length > 0 )
			{
				try
				{
					var LogValue = (LogLevel) Enum.Parse(typeof(LogLevel), this.ARKConfiguration.LogLevel, true);
					this.Log.SetLogLevel(LogValue);
				}
				catch( ArgumentException )
				{
					Log.ConsolePrint(LogLevel.Error, "Invalid LogLevel in settings.json, using default (DEBUG)");
				}
			}

			// Configure Servers
			var TempList = new List<ServerClass>();
			foreach( var ServerData in this.ARKConfiguration.Servers )
			{
				TempList.Add( new ServerClass(ServerData) );
				Log.ConsolePrint(LogLevel.Debug, "Loaded server '{0}' from configuration", ServerData.GameServerName);
			}

			this._Servers = TempList.ToArray();

			// Initialise Interfaces
			if( Helpers.IsUnixPlatform() )
			{
				this.Steam = new SteamInterfaceUnix();
				this.Server = new ServerInterfaceUnix();
			}
			else
			{
				this.Steam = new SteamInterfaceWindows();
				this.Server = new ServerInterfaceWindows();
			}

			// Verify path to SteamCMD
			if( !this.Steam.VerifySteamPath(this.ARKConfiguration.SteamCMDPath) )
			{
				Log.ConsolePrint(LogLevel.Error, "Unable to find SteamCMD in provided path, please check the SteamCMD path in settings.json");
				Helpers.ExitWithError();
			}
		}

		public void Run()
		{
			// Get game information
			Log.ConsolePrint(LogLevel.Info, "Fetching public build number for `ARK: Survival Evolved` via SteamCMD");
			int BuildNumber = this.Steam.GetGameInformation(376030);
			if( BuildNumber == -1 )
			{
				Log.ConsolePrint(LogLevel.Error, "Unable to fetch Build ID from Steam, this may be an issue with your SteamCMD configuration.");
			}
			else
			{
				Log.ConsolePrint(LogLevel.Success, "Current Build ID for `ARK: Survival Evolved` is: {0}", BuildNumber);
			}

			// Initial Setup
			foreach( var Server in this._Servers )
			{
				Log.ConsolePrint(LogLevel.Debug, "Init Server '{0}'", Server.ServerData.GameServerName);
				int CurrentAppID = this.Steam.GetGameBuildVersion(Server.ServerData.GameServerPath, Log);

				if( (CurrentAppID < BuildNumber) && (CurrentAppID != -1) )
				{
					// Update Available
					Log.ConsolePrint(LogLevel.Info, "Server '{0}' has an update available, Updating before we start the server up.", Server.ServerData.GameServerName);
					this.Steam.UpdateGame(Server.ServerData.SteamUpdateScript);
				}
				else
				{
					// Start Server
					Log.ConsolePrint(LogLevel.Info, "Server '{0}' is up to date, Starting server.", Server.ServerData.GameServerName);
					var ProcessID = this.Server.StartServer(Server.ServerData, Log);
					Server.ProcessID = ProcessID;
				}
			}

			// Application Loop
			int LastUpdatePollTime = Helpers.CurrentUnixStamp;
			int PreviousBuild = BuildNumber;
			bool UpdatesQueued = false;

			while( this._Running )
			{
				if( (LastUpdatePollTime + (60 * this.ARKConfiguration.UpdatePollingInMinutes) < Helpers.CurrentUnixStamp) && !UpdatesQueued )
				{
					// Query Steam and check for updates to our servers
					Log.ConsolePrint(LogLevel.Debug, "Checking with Steam for updates to ARK (Current Build: {0})", BuildNumber);
					BuildNumber = this.Steam.GetGameInformation(376030);
					LastUpdatePollTime = Helpers.CurrentUnixStamp;
					Log.ConsolePrint(LogLevel.Debug, "New build number: {0}", BuildNumber);
				}

				if( PreviousBuild != BuildNumber )
				{
					foreach( var Server in this._Servers )
					{
						// Check each server for updates
						if( Server.MinutesRemaining != 0 ) continue;

						var ServerBuild = this.Steam.GetGameBuildVersion(Server.ServerData.GameServerPath, Log);
						if( (ServerBuild < BuildNumber) && (ServerBuild != -1) )
						{
							Server.MinutesRemaining = this.ARKConfiguration.UpdateWarningTimeInMinutes;

							string RCONWarning = string.Format(this.ARKConfiguration.Messages.ServerUpdateBroadcast, Server.MinutesRemaining);
							Log.ConsolePrint(LogLevel.Debug, "RCON: {0}", RCONWarning);
							//TODO: Send RCON Warning
						}
					}
				}

				if( this.ARKConfiguration.Backup.EnableBackup )
				{
					// Check for Last Backup
				}

				// ZzzzzZZZzzzZZZ
				PreviousBuild = BuildNumber;
				this._Sleeper.WaitOne( TimeSpan.FromSeconds(10) );
			}
		}

		public void Shutdown()
		{
			// Shutdown Main Thread
			Log.ConsolePrint(LogLevel.Info, "Shutting down");
			this._Running = false;

			this._Sleeper.Set();
			Thread.Sleep(500);
		}
	}
}
