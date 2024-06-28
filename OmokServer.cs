using Microsoft.AspNetCore.Http;
using System.Drawing;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace WebOmokServer
{
    public partial class OmokServer
    {
        private class MessageSizeOverFlowException : Exception
        {
            public MessageSizeOverFlowException()
            {

            }
            public MessageSizeOverFlowException(int size) : base($"메시지 크기가 버퍼 크기보다 큽니다: {size}/{BUFFER_SIZE}byte")
            {
            }
        }

        enum FlashMessageType { Info, Warning, Error }

        private const int MAX_ROOM_COUNT = 16;
		private const int BUFFER_SIZE = 256;
        private const string DEFAULT_USER_NICKNAME = "플레이어";

        private GameRoom[] _gameRooms = new GameRoom[MAX_ROOM_COUNT];
        private Dictionary<string, string> _nicknames = new Dictionary<string, string>();
		private Dictionary<string, Client> _clients = new Dictionary<string, Client>();

        public OmokServer()
        {
            for (int roomId = 0; roomId<MAX_ROOM_COUNT; roomId++)
            {
                var newRoom = new GameRoom(roomId);
                newRoom.PlayerKicked += OnPlayerKicked;
                newRoom.GameRoomInactivated += OnGameRoomInactivated;
                _gameRooms[roomId] = newRoom;
            }
        }

        public async Task HandleClientLoopAsync(Client client)
        {
            var closeWebSocket = (string message) => client.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, message, CancellationToken.None);
            OnClientConnected(client);
            var buffer = new byte[BUFFER_SIZE];
            
            try
            {
				while (client.WebSocket.State == WebSocketState.Open)
				{
					var result = await client.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
					if (result.MessageType == WebSocketMessageType.Close)
					{
                        await closeWebSocket("클라이언트가 연결을 종료하였습니다.");
                        break;
					}
                    
                    if (result.Count > BUFFER_SIZE)
                    {
                        Console.Write("[수신 버퍼 에러]");
                        throw new MessageSizeOverFlowException(result.Count);
                    }

					string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (!await HandleMessageAsync(client, message))
                    {
                        await closeWebSocket("오류가 발생하였습니다.");
                        break;
                    }
				}
			}
            catch (WebSocketException webSocketException)
            {
                if (webSocketException.ErrorCode != 0)
                    Console.WriteLine($"[웹소켓 에러] {client}: {webSocketException.Message}");
            }
            catch (MessageSizeOverFlowException messageSizeOverFlowException)
            {
                Console.WriteLine($"클라이언트 {client}: {messageSizeOverFlowException.Message}");
            }
            finally
            {
                if (client.WebSocket.State == WebSocketState.Open)
                {
                    await client.WebSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "서버 로직 에러로 연결을 종료합니다.", CancellationToken.None);
                }
            }
            OnClientDisconnected(client);
		}

        private void OnGameRoomInactivated(object? sender, EventArgs args)
        {
            if (sender == null)
            {
                return;
            }

            foreach (var client in _clients.Values)
            {
                ClientRemoveRoomItemAsync(client, ((GameRoom)sender).RoomId).Wait();
            }
        }

        private void OnPlayerKicked(object? sender, GameRoom.ClientIdArgs args)
        {
            if (sender == null)
            {
                return;
            }

            if (!_clients.ContainsKey(args.ClientId))
                return;
            ClientNotifyKickedFromGameRoomAsync(_clients[args.ClientId]).Wait();
        }

        private void OnRoomNameChanged(object? sender, EventArgs args)
        {
            if (sender == null)
            {
                return;
            }

            foreach (var client in _clients.Values)
            {
                ClientSendRoomItemAsync(client, ((GameRoom)sender).RoomId).Wait();
            }
        }

        private void OnClientConnected(Client client)
        {
            Console.WriteLine($"[접속] {client}");
            _clients.Add(client.Id, client);
            _nicknames.Add(client.Id, "플레이어");
        }

        private void OnClientDisconnected(Client client)
        {
            _clients.Remove(client.Id);
            _nicknames.Remove(client.Id);
            foreach (var gameRoom in _gameRooms)
            {
                if (gameRoom.IsPlayerJoined(client.Id))
                {
                    gameRoom.RemovePlayer(client.Id);
                }
            }
            client.Dispose();
        }
    }
}
