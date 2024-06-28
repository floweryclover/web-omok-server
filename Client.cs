using System.Net.WebSockets;

namespace WebOmokServer
{
    public class Client : IDisposable
    {
        public WebSocket WebSocket { get; }
        public string Id { get; }
        public string Address { get; }
        public Client(WebSocket webSocket, ConnectionInfo connectionInfo)
        {
            WebSocket = webSocket;
            Id = connectionInfo.Id;
            Address = string.Format($"{connectionInfo.RemoteIpAddress}:{connectionInfo.RemotePort}");
        }

        public void Dispose()
        {
            WebSocket.Dispose();
        }
        public override string ToString()
        {
            return string.Format($"{Id}({Address})");
        }
    }
}
