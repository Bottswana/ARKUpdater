using System;
using System.IO;
using System.Text;
using Components.ConsolePlus;
using System.Collections.Generic;

namespace ARKUpdater.Classes
{
	enum LogLevel
	{
		Debug		= 0,
		Info		= 1,
		Success		= 2,
		Warning		= 3,
		Error		= 4
	};

	class ConsoleLogger
	{
		private StreamWriter _fileHandle;
		private LogLevel _Log;

		public ConsoleLogger(LogLevel Log)
		{
			// Open Log File
			this._fileHandle = new StreamWriter("console.log", true);
			this._fileHandle.AutoFlush = true;
			this._fileHandle.WriteLine("");
			this._Log = Log;
		}

		public void ConsolePrint(LogLevel Log, string Message, params object[] list)
		{
			// Check we are displaying this level of message
			if( (int)Log < (int)_Log ) return;

			// Format Incoming String
			var ReturnString = string.Format(Message, list);
			int TabCount = 1;
			var Prefix = "";

			// Console Colours
			switch( Log )
			{
				case LogLevel.Debug: { Prefix = "~Blue~"; break; }
				case LogLevel.Error: { Prefix = "~Red~"; break; }
				case LogLevel.Success: { Prefix = "~Green~"; TabCount = 2; break; }
				case LogLevel.Warning: { Prefix = "~Yellow~"; TabCount = 2; break; }
			}

			string DateString = DateTime.Now.ToString();
			string ConsoleOutput = ( TabCount == 1 ) ? string.Format("~Gray~[{0}] {3}[{1}]~Gray~:		{2}", DateString, Log, ReturnString, Prefix) : string.Format("~Gray~[{0}] {3}[{1}]~Gray~:	{2}", DateString, Log, ReturnString, Prefix);
			string FileOutput = ( TabCount == 1 ) ? string.Format("[{0}] [{1}]:		{2}", DateString, Log, ReturnString) : string.Format("[{0}] [{1}]:	{2}", DateString, Log, ReturnString);

			// Write to Console
			Cli.WriteLine(ConsoleOutput);
			this.LogToFile(FileOutput);
		}

		public void LogToFile(string Message)
		{
			// Write to Log File
			this._fileHandle.WriteLine(Message);
		}

		public void SetLogLevel(LogLevel Level)
		{
			this._Log = Level;
		}
	}
}
