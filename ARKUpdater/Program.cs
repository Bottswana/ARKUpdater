using System;
using System.Linq;
using System.Text;
using System.Threading;
using ARKUpdater.Classes;
using ARKUpdater.Interfaces;
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
			Console.Title = string.Format("ARKUpdater: {0}", ConsoleTitles[ (new Random()).Next(0, ConsoleTitles.Length) ]);

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
			this.MinutesRemaining = -1;

			this.LastUpdated = 0;
			this.LastBackedUp = 0;

			this.ServerData = Data;
		}
	}
	#endregion Global Methods

	class ARKUpdater
	{
		public SettingsLoader ARKConfiguration;
		public ServerClass[] Servers;
		public ConsoleLogger Log;

		public BackupInterface BackupInt;
		public ServerInterface ServerInt;
		public SteamInterface SteamInt;

		private ManualResetEvent _Sleeper;
		private bool _Running;

		public ARKUpdater()
		{
			this._Sleeper = new ManualResetEvent(false);
			this._Running = true;

			// Initialise Console Logging
			this.Log = new ConsoleLogger(LogLevel.Debug);
			this.Log.ConsolePrint(LogLevel.Info, "ARKUpdater Starting (Version: {0})", Helpers.GetApplicationVersion());
			
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
					this.Log.ConsolePrint(LogLevel.Error, "Invalid LogLevel in settings.json, using default (DEBUG)");
				}
			}

			// Configure Servers
			var TempList = new List<ServerClass>();
			foreach( var ServerData in this.ARKConfiguration.Servers )
			{
				TempList.Add( new ServerClass(ServerData) );
				this.Log.ConsolePrint(LogLevel.Debug, "Loaded server '{0}' from configuration", ServerData.GameServerName);
			}

			this.Servers = TempList.ToArray();

			// Initialise Interfaces
			if( Helpers.IsUnixPlatform() )
			{
				this.ServerInt = new ServerInterfaceUnix(this);
				this.BackupInt = new BackupInterfaceUnix(this);
				this.SteamInt = new SteamInterfaceUnix(this);
			}
			else
			{
				this.ServerInt = new ServerInterfaceWindows(this);
				this.BackupInt = new BackupInterfaceWindows(this);
				this.SteamInt = new SteamInterfaceWindows(this);
			}

			// Verify path to SteamCMD
			if( !this.SteamInt.VerifySteamPath(this.ARKConfiguration.SteamCMDPath) )
			{
				this.Log.ConsolePrint(LogLevel.Error, "Unable to find SteamCMD in provided path, please check the SteamCMD path in settings.json");
				Helpers.ExitWithError();
			}
		}

		public void Run()
		{
			// Get game information
			Log.ConsolePrint(LogLevel.Info, "Fetching public build number for `ARK: Survival Evolved` from Steam3");
			int BuildNumber = SteamInt.GetGameInformation(376030);
			if( BuildNumber == -1 )
			{
				Log.ConsolePrint(LogLevel.Error, "Unable to fetch Build ID from Steam, this may be an issue with your internet connection.");
			}
			else
			{
				Log.ConsolePrint(LogLevel.Success, "Current Build ID for `ARK: Survival Evolved` is: {0}", BuildNumber);
			}

			// Initial Setup
			foreach( var Server in Servers )
			{
				Log.ConsolePrint(LogLevel.Debug, "Init Server '{0}'", Server.ServerData.GameServerName);
				int CurrentAppID = SteamInt.GetGameBuildVersion(Server.ServerData.GameServerPath);

				Server.LastBackedUp = Helpers.CurrentUnixStamp;
				if( (CurrentAppID < BuildNumber) && (CurrentAppID != -1) )
				{
					// Update Available
					if( !ServerInt.ServerRunning(Server.ServerData) )
					{
						// Update Server
						Log.ConsolePrint(LogLevel.Info, "Server '{0}' has an update available, Updating before we start the server up.", Server.ServerData.GameServerName);
						SteamInt.UpdateGame(Server.ServerData.SteamUpdateScript, ARKConfiguration.ShowSteamUpdateInConsole);
						Log.ConsolePrint(LogLevel.Success, "Server '{0}' update successful, starting server.", Server.ServerData.GameServerName);
					}

					var ProcessID = ServerInt.StartServer(Server.ServerData);
					Server.ProcessID = ProcessID;
				}
				else
				{
					// Start Server
					Log.ConsolePrint(LogLevel.Info, "Server '{0}' is up to date, Starting server.", Server.ServerData.GameServerName);
					var ProcessID = ServerInt.StartServer(Server.ServerData);
					Server.ProcessID = ProcessID;
				}
			}

			// Application Loop
			int LastUpdatePollTime = Helpers.CurrentUnixStamp;
			int LastMinutePollTime = Helpers.CurrentUnixStamp;

			int PreviousBuild = BuildNumber;
			bool UpdatesQueued = false;
			while( _Running )
			{
				if( (LastUpdatePollTime + (60 * ARKConfiguration.UpdatePollingInMinutes) < Helpers.CurrentUnixStamp) && !UpdatesQueued )
				{
					// Query Steam and check for updates to our servers
					Log.ConsolePrint(LogLevel.Debug, "Checking with Steam for updates to ARK (Current Build: {0})", BuildNumber);
					BuildNumber = SteamInt.GetGameInformation(376030);

					LastUpdatePollTime = Helpers.CurrentUnixStamp;
					if( BuildNumber > PreviousBuild ) Log.ConsolePrint(LogLevel.Info, "A new build of `ARK: Survival Evolved` is available. Build number: {0}", BuildNumber);
				}

				bool MinutePassed = (LastMinutePollTime+60 <= Helpers.CurrentUnixStamp) ? true : false;
				foreach( var Server in Servers )
				{
					if( Server.MinutesRemaining == -1 )
					{
						// Check each server for updates
						var ServerBuild = SteamInt.GetGameBuildVersion(Server.ServerData.GameServerPath);
						if( (ServerBuild < BuildNumber) && (ServerBuild != -1) )
						{
							// Check for Player Requirement
							if( ARKConfiguration.PostponeUpdateWhenPlayersHigherThan > 0 )
							{
								using( var Query = new SrcQuery("127.0.0.1", Server.ServerData.QueryPort) )
								{
									try
									{
										var QueryData = Query.QueryServer();
										if( Convert.ToInt32(QueryData["CurrentPlayers"]) > ARKConfiguration.PostponeUpdateWhenPlayersHigherThan )
										{
											Log.ConsolePrint(LogLevel.Info, "Available update for server '{0}' postponed. Players online: {1}", Server.ServerData.GameServerName, QueryData["CurrentPlayers"]);
											continue;
										}
									} catch (QueryException) {}
								}
							}

							// Schedule update on server with the user defined interval.
							Log.ConsolePrint(LogLevel.Success, "Server '{0}' queued for update successfully. Update will begin in {1} minute(s)", Server.ServerData.GameServerName, ARKConfiguration.UpdateWarningTimeInMinutes);
							Server.MinutesRemaining = ARKConfiguration.UpdateWarningTimeInMinutes;
							if( !UpdatesQueued ) UpdatesQueued = true;

							// Send warning message to Server
							using( var RCONClient = new ArkRCON("127.0.0.1", Server.ServerData.RCONPort) )
							{
								try
								{
									RCONClient.Authenticate(Server.ServerData.ServerAdminPassword);
									string RCONWarning = string.Format(ARKConfiguration.Messages.ServerUpdateBroadcast, Server.MinutesRemaining);
									RCONClient.ExecuteCommand(string.Format("serverchat {0}", RCONWarning));
								} catch( QueryException ) {}
							}
						}
					}

					if( MinutePassed && (Server.MinutesRemaining >= 1) )
					{
						Log.ConsolePrint(LogLevel.Debug, "Ticking update minute counter from {0} to {1} for server '{2}'", Server.MinutesRemaining, Server.MinutesRemaining-1, Server.ServerData.GameServerName);
						Server.MinutesRemaining = (Server.MinutesRemaining - 1);

						// Send warning message to Server
						if( Server.MinutesRemaining != 0 )
						{
							using( var RCONClient = new ArkRCON("127.0.0.1", Server.ServerData.RCONPort) )
							{
								try
								{
									RCONClient.Authenticate(Server.ServerData.ServerAdminPassword);
									string RCONWarning = string.Format(ARKConfiguration.Messages.ServerUpdateBroadcast, Server.MinutesRemaining);
									RCONClient.ExecuteCommand(string.Format("serverchat {0}", RCONWarning));
								}
								catch( QueryException Ex )
								{
									Log.ConsolePrint(LogLevel.Error, Ex.Message);
								}
							}
						}
					}

					if( Server.MinutesRemaining == 0 )
					{
						// Send warning message to Server
						using( var RCONClient = new ArkRCON("127.0.0.1", Server.ServerData.RCONPort) )
						{
							try
							{
								RCONClient.Authenticate(Server.ServerData.ServerAdminPassword);
								RCONClient.ExecuteCommand(string.Format("serverchat {0}", ARKConfiguration.Messages.ServerShutdownBroadcast));
							}
							catch( QueryException Ex )
							{
								Log.ConsolePrint(LogLevel.Error, Ex.Message);
							}
						}

						_Sleeper.WaitOne( TimeSpan.FromSeconds(2) );

						// Shutdown Server
						Log.ConsolePrint(LogLevel.Info, "Server '{0}' will now be shutdown for an update", Server.ServerData.GameServerName);
						ServerInt.StopServer(Server.ServerData);

						Log.ConsolePrint(LogLevel.Debug, "Server '{0}' now waiting for process exit", Server.ServerData.GameServerName);
						while( Server.ProcessID != 0 ) continue; // This is set to 0 by our Exit event on the process in ServerInterface.cs

						// Update Server
						SteamInt.UpdateGame(Server.ServerData.SteamUpdateScript, ARKConfiguration.ShowSteamUpdateInConsole);
						_Sleeper.WaitOne( TimeSpan.FromSeconds(2) );

						// Restart Server
						Log.ConsolePrint(LogLevel.Info, "Server '{0}' update complete, restarting server.", Server.ServerData.GameServerName);
						var ProcessID = ServerInt.StartServer(Server.ServerData);

						Server.MinutesRemaining = -1;
						Server.ProcessID = ProcessID;
					}

					if( ARKConfiguration.Backup.EnableBackup )
					{
						// Check for Last Backup
						if( (Server.LastBackedUp + (60 * ARKConfiguration.Backup.BackupIntervalInMinutes) < Helpers.CurrentUnixStamp) && !UpdatesQueued )
						{
							Server.LastBackedUp = Helpers.CurrentUnixStamp;
							BackupInt.BackupServer(Server.ServerData);
							BackupInt.CleanBackups(Server.ServerData);
						}
					}
				}

				// Cleanup before next loop
				int NumberServersUpdateRemaining = Servers.Where(x => x.MinutesRemaining != -1).Count();
				if( NumberServersUpdateRemaining <= 0 && UpdatesQueued ) UpdatesQueued = false;

				PreviousBuild = BuildNumber;
				if( MinutePassed ) LastMinutePollTime = Helpers.CurrentUnixStamp;
				_Sleeper.WaitOne( TimeSpan.FromSeconds(5) );
			}
		}

		public void Shutdown()
		{
			// Shutdown Main Thread
			Log.ConsolePrint(LogLevel.Info, "Shutting down");
			_Running = false;

			_Sleeper.Set();
			Thread.Sleep(500);
		} 
	}
}