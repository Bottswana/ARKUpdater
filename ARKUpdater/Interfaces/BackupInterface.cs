using System;
using SevenZip;
using System.IO;
using System.Text;
using ARKUpdater.Classes;
using System.Diagnostics;
using System.Collections.Generic;

namespace ARKUpdater.Interfaces
{
	abstract class BackupInterface
	{
		public abstract bool BackupServer(SettingsLoader.ServerChild ServerData);
		public abstract bool CleanBackups(SettingsLoader.ServerChild ServerData);
	}

	class BackupInterfaceWindows : BackupInterface
	{
		#region Constructor
		private ARKUpdater _Parent;
		public BackupInterfaceWindows(ARKUpdater Parent)
		{
			this._Parent = Parent;
		}
		#endregion Constructor

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

			if( _Parent.ARKConfiguration.Backup.UseCompression )
			{
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
			}
			else
			{
				// Copy folder to backup directory
			}

			return true;
		}

		public override bool CleanBackups(SettingsLoader.ServerChild ServerData)
		{
			return true;
		}
	}

	class BackupInterfaceUnix : BackupInterface
	{
		#region Constructor
		private ARKUpdater _Parent;
		public BackupInterfaceUnix(ARKUpdater Parent)
		{
			this._Parent = Parent;
		}
		#endregion Constructor

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
