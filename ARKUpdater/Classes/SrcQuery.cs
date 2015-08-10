using System;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ARKUpdater.Classes
{
	#region Source Server Query
	class SrcQuery : IDisposable
	{
		IPAddress IPAddr;
		int Port, Timeout;

		Socket ServerSocket;
		IPEndPoint ServerEndpoint;

		public SrcQuery(string ip, int port, int timeout = 2000)
		{
			this.Port = port;
			this.Timeout = timeout;
			this.IPAddr = IPAddress.Parse(ip);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if( disposing )
			{
				this.ServerSocket.Dispose();
			}
		}

		public Dictionary<string, object> QueryServer()
		{
			this._ConnectSocket();

			byte[] Request = Encoding.ASCII.GetBytes("Source Engine Query\0");
			byte[] Command = new byte[ Request.Length + 5 ];

			string[] PackString = {"FF","FF","FF","FF","54"};
			for( int i=0; i<PackString.Length; i++ )
			{
				Command[i] = byte.Parse(PackString[i], System.Globalization.NumberStyles.AllowHexSpecifier);
			}

			int ii = 5;
			for( int i=0; i<Request.Length; i++ )
			{
				Command[ii] = Request[i];
				ii++;
			}

			int PacketLength = Command.Length;
			if( PacketLength == ServerSocket.SendTo(Command, ServerEndpoint) )
			{
				byte[] buffer = new byte[1400];

				try
				{
					// Wait for data from the server
					ServerSocket.Receive(buffer);
				}
				catch( SocketException )
				{
					throw new QueryException("Timeout occoured waiting for a response from the server");
				}

				int Position = 3;
				if( this._GetByte(buffer, ref Position) == 73 )
				{
					// Validated A2S_Info Reply from server
					var ListData = new Dictionary<string, object>();
					Position = 5;

					ListData["ServerName"]		= this._GetString(buffer, ref Position);
					ListData["MapName"]			= this._GetString(buffer, ref Position);
					ListData["GameDir"]			= this._GetString(buffer, ref Position);
					ListData["GameDesc"]		= this._GetString(buffer, ref Position);
					ListData["AppID"]			= this._GetShort(buffer, ref Position);
					ListData["CurrentPlayers"]	= this._GetByte(buffer, ref Position);
					ListData["MaxPlayers"]		= this._GetByte(buffer, ref Position);
					ListData["CurrentBots"]		= this._GetByte(buffer, ref Position);
					ListData["ServerType"]		= this._GetChar(buffer, ref Position);
					ListData["ServerOS"]		= this._GetChar(buffer, ref Position);
					ListData["PasswordNeeded"]	= this._GetByte(buffer, ref Position);
					ListData["VACSecure"]		= this._GetByte(buffer, ref Position);
					ListData["GameVersion"]		= this._GetString(buffer, ref Position);

					return ListData;
				}
				else
				{
					throw new QueryException("Invalid A2S_Info response from server");
				}
			}

			// Sent packet length less than message
			throw new QueryException("Unable to send request to server");
		}
		
		public List<Dictionary<string, object>> QueryPlayer()
		{
			this._ConnectSocket();

			byte[] Command = new byte[9];
			string[] PackString = {"FF","FF","FF","FF","55","FF","FF","FF","FF"};

			for( int i=0; i<PackString.Length; i++ )
			{
				Command[i] = byte.Parse(PackString[i], System.Globalization.NumberStyles.AllowHexSpecifier);
			}

			if( Command.Length == ServerSocket.SendTo(Command, ServerEndpoint) )
			{
				byte[] buffer = new byte[1400];

				try
				{
					// Wait for data from the server
					ServerSocket.Receive(buffer);
				}
				catch( SocketException )
				{
					throw new QueryException("Timeout occoured waiting for a response from the server");
				}

				int Position = 3;
				if( this._GetByte(buffer, ref Position) == 65 )
				{
					// Received valid challenge from server
					string[] ChallengePackString = {"FF","FF","FF","FF","55"};
					for( int i=0; i<ChallengePackString.Length; i++ )
					{
						Command[i] = byte.Parse(ChallengePackString[i], System.Globalization.NumberStyles.AllowHexSpecifier);
					}

					for( int i=5; i<9; i++ )
					{
						Command[i] = this._GetByte(buffer, ref Position);
					}

					// Send Challenge Response
					if( Command.Length == ServerSocket.SendTo(Command, ServerEndpoint) )
					{
						byte[] buffer2 = new byte[1400];
						try
						{
							// Wait for data from the server
							ServerSocket.Receive(buffer2);
						}
						catch( SocketException )
						{
							throw new QueryException("Timeout occoured waiting for a response from the server");
						}

						Position = 3;
						if( this._GetByte(buffer2, ref Position) == 68 )
						{
							// Valid A2S_Player Response
							int PlayerCount = this._GetShort(buffer2, ref Position);
							var PlayerList = new List<Dictionary<string, object>>();

							Position++;
							for( int i=0; i<PlayerCount; i++)
							{
								var PlayerData = new Dictionary<string, object>();

								PlayerData["Index"]		= this._GetByte(buffer2, ref Position);
								PlayerData["Name"]		= this._GetString(buffer2, ref Position);
								PlayerData["Score"]		= this._GetLong(buffer2, ref Position);
								PlayerData["Duration"]	= this._GetFloat(buffer2, ref Position);

								PlayerList.Add(PlayerData);
							}

							return PlayerList;
						}
					}
				}
				else
				{
					throw new QueryException("Invalid A2S_Player Challenge from server");
				}
			}

			// Sent packet length less than message
			throw new QueryException("Unable to send request to server");
		}

		#region Connection Methods
		private void _ConnectSocket()
		{
			if( this.ServerSocket != null && this.ServerSocket.Connected ) return;

			this.ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			this.ServerEndpoint = new IPEndPoint(IPAddr, Port);
			this.ServerSocket.ReceiveTimeout = this.Timeout;
		}
		#endregion Connection Methods

		#region Read Methods
		private byte _GetByte(byte[] data, ref int pos)
		{
			pos++;
			return data[pos];
		}

		private char _GetChar(byte[] data, ref int pos)
		{
			pos++;
			return Convert.ToChar(data[pos]);
		}

		private string _GetString(byte[] data, ref int pos)
		{
			int currLen = 0;
			byte[] byteString = new byte[1400];

			for( int i=pos+1; i<=data.Length; i++ )
			{
				if( data[i] == 0 )
				{
					pos = i;
					break;
				}

				byteString[currLen] = data[i];
				currLen++;
			}

			var UTF8String = Encoding.UTF8.GetString(byteString);
			return Regex.Replace(UTF8String.Trim(), @"\0", "");
		}

		private short _GetShort(byte[] data, ref int pos)
		{
			pos = pos+2;
			return BitConverter.ToInt16(data, pos-1);
		}

		private long _GetLong(byte[] data, ref int pos)
		{
			pos = pos+4;
			return BitConverter.ToInt32(data, pos-3);
		}

		private Single _GetFloat(byte[] data, ref int pos)
		{
			pos = pos+4;
			return BitConverter.ToSingle(data, pos-3);
		}
		#endregion Read Methods
	}
	#endregion Source Server Query

	#region ARK Customized RCON
	class ArkRCON : IDisposable
	{
		string IPAddr;
		int Port, Timeout;
		bool Authenticated;

		TcpClient ServerClient;
		NetworkStream ServerStream;

		public ArkRCON(string ip, int port, int timeout = 2000)
		{
			this.IPAddr = ip;
			this.Port = port;
			
			this.Timeout = timeout;
			this.Authenticated = false;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if( disposing )
			{
				if( this.ServerClient != null && this.ServerClient.Connected ) this._DisconnectSocket();
				if( this.ServerStream != null ) this.ServerStream.Dispose();
			}
		}

		public void Authenticate(string Password)
		{
			this._ConnectSocket();

			// Copy Request ID into Byte array
			byte[] Request = Encoding.ASCII.GetBytes(Password);
			byte[] Transmit = new byte[ 14 + Request.Length ];

			uint length = 10 + (uint)Request.Length;
			int pos = 0;

			UInt32[] PackValues = {length, 1, 3};
			foreach( var Value in PackValues )
			{
				byte[] output = BitConverter.GetBytes(Value);
				Buffer.BlockCopy(output, 0, Transmit, pos, output.Length);
				pos = pos + output.Length;
			}

			byte[] NullBytes = {00, 00};

			// Copy password into Byte array
			Buffer.BlockCopy(Request, 0, Transmit, pos, Request.Length);
			Buffer.BlockCopy(NullBytes, 0, Transmit, pos+Request.Length, 2);

			this._WriteSocket(Transmit);

			// Await Response
			byte[] ResponseData = new byte[4096];
			int Position = 3;

			try
			{
				this.ServerStream.Read(ResponseData, 0, 4096);
				var PasswordResponse = this._GetLong(ResponseData, ref Position);
				if( PasswordResponse == -1 )
				{
					this._DisconnectSocket();
					throw new QueryException("Incorrect RCON Password");
				}
			}
			catch( System.IO.IOException )
			{
				throw new QueryException("RCON Failed: Socket Read Error");
			}

			this.Authenticated = true;
		}

		public string ExecuteCommand(string Command)
		{
			string ReturnData = null;
			if( !Authenticated )
			{
				throw new QueryException("Not authenticated with Server");
			}

			try
			{
				// Copy Request ID into Byte array
				byte[] Request = Encoding.ASCII.GetBytes(Command);
				byte[] Transmit = new byte[ 14 + Request.Length ];

				uint length = 10 + (uint)Request.Length;
				int pos = 0;

				UInt32[] PackValues = {length, 1, 2};
				foreach( var Value in PackValues )
				{
					byte[] output = BitConverter.GetBytes(Value);
					Buffer.BlockCopy(output, 0, Transmit, pos, output.Length);
					pos = pos + output.Length;
				}

				byte[] NullBytes = {00, 00};

				// Copy request into Byte array
				Buffer.BlockCopy(Request, 0, Transmit, pos, Request.Length);
				Buffer.BlockCopy(NullBytes, 0, Transmit, pos+Request.Length, 2);

				this._WriteSocket(Transmit);

				// Await Response
				byte[] ResponseData = new byte[4096];
				this.ServerStream.Read(ResponseData, 0, 4096);

				// Read Response
				int Position = 11;
				ReturnData = this._GetString(ResponseData, ref Position);
			}
			catch( Exception )
			{
				return null;
			}

			return ReturnData;
		}

		#region Connection Methods
		private void _ConnectSocket()
		{
			if( this.ServerClient != null && this.ServerClient.Connected ) return;

			try
			{
				this.ServerClient = new TcpClient(IPAddr, Port);
				this.ServerStream = ServerClient.GetStream();

				this.ServerClient.ReceiveTimeout = this.Timeout;
				this.ServerClient.SendTimeout = this.Timeout;
			}
			catch( SocketException E )
			{
				throw new QueryException(string.Format("Unable to open RCON: {0}", E.Message));
			}
		}

		private void _DisconnectSocket()
		{
			this.ServerClient.Close();
		}
		#endregion Connection Methods

		#region Write Methods
		private void _WriteSocket(byte[] data)
		{
			try
			{
				int DataLength = data.Length;
				ServerStream.Write(data, 0, DataLength);
			}
			catch( Exception ) {}
		}
		#endregion Write Methods

		#region Read Methods
		private string _GetString(byte[] data, ref int pos)
		{
			int currLen = 0;
			byte[] byteString = new byte[4096];

			for( int i=pos+1; i<=data.Length; i++ )
			{
				if( data[i] == 0 )
				{
					pos = i;
					break;
				}

				byteString[currLen] = data[i];
				currLen++;
			}

			var UTF8String = Encoding.UTF8.GetString(byteString);
			return Regex.Replace(UTF8String.Trim(), @"\0|\n ", "").Trim();
		}

		private long _GetLong(byte[] data, ref int pos)
		{
			pos = pos+4;
			return BitConverter.ToInt32(data, pos-3);
		}
		#endregion Read Methods
	}
	#endregion ARK Customized RCON

	class QueryException : Exception
	{
		public QueryException() {}
		public QueryException(string message) : base(message) {}
		public QueryException(string message, Exception inner) : base(message, inner) {}
	}
}
