using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Wombat.Network.Sockets
{
    public class TcpSocketServer
    {
        #region Fields

        private ILogger _logger;
        private TcpListener _listener;
        private readonly ConcurrentDictionary<string, TcpSocketSession> _sessions = new ConcurrentDictionary<string, TcpSocketSession>();
        private readonly ITcpSocketServerEventDispatcher _dispatcher;
        private readonly TcpSocketServerConfiguration _configuration;

        private int _state;
        private const int _none = 0;
        private const int _listening = 1;
        private const int _disposed = 5;

        #endregion

        #region Constructors

        public TcpSocketServer(int listenedPort, ITcpSocketServerEventDispatcher dispatcher, TcpSocketServerConfiguration configuration = null)
            : this(IPAddress.Any, listenedPort, dispatcher, configuration)
        {
        }



        public TcpSocketServer(IPAddress listenedAddress, int listenedPort, ITcpSocketServerEventDispatcher dispatcher, TcpSocketServerConfiguration configuration = null)
            : this(new IPEndPoint(listenedAddress, listenedPort), dispatcher, configuration)
        {
        }

        public TcpSocketServer(IPEndPoint listenedEndPoint, ITcpSocketServerEventDispatcher dispatcher, TcpSocketServerConfiguration configuration = null)
        {
            if (listenedEndPoint == null)
                throw new ArgumentNullException("listenedEndPoint");
            if (dispatcher == null)
                throw new ArgumentNullException("dispatcher");

            this.ListenedEndPoint = listenedEndPoint;
            _dispatcher = dispatcher;
            _configuration = configuration ?? new TcpSocketServerConfiguration();

            if (_configuration.BufferManager == null)
                throw new InvalidProgramException("The buffer manager in configuration cannot be null.");
            if (_configuration.FrameBuilder == null)
                throw new InvalidProgramException("The frame handler in configuration cannot be null.");
        }

        public TcpSocketServer(
            int listenedPort,
            Func<TcpSocketSession, byte[], int, int, Task> onSessionDataReceived = null,
            Func<TcpSocketSession, Task> onSessionStarted = null,
            Func<TcpSocketSession, Task> onSessionClosed = null,
            TcpSocketServerConfiguration configuration = null)
            : this(IPAddress.Any, listenedPort, onSessionDataReceived, onSessionStarted, onSessionClosed, configuration)
        {
        }

        public TcpSocketServer(
            IPAddress listenedAddress, int listenedPort,
            Func<TcpSocketSession, byte[], int, int, Task> onSessionDataReceived = null,
            Func<TcpSocketSession, Task> onSessionStarted = null,
            Func<TcpSocketSession, Task> onSessionClosed = null,
            TcpSocketServerConfiguration configuration = null)
            : this(new IPEndPoint(listenedAddress, listenedPort), onSessionDataReceived, onSessionStarted, onSessionClosed, configuration)
        {
        }

        public TcpSocketServer(
            IPEndPoint listenedEndPoint,
            Func<TcpSocketSession, byte[], int, int, Task> onSessionDataReceived = null,
            Func<TcpSocketSession, Task> onSessionStarted = null,
            Func<TcpSocketSession, Task> onSessionClosed = null,
            TcpSocketServerConfiguration configuration = null)
            : this(listenedEndPoint,
                  new DefaultTcpSocketServerEventDispatcher(onSessionDataReceived, onSessionStarted, onSessionClosed),
                  configuration)
        {
        }

        #endregion

        #region Properties

        public IPEndPoint ListenedEndPoint { get; private set; }
        public bool IsListening { get { return _state == _listening; } }
        public int SessionCount { get { return _sessions.Count; } }

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
                throw new InvalidOperationException("This tcp server has already started.");
            }

            try
            {
                _listener = new TcpListener(this.ListenedEndPoint);
                SetSocketOptions();

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
                            await session.Close(); // parent server close session when shutdown
                        }
                    }
                    catch (Exception ex) when (!ShouldThrow(ex)) { }
                },
                TaskCreationOptions.PreferFairness)
                .Wait();
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
        }

        private void SetSocketOptions()
        {
            _listener.AllowNatTraversal(_configuration.AllowNatTraversal);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, _configuration.ReuseAddress);
        }

        public bool Pending()
        {
            if (!IsListening)
                throw new InvalidOperationException("The tcp server is not active.");

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
                    TaskCreationOptions.None)
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
            var session = new TcpSocketSession(acceptedTcpClient, _configuration, _configuration.BufferManager, _dispatcher, this);

            if (_sessions.TryAdd(session.SessionKey, session))
            {
                _logger?.LogDebug("New session [{0}].", session);
                try
                {
                    await session.Start();
                }
                catch (TimeoutException ex)
                {
                    _logger?.LogError(ex.Message, ex);
                }
                finally
                {
                    TcpSocketSession throwAway;
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

        #region Logger
        public void UseLogger(ILogger logger)
        {
            _logger = logger;
        }

        #endregion

        #region Send

        public async Task SendToAsync(string sessionKey, byte[] data)
        {
            await SendToAsync(sessionKey, data, 0, data.Length);
        }

        public async Task SendToAsync(string sessionKey, byte[] data, int offset, int count)
        {
            TcpSocketSession sessionFound;
            if (_sessions.TryGetValue(sessionKey, out sessionFound))
            {
                await sessionFound.SendAsync(data, offset, count);
            }
            else
            {
                _logger?.LogWarning("Cannot find session [{0}].", sessionKey);
            }
        }

        public async Task SendToAsync(TcpSocketSession session, byte[] data)
        {
            await SendToAsync(session, data, 0, data.Length);
        }

        public async Task SendToAsync(TcpSocketSession session, byte[] data, int offset, int count)
        {
            TcpSocketSession sessionFound;
            if (_sessions.TryGetValue(session.SessionKey, out sessionFound))
            {
                await sessionFound.SendAsync(data, offset, count);
            }
            else
            {
                _logger?.LogWarning("Cannot find session [{0}].", session);
            }
        }

        public async Task BroadcastAsync(byte[] data)
        {
            await BroadcastAsync(data, 0, data.Length);
        }

        public async Task BroadcastAsync(byte[] data, int offset, int count)
        {
            foreach (var session in _sessions.Values)
            {
                await session.SendAsync(data, offset, count);
            }
        }

        #endregion

        #region Session

        public bool HasSession(string sessionKey)
        {
            return _sessions.ContainsKey(sessionKey);
        }

        public TcpSocketSession GetSession(string sessionKey)
        {
            TcpSocketSession session = null;
            _sessions.TryGetValue(sessionKey, out session);
            return session;
        }

        public async Task CloseSession(string sessionKey)
        {
            TcpSocketSession session = null;
            if (_sessions.TryGetValue(sessionKey, out session))
            {
                await session.Close(); // parent server close session by session-key
            }
        }

        #endregion
    }
}
