using System;
using SevenZip;
using System.IO;
using System.Text;
using System.Linq;
using System.Reflection;
using ARKUpdater.Classes;
using System.Diagnostics;
using System.Collections.Generic;

namespace ARKUpdater.Interfaces
{
	abstract class BackupInterface
	{
		protected ARKUpdater _Parent;
		public BackupInterface(ARKUpdater Parent)
		{
			this._Parent = Parent;
		}

		public abstract bool BackupServer(SettingsLoader.ServerChild ServerData);
		public abstract bool CleanBackups(SettingsLoader.ServerChild ServerData);
	}

	class BackupInterfaceWindows : BackupInterface
	{
		public BackupInterfaceWindows(ARKUpdater parent) : base(parent) {}
		public override bool BackupServer(SettingsLoader.ServerChild ServerData)
		{
			string BackupFrom = string.Format("{0}\\ShooterGame\\Saved\\", ServerData.GameServerPath);
			string BackupTo = ServerData.BackupDirectory;
			if( !Directory.Exists(BackupFrom) )
			{
				_Parent.Log.ConsolePrint(LogLevel.Warning, @"Unable to Backup server, directory ShooterGame\Saved is missing. If this is the first startup then ignore this message");
				return false;
			}
			else if( (BackupTo.Length <= 0) || !Directory.Exists(BackupTo) )
			{
				_Parent.Log.ConsolePrint(LogLevel.Warning, @"Unable to Backup server, Specified backup directory does not exist or is not set.");
				return false;
			}

			_Parent.Log.ConsolePrint(LogLevel.Info, "Starting backup of server {0}", ServerData.GameServerName);
			if( _Parent.ARKConfiguration.Messages.ServerBackupBroadcast.Length >= 1 )
			{
				using( var RCONClient = new ArkRCON("127.0.0.1", ServerData.RCONPort) )
				{
					try
					{
						RCONClient.Authenticate(ServerData.ServerAdminPassword);
						RCONClient.ExecuteCommand(string.Format("serverchat {0}", _Parent.ARKConfiguration.Messages.ServerBackupBroadcast));
					} catch( QueryException ) {}
				}
			}

			// Use 7zipSharp to compress the backup directory
			SevenZipCompressor.SetLibraryPath("7za.dll");
			var Compressor = new SevenZipCompressor()
			{
				CompressionLevel = CompressionLevel.Normal,
				ArchiveFormat = OutArchiveFormat.SevenZip,
				IncludeEmptyDirectories = true,
			};

			string BackupPath = string.Format("{0}\\Backup-{1}.7z", BackupTo, Helpers.CurrentUnixStamp);
			Compressor.CompressDirectory(BackupFrom, BackupPath);

			_Parent.Log.ConsolePrint(LogLevel.Success, "Backup of server {0} complete", ServerData.GameServerName);
			return true;
		}

		public override bool CleanBackups(SettingsLoader.ServerChild ServerData)
		{
			try
			{
				var BackupFiles = Directory.GetFiles(ServerData.BackupDirectory, "*.7z", SearchOption.TopDirectoryOnly);
				if( BackupFiles.Length > _Parent.ARKConfiguration.Backup.NumberOfBackupsToKeepPerServer )
				{
					var Dict = new Dictionary<string, long>();
					foreach( var tFile in BackupFiles )
					{
						Dict.Add(tFile, File.GetCreationTime(tFile).Ticks);
					}

					var KeepItems = Dict.OrderByDescending( x => x.Value ).Take( _Parent.ARKConfiguration.Backup.NumberOfBackupsToKeepPerServer ).ToDictionary( x => x.Key );
					foreach( var tFile in BackupFiles )
					{
						if( KeepItems.ContainsKey(tFile) ) continue; // Keep this file

						_Parent.Log.ConsolePrint(LogLevel.Debug, "Deleting backup file '{0}' from server '{1}'", tFile, ServerData.GameServerName);
						File.Delete(tFile);
					}
				}
			}
			catch( Exception )
			{
				return false;
			}
			
			return true;
		}
	}

	class BackupInterfaceUnix : BackupInterface
	{
		public BackupInterfaceUnix(ARKUpdater parent) : base(parent) {}
		public override bool BackupServer(SettingsLoader.ServerChild ServerData)
		{
			throw new NotImplementedException();
		}

		public override bool CleanBackups(SettingsLoader.ServerChild ServerData)
		{
			throw new NotImplementedException();
		}
	}
}
