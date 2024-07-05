using System.Text.Json;

namespace WebOmokServer
{
    public partial class OmokServer
    {
        private bool IsThisClientAlreadyJoinedAnyRoom(string clientId)
        {
            foreach (var gameRoom in _gameRooms)
            {
                if (gameRoom.IsPlayerJoined(clientId))
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <exception cref="JsonException"></exception>
        /// <exception cref="MessageSizeOverFlowException"></exception>
        private async Task<bool> HandleMessageAsync(string clientId, string message)
        {
            using JsonDocument jsonDocument = JsonDocument.Parse(message);
            JsonElement json = jsonDocument.RootElement;
            JsonElement messageName = json.GetProperty("msg");

            var messageNameString = messageName.GetString();
            switch (messageNameString)
            {
                case "createRoom":
                    {
                        return await OnClientCreateRoom(clientId, json);
                    }
                case "requestAllRoomDatas":
                    {
                        await ClientSendAllRoomItemsAsync(clientId);
                        return true;
                    }
                case "requestJoinGameRoom":
                    {
                        if (IsThisClientAlreadyJoinedAnyRoom(clientId))
                        {
                            return false;
                        }
                        var roomId = json.GetProperty("roomId").GetInt32();
                        if (roomId < 0 || roomId >= MAX_ROOM_COUNT || !_gameRooms[roomId].IsJoinable())
                        {
                            await ClientFlashMessageAsync(clientId, "유효하지 않은 방입니다.", FlashMessageType.Warning);
                            var peerIds = await GetAllPeerIds();
                            foreach (var peerId in peerIds)
                            {
                                await ClientRemoveRoomItemAsync(peerId, roomId);
                            }
                            return true;
                        }

                        await _gameRoomSemaphoreSlim[roomId].WaitAsync();
                        var gameRoomChanges = GameRoom.GameRoomChanges.Empty;
                        try
                        {
                            gameRoomChanges = _gameRooms[roomId].AddPlayer(clientId);
                        }
                        finally
                        {
                            _gameRoomSemaphoreSlim[roomId].Release();
                        }
                        await HandleGameRoomChanges(_gameRooms[roomId], gameRoomChanges);

                        return true;
                    }
                default:
                    {
                        Console.WriteLine($"[알 수 없는 메시지 수신]: {messageNameString}");
                        await ClientFlashMessageAsync(clientId, "프로토콜 에러", FlashMessageType.Error);
                        return false;
                    }
            }
        }

        private async Task<bool> OnClientCreateRoom(string clientId, JsonElement json)
        {
            var IsClientEnteredEmptyRoomName = (string roomName) => roomName.Length == 0;

            if (IsThisClientAlreadyJoinedAnyRoom(clientId))
            {
                await ClientFlashMessageAsync(clientId, "에러가 발생하여 연결을 종료합니다.", FlashMessageType.Error);
                return false;
            }

            string roomName = json.GetProperty("roomName").GetString() ?? "";
            if (IsClientEnteredEmptyRoomName(roomName))
            {
                await ClientFlashMessageAsync(clientId, "방 이름을 입력해 주세요.", FlashMessageType.Error);
                return true;
            }

            foreach (var gameRoom in _gameRooms)
            {
                var gameRoomChanges = GameRoom.GameRoomChanges.Empty;
                await _gameRoomSemaphoreSlim[gameRoom.RoomId].WaitAsync();
                try
                {
                    if (gameRoom.State == GameRoom.RoomState.Inactive)
                    {
                        gameRoom.SetRoomName(roomName);
                        gameRoomChanges = gameRoom.AddPlayer(clientId);
                    }
                }
                finally
                {
                    _gameRoomSemaphoreSlim[gameRoom.RoomId].Release();
                }

                if (gameRoomChanges.NewRoomState != null)
                {
                    await HandleGameRoomChanges(gameRoom, gameRoomChanges);
                    return true;
                }
            }
            await ClientFlashMessageAsync(clientId, "최대 생성할 수 있는 방의 개수를 초과했습니다.", FlashMessageType.Error);
            return true;
        }
    }
}
