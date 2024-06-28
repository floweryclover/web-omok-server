using System.Drawing;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace WebOmokServer
{
    public class OmokServer
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

        private const int MAX_ROOM_COUNT = 16;
		private const int BUFFER_SIZE = 256;

        private GameRoom[] _gameRooms = new GameRoom[MAX_ROOM_COUNT];
        private Dictionary<string, string> _nicknames = new Dictionary<string, string>();
        
		private Dictionary<string, WebSocket> _webSockets = new Dictionary<string, WebSocket>();

        enum FlashMessageType { Info, Warning, Error }

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

        private void OnGameRoomInactivated(object? sender, EventArgs args)
        {
            foreach (var client in _webSockets.Values)
            {
                ClientRemoveRoomItemAsync(client, ((GameRoom)sender).RoomId).Wait();
            }
        }

        private void OnPlayerKicked(object? sender, GameRoom.ClientIdArgs args)
        {
            if (!_webSockets.ContainsKey(args.ClientId))
                return;
            ClientNotifyKickedFromGameRoomAsync(_webSockets[args.ClientId]).Wait();
        }

        private void OnRoomNameChanged(object? sender, EventArgs args)
        {
			foreach (var client in _webSockets.Values)
			{
				ClientSendRoomItemAsync(client, ((GameRoom)sender).RoomId).Wait();
			}
		}

        public async Task HandleClientAsync(WebSocket client, ConnectionInfo connectionInfo)
        {
            var clientAddressInfo = string.Format($"클라이언트 {connectionInfo.Id}({connectionInfo.RemoteIpAddress?.MapToIPv4()}:{connectionInfo.RemotePort})");
            Console.WriteLine($"[접속] {clientAddressInfo}");
            _webSockets.Add(connectionInfo.Id, client);
            _nicknames.Add(connectionInfo.Id, "플레이어");
            var buffer = new byte[BUFFER_SIZE];
            
            try
            {
				while (client.State == WebSocketState.Open)
				{
					var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
					Console.WriteLine("[{0} 스레드가 처리중]", Thread.CurrentThread.ManagedThreadId);
					if (result.MessageType == WebSocketMessageType.Close)
					{
						await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "클라이언트가 연결을 종료하였습니다.", CancellationToken.None);
						break;
					}
                    
                    if (result.Count > BUFFER_SIZE)
                    {
                        throw new MessageSizeOverFlowException(result.Count);
                    }
					string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine("메시지 크기: " + result.Count);
                    await HandleMessageAsync(client, connectionInfo.Id, clientAddressInfo, message);
				}
			}
            catch (WebSocketException webSocketException)
            {
                if (webSocketException.ErrorCode != 0)
                    Console.WriteLine($"[웹소켓 에러] {clientAddressInfo}: {webSocketException.Message}");
            }
            catch (JsonException jsonException)
            {
                Console.WriteLine($"[JSON 에러] {clientAddressInfo}: {jsonException.Message}");
            }
            catch (KeyNotFoundException keyNotFoundException)
            {
                Console.WriteLine($"[프로토콜 에러] {clientAddressInfo}: {keyNotFoundException.Message}");
            }
            catch (MessageSizeOverFlowException messageSizeOverFlowException)
            {
                Console.WriteLine($"[메시지 크기 에러] {clientAddressInfo}: {messageSizeOverFlowException.Message}");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[처리되지 않은 예외] {clientAddressInfo}: {exception.Message}");
            }
            finally
            {
                if (client.State == WebSocketState.Open || client.State == WebSocketState.CloseReceived || client.State == WebSocketState.CloseSent)
                {
                    await client.CloseAsync(WebSocketCloseStatus.InternalServerError, "서버 로직 에러로 연결을 종료합니다.", CancellationToken.None);
                }
            }
			Console.WriteLine($"[접속 해제] {clientAddressInfo}");
            _webSockets.Remove(connectionInfo.Id);
            _nicknames.Remove(connectionInfo.Id);
            foreach (var gameRoom in _gameRooms)
            {
                if (gameRoom.IsPlayerJoined(connectionInfo.Id))
                {
                    gameRoom.RemovePlayer(connectionInfo.Id);
                }
            }
		}

        private async Task HandleMessageAsync(WebSocket client, string clientId, string clientAddressInfo, string message)
        {
            using JsonDocument jsonDocument = JsonDocument.Parse(message);
            JsonElement rootElement = jsonDocument.RootElement;
            JsonElement messageName = rootElement.GetProperty("msg");

            var messageNameString = messageName.GetString();
            switch (messageNameString)
            {
                case "createRoom":
                    {
                        string roomName = rootElement.GetProperty("roomName").GetString() ?? "";
                        if (roomName.Length == 0)
                        {
                            await ClientFlashMessageAsync(client, "방 이름을 입력해 주세요.", FlashMessageType.Error);
                            return;
                        }
                        foreach (var gameRoom in _gameRooms)
                        {
                            if (gameRoom.State == GameRoom.RoomState.Inactive)
                            {
                                gameRoom.RoomName = roomName;
                                if (gameRoom.AddPlayer(clientId))
                                {
                                    foreach (WebSocket webSocket in _webSockets.Values)
                                    {
                                        await ClientSendRoomItemAsync(webSocket, gameRoom.RoomId);
                                    }
                                }
                                return;
                            }
                        }
                        await ClientFlashMessageAsync(client, "최대 생성할 수 있는 방의 개수를 초과했습니다.", FlashMessageType.Error);
                        return;
                    }
                case "requestAllRoomDatas":
                    {
                        await ClientSendAllRoomItemsAsync(client);
                        return;
                    }
                default:
                    {
                        Console.WriteLine($"[알 수 없는 메시지 수신]: {messageNameString}");
						return;
                    }
            }
        }

        private async Task ClientFlashMessageAsync(WebSocket client, string message, FlashMessageType messageType)
        {
            int messageTypeNumber;
            if (messageType == FlashMessageType.Info)
                messageTypeNumber = 0;
            else if (messageType == FlashMessageType.Warning)
                messageTypeNumber = 1;
            else
                messageTypeNumber = 2;
            var messageObject = new
            {
                msg = "flash",
                text = message,
                flashType = messageTypeNumber
            };
            await SendClientAsync(client, messageObject);
        }

        private async Task ClientSendAllRoomItemsAsync(WebSocket client)
        {
            foreach (var gameRoom in _gameRooms)
            {
                await ClientSendRoomItemAsync(client, gameRoom.RoomId);
            }
        }

        private async Task ClientRemoveRoomItemAsync(WebSocket client, int roomId)
        {
            var messageObject = new
            {
                msg = "removeRoomItem",
                roomId = roomId
            };
            await SendClientAsync(client, messageObject);
        }

        private async Task ClientSendRoomItemAsync(WebSocket client, int roomId)
        {
            if (roomId < 0 || roomId >= MAX_ROOM_COUNT)
            {
                return;
            }

            GameRoom gameRoom = _gameRooms[roomId];
            string roomOwnerName = "알 수 없음";
            if (_nicknames.Contains)
			if (gameRoom.State == GameRoom.RoomState.Waiting)
			{
				var messageObject = new
				{
					msg = "sendRoomItem",
					roomId = gameRoom.RoomId,
					roomName = gameRoom.RoomName,
					roomOwner = _nicknames[gameRoom.RoomOwnerId ?? ""]
				};
                
				await SendClientAsync(client, messageObject);
			}
		}

        private async Task ClientNotifyKickedFromGameRoomAsync(WebSocket client)
        {
            var messageObject = new
            {
                msg = "kickedFromGameRoom"
            };

            await SendClientAsync(client, messageObject);
        }

        private async Task SendClientAsync(WebSocket client, object jsonObject)
        {
            string jsonString = JsonSerializer.Serialize(jsonObject);
            byte[] bytes = Encoding.UTF8.GetBytes(jsonString);
            var segment = new ArraySegment<byte>(bytes);

            if (segment.Count > BUFFER_SIZE)
            {
                throw new MessageSizeOverFlowException(segment.Count);
            }

            await client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
