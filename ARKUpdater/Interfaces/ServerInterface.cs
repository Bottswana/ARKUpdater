using System;
using System.IO;
using System.Text;
using ARKUpdater.Classes;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices; 

namespace ARKUpdater.Interfaces
{
	abstract class ServerInterface
	{
		protected ARKUpdater _Parent;
		protected Dictionary<Process, SettingsLoader.ServerChild> _ProcessDict;
		public ServerInterface(ARKUpdater Parent)
		{
			this._ProcessDict = new Dictionary<Process, SettingsLoader.ServerChild>();
			this._Parent = Parent;
		}

		public abstract bool StopServer(SettingsLoader.ServerChild ServerData);
		public abstract int StartServer(SettingsLoader.ServerChild ServerData);
		public abstract bool ServerRunning(SettingsLoader.ServerChild ServerData);
	}

	class ServerInterfaceWindows : ServerInterface
	{
		#region W32API Imports
		static class NativeMethods
		{
			[DllImport("user32.dll", CharSet = CharSet.Unicode)]
			public static extern bool SetWindowText(IntPtr hWnd, string text);
		}
		#endregion W32API Imports

		public ServerInterfaceWindows(ARKUpdater parent) : base(parent) {}
		public override bool StopServer(SettingsLoader.ServerChild ServerData)
		{
			Process thisProcess = null;
			foreach( var tProcess in _ProcessDict )
			{
				if( tProcess.Value != ServerData ) continue;
				thisProcess = tProcess.Key;
			}

			if( thisProcess == null ) return false;
			File.Delete(string.Format("{0}\\server.pid", ServerData.GameServerPath));

			thisProcess.CloseMainWindow();
			return true;
		}

		public override int StartServer(SettingsLoader.ServerChild ServerData)
		{
			// Check for existing process
			var ProcessID = _ReadProcessFile(ServerData.GameServerPath);
			if( ProcessID != -1 )
			{
				try
				{
					var Proc = Process.GetProcessById(ProcessID);
					if( !Proc.HasExited && (Proc.Id == ProcessID) )
					{
						// Listener for Exit Event
						_ProcessDict.Add(Proc, ServerData);
						Proc.EnableRaisingEvents = true;
						Proc.Exited += new EventHandler(_ProcessExited);

						_Parent.Log.ConsolePrint(LogLevel.Debug, "Re-used existing Server Process with ID {0} (From PID File)", Proc.Id);
						return Proc.Id;
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

			try
			{
				// Write process to pid file
				var Proc = Process.Start(ProcessData);
				_WriteProcessFile(ServerData.GameServerPath, Proc.Id);

				// Listener for Exit Event
				_ProcessDict.Add(Proc, ServerData);
				Proc.EnableRaisingEvents = true;
				Proc.Exited += new EventHandler(_ProcessExited);

				// Set Window Title
				System.Threading.Thread.Sleep(2000);
				NativeMethods.SetWindowText(Proc.MainWindowHandle, string.Format("ARK: {0} (Managed by ARKUpdater)", ServerData.GameServerName));
				
				// Return with Process ID
				_Parent.Log.ConsolePrint(LogLevel.Debug, "Spawned new Server Process with ID {0}", Proc.Id);
				return Proc.Id;
			}
			catch( Exception Ex )
			{
				_Parent.Log.ConsolePrint(LogLevel.Error, "Unable to spawn new server process: {0}", Ex.Message);
				return -1;
			}
		}

		public override bool ServerRunning(SettingsLoader.ServerChild ServerData)
		{
			var ProcessID = _ReadProcessFile(ServerData.GameServerPath);
			if( ProcessID != -1 )
			{
				try
				{
					var Proc = Process.GetProcessById(ProcessID);
					if( !Proc.HasExited && (Proc.Id == ProcessID) )
					{
						return true;
					}
				} catch( System.ArgumentException ) {}
			}

			return false;
		}

		private void _ProcessExited(object sender, EventArgs e)
		{
			Process thisProcess = (Process) sender;

			if( !_ProcessDict.ContainsKey(thisProcess) ) return;
			foreach( var Server in _Parent.Servers )
			{
				if( Server.ServerData != _ProcessDict[thisProcess] ) continue;
				if( Server.MinutesRemaining != 0 )
				{
					// If Minutes Remaining is not 0, we are not expecting the server to send us an exit signal, so this is probably a crash. (or someone exiting the server).
					// In which case, we will restart the server again.
					_Parent.Log.ConsolePrint(LogLevel.Warning, "Server '{0}' exited unexpectedly. Restarting server..", Server.ServerData.GameServerName);
					StartServer(Server.ServerData);
					break;
				}

				// We are expecting the server to send us an exit signal, which means we have an update pending for the server (holding the main loop)
				// Set processid to 0 to signal that the server has exited and we are free to start the update process
				_Parent.Log.ConsolePrint(LogLevel.Debug, "Server '{0}' completed shutdown, marking as ready for update", Server.ServerData.GameServerName);
				Server.ProcessID = 0;
				break;
			}

			// Clean-up
			_ProcessDict.Remove(thisProcess);
			thisProcess.Dispose();
		}

		private void _WriteProcessFile(string ServerDir, int Pid)
		{
			using( var FileName = new StreamWriter(string.Format("{0}\\server.pid", ServerDir), false) )
			{
				FileName.Write(Pid);
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
			catch( Exception )
			{
				return -1;
			}
		}
	}

	class ServerInterfaceUnix : ServerInterface
	{
		public ServerInterfaceUnix(ARKUpdater parent) : base(parent) {}
		public override int StartServer(SettingsLoader.ServerChild ServerData)
		{
			throw new NotImplementedException();
		}

		public override bool StopServer(SettingsLoader.ServerChild ServerData)
		{
			throw new NotImplementedException();
		}

		public override bool ServerRunning(SettingsLoader.ServerChild ServerData)
		{
			throw new NotImplementedException();
		}
	}
}
