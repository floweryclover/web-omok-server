using System.Collections;
using System.Net.WebSockets;

namespace WebOmokServer
{
    public class OmokServer
    {
        private int clientIndex = 0;
        private Dictionary<int, WebSocket> webSockets = new Dictionary<int, WebSocket>();

        public void HandleClient(WebSocket webSocket)
        {

        }
    }
}
