using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices; 

namespace ARKUpdater.Classes
{
	abstract class ServerInterface
	{
		public abstract int StartServer(SettingsLoader.ServerChild ServerData, ConsoleLogger Log);
	}

	class ServerInterfaceWindows : ServerInterface
	{
		#region W32API Imports
		[DllImport("user32.dll")]
		public static extern bool SetWindowText(IntPtr hWnd, string text);
		#endregion W32API Imports

		#region Constructor
		private Dictionary<Process, SettingsLoader.ServerChild> _ProcessDict;
		public ServerInterfaceWindows()
		{
			this._ProcessDict = new Dictionary<Process, SettingsLoader.ServerChild>();
		}
		#endregion Constructor

		public override int StartServer(SettingsLoader.ServerChild ServerData, ConsoleLogger Log)
		{
			// Check for existing process
			var ProcessID = this._ReadProcessFile(ServerData.GameServerPath);
			if( ProcessID != -1 )
			{
				try
				{
					using( var Proc = Process.GetProcessById(ProcessID) )
					{
						if( !Proc.HasExited && (Proc.Id == ProcessID) )
						{
							// Listener for Exit Event
							this._ProcessDict.Add(Proc, ServerData);
							Proc.Exited += _ProcessExited;

							Log.ConsolePrint(LogLevel.Debug, "Re-used existing Server Process with ID {0} (From PID File)", Proc.Id);
							return Proc.Id;
						}
					}
				} catch( System.ArgumentException ) {}
			}

			// Start new process
			var QueryString = new StringBuilder();
			QueryString.Append(string.Format("{0}?listen?MaxPlayers={1}?ServerName={2}?QueryPort={3}?RCONEnabled=true?RCONPort={4}?Port={5}?ServerAdminPassword={6}?ServerPVE={7}",
				ServerData.GameServerMap,
				ServerData.MaxPlayers,
				ServerData.GameServerName,
				ServerData.QueryPort,
				ServerData.RCONPort,
				ServerData.Port,
				ServerData.ServerAdminPassword,
				ServerData.ServerPVE
			));

			if( ServerData.ServerPassword.Length > 1 ) QueryString.Append("?ServerPassword="+ServerData.ServerPassword);
			if( ServerData.ServerParameters.Count >= 1 )
			{
				foreach( var Param in ServerData.ServerParameters )
				{
					QueryString.Append(string.Format("?{0}={1}", Param.Key, Param.Value));
				}
			}

			var ProcessData = new ProcessStartInfo()
			{
				WorkingDirectory = string.Format("{0}\\ShooterGame\\Binaries\\Win64", ServerData.GameServerPath),
				FileName = "ShooterGameServer.exe",
				Arguments = QueryString.ToString(),

				CreateNoWindow = false,
				UseShellExecute = true
			};

			using(var Proc = Process.Start(ProcessData))
			{
				// Write process to pid file
				this._WriteProcessFile(ServerData.GameServerPath, Proc.Id);

				// Listener for Exit Event
				this._ProcessDict.Add(Proc, ServerData);
				Proc.Exited += _ProcessExited;

				// Set Window Title
				System.Threading.Thread.Sleep(2000);
				SetWindowText(Proc.MainWindowHandle, string.Format("ARK: {0} (Managed by ARKUpdater)", ServerData.GameServerName));
				
				// Return with Process ID
				Log.ConsolePrint(LogLevel.Debug, "Spawned new Server Process with ID {0}", Proc.Id);
				return Proc.Id;
			}
		}

		private void _ProcessExited(object sender, EventArgs e)
		{
			Console.WriteLine("Test");
		}

		private void _WriteProcessFile(string ServerDir, int Pid)
		{
			using( var FileName = new StreamWriter(string.Format("{0}\\server.pid", ServerDir), false) )
			{
				FileName.Write(Pid);
				FileName.Close();
			}
		}

		private int _ReadProcessFile(string ServerDir)
		{
			try
			{
				using( var FileName = new StreamReader(string.Format("{0}\\server.pid", ServerDir)) )
				{
					return Convert.ToInt32( FileName.ReadToEnd() );
				}
			}
			catch( FileNotFoundException )
			{
				return -1;
			}
		}
	}

	class ServerInterfaceUnix : ServerInterface
	{
		public override int StartServer(SettingsLoader.ServerChild ServerData, ConsoleLogger Log)
		{
			throw new NotImplementedException();
		}
	}
}
