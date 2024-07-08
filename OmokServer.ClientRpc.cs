using System.Linq;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text;
using System.Net;
using Microsoft.VisualBasic;

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

        private async Task ClientEnterGameRoomAsync(string clientId, int roomId)
        {
            var messageObject = new
            {
                msg = "enterGameRoom",
                roomId
            };

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

        private async Task ClientUpdateRoomStateAsync(string clientId, int roomId, GameRoom.RoomState roomState)
        {
            int roomStateNumber = 0;
            if (roomState == GameRoom.RoomState.Waiting)
            {
				roomStateNumber = 1;
            }
            else if (roomState == GameRoom.RoomState.Playing)
            {
				roomStateNumber = 2;
            }

            var messageObject = new
            {
                msg = "updateRoomState",
                roomId,
				roomStateNumber,
            };

            await SendClientAsync(clientId, messageObject);
        }

        private async Task ClientUpdateOwnershipAsync(string clientId, bool isOwner)
        {
            var messageObject = new
            {
                msg = "updateOwnership",
                isOwner,
            };

            await SendClientAsync(clientId, messageObject);
        }

        private async Task ClientUpdateStoneColorAsync(string clientId, GameRoom.StoneColor myColor)
        {
            int myColorNumber = myColor switch
			{
				GameRoom.StoneColor.Black => 0,
				GameRoom.StoneColor.White => 1,
				_ => 2,
			};

            var messageObject = new
            {
                msg = "updateStoneColor",
                myColorNumber,
            };

            await SendClientAsync(clientId, messageObject);
        }

        private async Task ClientUpdateCurrentRoomNameAsync(string clientId, string roomName)
        {
            var messageObject = new
            {
                msg = "updateCurrentRoomName",
                roomName
            };

            await SendClientAsync(clientId, messageObject);
        }

        private async Task ClientUpdateMyNameAsync(string clientId, string myName)
        {
            var messageObject = new
            {
                msg = "updateMyName",
                myName
            };

            await SendClientAsync(clientId, messageObject);
        }

        private async Task ClientUpdateOpponentNameAsync(string clientId, string opponentName)
        {
            var messageObject = new
            {
                msg = "updateOpponentName",
                opponentName,
            };

            await SendClientAsync(clientId, messageObject);
        }

        private async Task ClientPlaceStoneAsync(string clientId, GameRoom.StoneColor stoneColor, int row, int column)
        {
            var stoneColorNumber = stoneColor switch
            {
                GameRoom.StoneColor.Black => 0,
                GameRoom.StoneColor.White => 1,
                _ => 2,
            };

            var messageObject = new
            {
                msg = "placeStone",
                stoneColorNumber,
                row,
                column
            };

            await SendClientAsync(clientId, messageObject);
        }

        private async Task ClientGameMessageAsync(string clientId, string message)
        {
            var messageObject = new
            {
                msg = "gameMessage",
                message,
            };

            await SendClientAsync(clientId, messageObject);
        }
    }
}
