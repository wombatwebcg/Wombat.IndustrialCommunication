using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Wombat.Network.WebSockets
{
    public sealed class WebSocketServer
    {
        #region Fields

        private static  ILogger _logger;
        private TcpListener _listener;
        private readonly ConcurrentDictionary<string, WebSocketSession> _sessions = new ConcurrentDictionary<string, WebSocketSession>();
        private readonly AsyncWebSocketServerModuleCatalog _catalog;
        private readonly WebSocketServerConfiguration _configuration;
        private AsyncWebSocketRouteResolver _routeResolver;

        private int _state;
        private const int _none = 0;
        private const int _listening = 1;
        private const int _disposed = 5;

        #endregion

        #region Constructors

        public WebSocketServer(int listenedPort, AsyncWebSocketServerModuleCatalog catalog, WebSocketServerConfiguration configuration = null)
            : this(IPAddress.Any, listenedPort, catalog, configuration)
        {
        }

        public WebSocketServer(IPAddress listenedAddress, int listenedPort, AsyncWebSocketServerModuleCatalog catalog, WebSocketServerConfiguration configuration = null)
            : this(new IPEndPoint(listenedAddress, listenedPort), catalog, configuration)
        {
        }

        public WebSocketServer(IPEndPoint listenedEndPoint, AsyncWebSocketServerModuleCatalog catalog, WebSocketServerConfiguration configuration = null)
        {
            if (listenedEndPoint == null)
                throw new ArgumentNullException("listenedEndPoint");
            if (catalog == null)
                throw new ArgumentNullException("catalog");

            this.ListenedEndPoint = listenedEndPoint;
            _catalog = catalog;
            _configuration = configuration ?? new WebSocketServerConfiguration();

            if (_configuration.BufferManager == null)
                throw new InvalidProgramException("The buffer manager in configuration cannot be null.");

            Initialize();
        }

        private void Initialize()
        {
            _routeResolver = new AsyncWebSocketRouteResolver(_catalog);
        }

        #endregion

        public  void UsgLogger(ILogger log)
        {
            _logger = log;
        }


        #region Properties

        public IPEndPoint ListenedEndPoint { get; private set; }
        public bool IsListening { get { return _state == _listening; } }
        public int SessionCount { get { return _sessions.Count; } }

        public IEnumerable<string> EnabledExtensions
        {
            get { return _configuration.EnabledExtensions != null ? _configuration.EnabledExtensions.Keys : null; }
        }
        public IEnumerable<string> EnabledSubProtocols
        {
            get { return _configuration.EnabledSubProtocols != null ? _configuration.EnabledSubProtocols.Keys : null; }
        }

        #endregion

        #region Server

        public void Listen()
        {
            int origin = Interlocked.CompareExchange(ref _state, _listening, _none);
            if (origin == _disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
            else if (origin != _none)
            {
                throw new InvalidOperationException("This websocket server has already started.");
            }

            try
            {
                _listener = new TcpListener(this.ListenedEndPoint);
                ConfigureListener();

                _listener.Start(_configuration.PendingConnectionBacklog);

                Task.Factory.StartNew(async () =>
                {
                    await Accept();
                },
                TaskCreationOptions.LongRunning)
                .Forget();
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
        }

        public void Shutdown()
        {
            if (Interlocked.Exchange(ref _state, _disposed) == _disposed)
            {
                return;
            }

            try
            {
                _listener.Stop();
                _listener = null;

                Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        foreach (var session in _sessions.Values)
                        {
                            await session.Close(WebSocketCloseCode.NormalClosure);
                        }
                    }
                    catch (Exception ex) when (!ShouldThrow(ex)) { }
                },
                TaskCreationOptions.PreferFairness)
                .Wait();
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
        }

        private void ConfigureListener()
        {
            _listener.AllowNatTraversal(_configuration.AllowNatTraversal);
        }

        public bool Pending()
        {
            if (!IsListening)
                throw new InvalidOperationException("The websocket server is not active.");

            // determine if there are pending connection requests.
            return _listener.Pending();
        }

        private async Task Accept()
        {
            try
            {
                while (IsListening)
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync();
                    Task.Factory.StartNew(async () =>
                    {
                        await Process(tcpClient);
                    },
                    TaskCreationOptions.PreferFairness)
                    .Forget();
                }
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
            catch (Exception ex)
            {
               _logger?.LogError(ex.Message, ex);
            }
        }

        private async Task Process(TcpClient acceptedTcpClient)
        {
            var session = new WebSocketSession(acceptedTcpClient, _configuration, _configuration.BufferManager, _routeResolver, this);
            if (_logger != null) session.UsgLogger(_logger);
            if (_sessions.TryAdd(session.SessionKey, session))
            {
                _logger?.LogDebug("New session [{0}].", session);
                try
                {
                    await session.Start();
                }
                catch (Exception ex)
                when (ex is TimeoutException || ex is WebSocketException)
                {
                   _logger?.LogError(ex.Message, ex);
                }
                finally
                {
                    WebSocketSession throwAway;
                    if (_sessions.TryRemove(session.SessionKey, out throwAway))
                    {
                        _logger?.LogDebug("Close session [{0}].", throwAway);
                    }
                }
            }
        }

        private bool ShouldThrow(Exception ex)
        {
            if (ex is ObjectDisposedException
                || ex is InvalidOperationException
                || ex is SocketException
                || ex is IOException)
            {
                return false;
            }
            return true;
        }

        #endregion

        #region Send

        public async Task SendTextToAsync(string sessionKey, string text)
        {
            WebSocketSession sessionFound;
            if (_sessions.TryGetValue(sessionKey, out sessionFound))
            {
                await sessionFound.SendTextAsync(text);
            }
            else
            {
                _logger?.LogWarning("Cannot find session [{0}].", sessionKey);
            }
        }

        public async Task SendTextToAsync(WebSocketSession session, string text)
        {
            WebSocketSession sessionFound;
            if (_sessions.TryGetValue(session.SessionKey, out sessionFound))
            {
                await sessionFound.SendTextAsync(text);
            }
            else
            {
                _logger?.LogWarning("Send text data but cannot find session [{0}].", session);
            }
        }

        public async Task SendBinaryToAsync(string sessionKey, byte[] data)
        {
            await SendBinaryToAsync(sessionKey, data, 0, data.Length);
        }

        public async Task SendBinaryToAsync(string sessionKey, byte[] data, int offset, int count)
        {
            WebSocketSession sessionFound;
            if (_sessions.TryGetValue(sessionKey, out sessionFound))
            {
                await sessionFound.SendBinaryAsync(data, offset, count);
            }
            else
            {
                _logger?.LogWarning("Cannot find session [{0}].", sessionKey);
            }
        }

        public async Task SendBinaryToAsync(WebSocketSession session, byte[] data)
        {
            await SendBinaryToAsync(session, data, 0, data.Length);
        }

        public async Task SendBinaryToAsync(WebSocketSession session, byte[] data, int offset, int count)
        {
            WebSocketSession sessionFound;
            if (_sessions.TryGetValue(session.SessionKey, out sessionFound))
            {
                await sessionFound.SendBinaryAsync(data, offset, count);
            }
            else
            {
                _logger?.LogWarning("Send binary data but cannot find session [{0}].", session);
            }
        }

        public async Task BroadcastTextAsync(string text)
        {
            foreach (var session in _sessions.Values)
            {
                await session.SendTextAsync(text);
            }
        }

        public async Task BroadcastBinaryAsync(byte[] data)
        {
            await BroadcastBinaryAsync(data, 0, data.Length);
        }

        public async Task BroadcastBinaryAsync(byte[] data, int offset, int count)
        {
            foreach (var session in _sessions.Values)
            {
                await session.SendBinaryAsync(data, offset, count);
            }
        }

        #endregion

        #region Session

        public bool HasSession(string sessionKey)
        {
            return _sessions.ContainsKey(sessionKey);
        }

        public WebSocketSession GetSession(string sessionKey)
        {
            WebSocketSession session = null;
            _sessions.TryGetValue(sessionKey, out session);
            return session;
        }

        public async Task CloseSession(string sessionKey)
        {
            WebSocketSession session = null;
            if (_sessions.TryGetValue(sessionKey, out session))
            {
                await session.Close(WebSocketCloseCode.NormalClosure);
            }
        }

        #endregion
    }
}
