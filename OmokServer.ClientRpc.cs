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
        private async Task SendClientAsync(string clientId, object jsonObject)
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

            await _dictionarySemaphoreSlim.WaitAsync();
            try
            {
                if (!_clients.ContainsKey(clientId))
                {
                    return;
                }

                await _clientLocks[clientId].WaitAsync();
                try
                {
                    await _clients[clientId].WebSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                finally
                {
                    _clientLocks[clientId].Release();
                }
            }
            finally
            {
                _dictionarySemaphoreSlim.Release();
            }
        }

        private async Task ClientFlashMessageAsync(string clientId, string message, FlashMessageType messageType)
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
            await SendClientAsync(clientId, messageObject);
        }

        private async Task ClientSendAllRoomItemsAsync(string clientId)
        {
            foreach (var gameRoom in _gameRooms)
            {
                await ClientSendRoomItemAsync(clientId, gameRoom.RoomId);
            }
        }

        private async Task ClientRemoveRoomItemAsync(string clientId, int roomId)
        {
            var messageObject = new
            {
                msg = "removeRoomItem",
                roomId = roomId
            };
            await SendClientAsync(clientId, messageObject);
        }

        private async Task ClientSendRoomItemAsync(string clientId, int roomId)
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

                await SendClientAsync(clientId, messageObject);
            }
        }

        private async Task ClientEnterGameRoomAsync(string clientId)
        {
            var messageObject = new
            {
                msg = "enterGameRoom",
            };

            await SendClientAsync(clientId, messageObject);
        }

        private async Task ClientUpdateGameRoomInfoAsync(string clientId)
        {
            GameRoom? gameRoom = null;
            foreach (var room in _gameRooms)
            {
                if (room.IsPlayerJoined(clientId))
                {
                    gameRoom = room;
                    break;
                }
            }

            if (gameRoom == null)
            {
                return;
            }

            await _gameRoomSemaphoreSlim[gameRoom.RoomId].WaitAsync();
            object? messageObject = null;
            try
            {
                if (!gameRoom.IsPlayerJoined(clientId))
                {
                    return;
                }

                string? opponentClientId = clientId == gameRoom.BlackPlayer ? gameRoom.WhitePlayer : gameRoom.BlackPlayer;
                string? opponentName = null;
                if (opponentClientId != null)
                {
                    _nicknames.TryGetValue(opponentClientId, out opponentName);
                }

                int myColor = 2;
                int opponentColor = 2;
                if (gameRoom.State == GameRoom.RoomState.Playing)
                {
                    myColor = clientId == gameRoom.BlackPlayer ? 0 : 1;
                    opponentColor = 1 - myColor;
                }

                messageObject = new
                {
                    msg = "updateGameRoomInfo",
                    roomName = gameRoom.RoomName,
                    myName = _nicknames[clientId],
                    myColor,
                    opponentName,
                    opponentColor,
                    isOwner = gameRoom.RoomOwnerId == clientId,
                };
            }
            finally
            {
                _gameRoomSemaphoreSlim[gameRoom.RoomId].Release();
            }

            if (messageObject == null)
            {
                return;
            }

            await SendClientAsync(clientId, messageObject);
        }

        private async Task ClientNotifyKickedFromGameRoomAsync(string clientId)
        {
            var messageObject = new
            {
                msg = "kickedFromGameRoom"
            };

            await SendClientAsync(clientId, messageObject);
        }

        private async Task ClientChangeRoomStateAsync(string clientId, int roomId)
        {
            var gameRoom = _gameRooms[roomId];
            int roomState = 0;
            if (gameRoom.State == GameRoom.RoomState.Waiting)
            {
                roomState = 1;
            }
            else if (gameRoom.State == GameRoom.RoomState.Playing)
            {
                roomState = 2;
            }

            var messageObject = new
            {
                msg = "changeRoomState",
                roomId,
                roomState,
            };

            await SendClientAsync(clientId, messageObject);
        }
    }
}
