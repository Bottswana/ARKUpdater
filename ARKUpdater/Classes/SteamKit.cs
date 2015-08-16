using System;
using SteamKit2;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ARKUpdater.Classes
{
	class ThreadPair : IDisposable
	{
		public Thread tThread;
		public SteamKit tClass;

		public ThreadPair(Thread t, SteamKit c)
		{
			tThread = t;
			tClass = c;
		}

		#region Disposal
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if( disposing )
			{
				tClass.Dispose();
			}
		}
		#endregion Disposal
	}

	class SteamKit : IDisposable
	{
		private SteamUser _User;
		private SteamApps _Apps;
		private ARKUpdater _Parent;
		private SteamClient _Client;
		private CallbackManager _CManager;

		public bool Ready;
		public bool Failed;
		private bool _ThreadRunning;

		#region Disposal
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if( disposing )
			{
				// Stop thread if it is running
				if( _ThreadRunning ) _ThreadRunning = false;
				Ready = false;

				// Disconnect from Steam3
				if( _Client.IsConnected ) _Client.Disconnect();
			}
		}
		#endregion Disposal

		#region Create Thread
		public static ThreadPair SpawnThread(ARKUpdater p)
		{
			var thisThread = new SteamKit(p);
			Thread tThread = new Thread( () => thisThread.RunThread() )
			{
				IsBackground = true
			};

			tThread.Start();
			return new ThreadPair(tThread, thisThread);
		}
		#endregion Create Thread

		#region Thread Setup
		public SteamKit(ARKUpdater p)
		{
			this._Parent = p;
			this.Ready = false;
			this.Failed = false;
			this._ThreadRunning = true;

			this._Client = new SteamClient();
			this._CManager = new CallbackManager(this._Client);

			this._User = this._Client.GetHandler<SteamUser>();
			this._Apps = this._Client.GetHandler<SteamApps>();

			this.SubscribeCallbacks();
			this._Client.Connect();
		}

		public void RunThread()
		{
			while( this._ThreadRunning )
			{
				this._CManager.RunWaitCallbacks( TimeSpan.FromSeconds( 1 ) );
			}
		}

		public void StopThread()
		{
			this._ThreadRunning = false;
		}

		public void SubscribeCallbacks()
		{
			this._CManager.Subscribe<SteamUser.LoggedOnCallback>(LogOnCallback);
			this._CManager.Subscribe<SteamClient.ConnectedCallback>(ConnectedCallback);
			this._CManager.Subscribe<SteamClient.DisconnectedCallback>(DisconnectedCallback);
		}
		#endregion Thread Setup

		#region Steam3 Callbacks
		private void ConnectedCallback(SteamClient.ConnectedCallback connected)
		{
			_Parent.Log.ConsolePrint(LogLevel.Debug, "Connected to Steam3, Authenticating as anonymous user");
			_User.LogOnAnonymous();
		}

		private void DisconnectedCallback(SteamClient.DisconnectedCallback disconnected)
		{
			_Parent.Log.ConsolePrint(LogLevel.Debug, "Disconnected from Steam3");
			_ThreadRunning = false;
		}

		private void LogOnCallback(SteamUser.LoggedOnCallback loggedOn)
		{
			if( loggedOn.Result != EResult.OK )
			{
				_Parent.Log.ConsolePrint(LogLevel.Debug, "Unable to connect to Steam3. {0}", loggedOn.Result);
				_ThreadRunning = false;
				Failed = true;
				return;
			}

			_Parent.Log.ConsolePrint(LogLevel.Debug, "Logged in anonymously to Steam3");
			Ready = true;
		}
		#endregion Steam3 Callbacks

		#region Fetch App Information
		public delegate void AppCallback(SteamApps.PICSProductInfoCallback.PICSProductInfo returnData);
		public void RequestAppInfo(uint appid, AppCallback callback)
		{
			// Callback for Application Information
			Action<SteamApps.PICSProductInfoCallback> AppCallback = (appinfo) =>
			{
				var returnData = appinfo.Apps.Where( x => x.Key == appid );
				if( returnData.Count() == 1 )
				{
					// Return successful data
					callback( returnData.First().Value );
					return;
				}

				callback(null);
			};

			// Callback for Token
			Action<SteamApps.PICSTokensCallback> TokenCallback = (apptoken) =>
			{
				// Check if our token was returned
				var ourToken = apptoken.AppTokens.Where( x => x.Key == appid );
				if( ourToken.Count() != 1 ) return;

				// Use our token to request the app information
				var Request = new SteamApps.PICSRequest(appid)
				{
					AccessToken = ourToken.First().Value,
					Public = false
				};

				_CManager.Subscribe( _Apps.PICSGetProductInfo(new List<SteamApps.PICSRequest>() {Request}, new List<SteamApps.PICSRequest>()), AppCallback);
			};

			// Fire Token Callback
			_CManager.Subscribe(_Apps.PICSGetAccessTokens(new List<uint>() {appid}, new List<uint>()), TokenCallback);
		}
		#endregion Fetch App Information
	}
}
