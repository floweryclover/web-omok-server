using System.Linq;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text;
using System.Net;

namespace WebOmokServer
{
    public partial class OmokServer
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <exception cref="JsonException"></exception>
        /// <exception cref="MessageSizeOverFlowException"></exception>
        private async Task HandleMessageAsync(Client client, string message)
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
                                if (gameRoom.AddPlayer(client.Id))
                                {
                                    foreach (var peer in _clients.Values)
                                    {
                                        await ClientSendRoomItemAsync(peer, gameRoom.RoomId);
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        /// <param name="jsonObject"></param>
        /// <returns></returns>
        /// <exception cref="MessageSizeOverFlowException"></exception>
        /// <exception cref="JsonException"></exception>
        private async Task SendClientAsync(Client client, object jsonObject)
        {
            string jsonString;
            try
            {
                jsonString = JsonSerializer.Serialize(jsonObject);
            }
            catch (NotSupportedException e)
            {
                throw new JsonException("전달된 객체를 유효한 JSON 문자열로 변환할 수 없습니다.", e);
            }
            byte[] bytes = Encoding.UTF8.GetBytes(jsonString);
            var segment = new ArraySegment<byte>(bytes);

            if (segment.Count > BUFFER_SIZE)
            {
                Console.Write("[송신 에러]");
                throw new MessageSizeOverFlowException(segment.Count);
            }

            await client.WebSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task ClientFlashMessageAsync(Client client, string message, FlashMessageType messageType)
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

        private async Task ClientSendAllRoomItemsAsync(Client client)
        {
            foreach (var gameRoom in _gameRooms)
            {
                await ClientSendRoomItemAsync(client, gameRoom.RoomId);
            }
        }

        private async Task ClientRemoveRoomItemAsync(Client client, int roomId)
        {
            var messageObject = new
            {
                msg = "removeRoomItem",
                roomId = roomId
            };
            await SendClientAsync(client, messageObject);
        }

        private async Task ClientSendRoomItemAsync(Client client, int roomId)
        {
            if (roomId < 0 || roomId >= MAX_ROOM_COUNT)
            {
                return;
            }

            GameRoom gameRoom = _gameRooms[roomId];
            if (gameRoom.RoomOwnerId == null)
            {
                return;
            }

            if (gameRoom.State == GameRoom.RoomState.Waiting)
            {
                var messageObject = new
                {
                    msg = "sendRoomItem",
                    roomId = gameRoom.RoomId,
                    roomName = gameRoom.RoomName,
                    roomOwner = _nicknames[gameRoom.RoomOwnerId]
                };

                await SendClientAsync(client, messageObject);
            }
        }

        private async Task ClientNotifyKickedFromGameRoomAsync(Client client)
        {
            var messageObject = new
            {
                msg = "kickedFromGameRoom"
            };

            await SendClientAsync(client, messageObject);
        }
    }
}
