using System.ComponentModel;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using NetDaemon.Client;

public class SerialClient : IDisposable
{
    private readonly ILogger _logger;
    private Socket? _socket;
    private Timer? _checkTimer;
    private bool _isConnected;
    private bool _disposedValue;
    private readonly string _host;
    private readonly int _port;
    private byte[] _buffer;

    public class ReceiveEventArgs : EventArgs
    {
        public string Content;
        public ReceiveEventArgs(string content)
        {
            Content = content;
        }
    }
    public event EventHandler<ReceiveEventArgs>? ReceivedEvent;
    public event EventHandler<EventArgs>? DisconnectedEvent;

    private const int BufferSize = 1024;
    private const int ConnectionCheckInterval = 4096;
    
    public SerialClient(ILogger logger, string host, int port)
    {
        _logger = logger;
        _host = host;
        _port = port;

        _isConnected = false;
    }

    public void Connect()
    {
        if (_isConnected) throw new InvalidOperationException("Already connected.");
        _logger.LogInformation($"Connecting to {_host}:{_port} via TCP...");

        try
        {
            IPAddress ip = Dns.GetHostAddresses(_host)[0];
            IPEndPoint endpoint = new IPEndPoint(ip, _port);

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _buffer = new byte[BufferSize];
            _checkTimer = new Timer();
            _checkTimer.Interval = ConnectionCheckInterval;
            _checkTimer.Elapsed += ConnectionCheck;

            _socket.Connect(endpoint);
            _socket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, new AsyncCallback(ReceivePacket), _socket);
            _checkTimer.Start();
        }
        catch(Exception ex)
        {
            _logger.LogError("Exception occurred in open tcp connection: " + ex);
            Disconnect();
            return;
        }

        _logger.LogInformation("Connected.");
    }

    public void Disconnect()
    {
        if (_socket is not null)
        {
            _socket.Close();
            _socket = null;
        }

        if (_checkTimer is not null)
        {
            _checkTimer.Stop();
            _checkTimer.Elapsed -= ConnectionCheck;
            _checkTimer.Dispose();
            _checkTimer = null;
        }

        _isConnected = false;
        _logger.LogInformation($"Disconnected.");
        if(DisconnectedEvent is not null) DisconnectedEvent(this, EventArgs.Empty);
    }

    private void ReceivePacket(IAsyncResult asyncResult)
    {
        Socket asyncSocket = (Socket)asyncResult.AsyncState;
        try
        {
            int recv = asyncSocket.EndReceive(asyncResult);
            if (recv == 0) return;
            string receivedString = BitConverter.ToString(_buffer, 0, recv);
            _logger.LogDebug("Received: " + receivedString);

            if (ReceivedEvent is not null) ReceivedEvent(this, new ReceiveEventArgs(receivedString.Replace("-", String.Empty)));
            asyncSocket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None,
                new AsyncCallback(ReceivePacket), asyncSocket);
        }
        catch (Exception e)
        {
            _logger.LogError("Exception occurred in receive packet: " + e);
            Disconnect();
        }
    }

    private void ConnectionCheck(object? sender, ElapsedEventArgs e)
    {
        try
        {
            _isConnected = !(_socket.Poll(1, SelectMode.SelectRead) && _socket.Available == 0);
        }
        catch(Exception ex)
        {
            _logger.LogError("Exception occurred in connection keep-alive check: " + ex);
        }

        if (!_isConnected)
        {
            _logger.LogWarning("Connection keep-alive check failed. Disconnecting..");
            Disconnect();
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~SerialClient()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

