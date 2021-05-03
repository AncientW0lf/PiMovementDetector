using System.Timers;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace PiMovementDetector
{
    public class TcpConnector : IDisposable
    {
        private readonly TcpListener _listener;

        private readonly List<TcpClient> _connectedClients = new List<TcpClient>();

        private readonly Timer _connector = new Timer
        {
            AutoReset = false,
            Interval = 1000
        };

        public TcpConnector(out ushort port)
        {
            _listener = TcpListener.Create(0);
            _listener.Start();

            _connector.Elapsed += ConnectNewClient;
            _connector.Start();

            port = (ushort)((IPEndPoint)_listener.LocalEndpoint).Port;
        }

        public void Write(params byte[] buffer)
        {
            CheckConnections();

            for (int i = 0; i < _connectedClients.Count; i++)
                _connectedClients[i].GetStream().Write(buffer);
        }

        public void Write<T>(T data)
        {
            if (typeof(T)
                .GetCustomAttributes(true)
                .FirstOrDefault(a => a.GetType()
                .Equals(typeof(SerializableAttribute))) == null)
                throw new ArgumentException($"Type {nameof(T)} must have attribute {nameof(SerializableAttribute)}.");

            byte[] serialized = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<T>(data);

            Write(serialized);
        }

        public async Task WriteAsync(params byte[] buffer)
        {
            CheckConnections();

            for (int i = 0; i < _connectedClients.Count; i++)
                await _connectedClients[i].GetStream().WriteAsync(buffer);
        }

        public async Task WriteAsync<T>(T data)
        {
            if (typeof(T)
                .GetCustomAttributes(true)
                .FirstOrDefault(a => a.GetType()
                .Equals(typeof(SerializableAttribute))) == null)
                throw new ArgumentException($"Type {nameof(T)} must have attribute {nameof(SerializableAttribute)}.");

            byte[] serialized = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes<T>(data);

            await WriteAsync(serialized);
        }

        public event EventHandler<NetworkStream> ClientConnected;

        private void ConnectNewClient(object sender, ElapsedEventArgs e)
        {
            _listener.BeginAcceptTcpClient(ClientAccepted, null);
        }

        private void ClientAccepted(IAsyncResult ar)
        {
            _connectedClients.Add(_listener.EndAcceptTcpClient(ar));
            ClientConnected.Invoke(this, _connectedClients[^1].GetStream());

            _connector.Start();
        }

        private void CheckConnections()
        {
            for (int i = 0; i < _connectedClients.Count; i++)
            {
                var currClient = _connectedClients[i];
                if (!currClient.Connected)
                {
                    currClient.Dispose();
                    _connectedClients.Remove(currClient);
                    i--;
                    continue;
                }
            }
        }

        public void Dispose()
        {
            _connector.Dispose();
            _listener.Stop();

            foreach (TcpClient client in _connectedClients)
                client.Dispose();
        }
    }
}