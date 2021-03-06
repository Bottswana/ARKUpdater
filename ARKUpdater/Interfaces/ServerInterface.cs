﻿using System;
using System.IO;
using System.Text;
using System.Threading;
using ARKUpdater.Classes;
using System.Diagnostics;
using System.Collections;
using System.Threading.Tasks;
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

		public abstract int StartServer(SettingsLoader.ServerChild ServerData);
		public abstract bool ServerRunning(SettingsLoader.ServerChild ServerData);
		public abstract bool StopServer(SettingsLoader.ServerChild ServerData, AutoResetEvent ResetEvent);
	}

	class ServerInterfaceWindows : ServerInterface
	{
		#region Window Text Code
		static class NativeMethods
		{
			[DllImport("user32.dll", CharSet = CharSet.Unicode)]
			public static extern bool SetWindowText(IntPtr hWnd, string text);
		}

		private async Task _UpdateWindow(Process Proc, string Text)
		{
			await Task.Delay(5000);
			NativeMethods.SetWindowText(Proc.MainWindowHandle, Text);
		}
		#endregion Window Text Code

		public ServerInterfaceWindows(ARKUpdater parent) : base(parent) {}
		public override bool StopServer(SettingsLoader.ServerChild ServerData, AutoResetEvent ResetEvent)
		{
			Process thisProcess = null;
			foreach( var tProcess in _ProcessDict )
			{
				if( tProcess.Value != ServerData ) continue;
				thisProcess = tProcess.Key;
			}

			if( thisProcess == null ) return false;
			thisProcess.Exited += new EventHandler( (object sender, EventArgs e) => {
				ResetEvent.Set();
			});

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
			QueryString.Append(string.Format("{0}?listen?MaxPlayers={1}?QueryPort={2}?RCONEnabled=true?RCONPort={3}?Port={4}?ServerAdminPassword={5}?ServerPVE={6}",
				ServerData.GameServerMap,
				ServerData.MaxPlayers,
				ServerData.QueryPort,
				ServerData.RCONPort,
				ServerData.Port,
				ServerData.ServerAdminPassword,
				ServerData.ServerPVE
			));

			if( ServerData.ServerPassword.Length > 1 ) QueryString.Append("?ServerPassword="+ServerData.ServerPassword);
			if( !_Parent.ARKConfiguration.UseServerNameInINIFile ) QueryString.Append("?ServerName="+ServerData.GameServerName);
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
				Task tResult = this._UpdateWindow(Proc, string.Format("ARK: {0} (Managed by ARKUpdater)", ServerData.GameServerName));

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

		public override bool StopServer(SettingsLoader.ServerChild ServerData, AutoResetEvent ResetEvent)
		{
			throw new NotImplementedException();
		}

		public override bool ServerRunning(SettingsLoader.ServerChild ServerData)
		{
			throw new NotImplementedException();
		}
	}
}
