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

        private async Task ClientEnterGameRoomAsync(Client client)
        {
            var messageObject = new
            {
                msg = "enterGameRoom",
            };

            await SendClientAsync(client, messageObject);
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
