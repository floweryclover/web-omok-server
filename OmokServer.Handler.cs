using System.Text.Json;

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
        private async Task<bool> HandleMessageAsync(Client client, string message)
        {
            using JsonDocument jsonDocument = JsonDocument.Parse(message);
            JsonElement json = jsonDocument.RootElement;
            JsonElement messageName = json.GetProperty("msg");

            var messageNameString = messageName.GetString();
            switch (messageNameString)
            {
                case "createRoom":
                    {
                        return await OnClientCreateRoom(client, json);
                    }
                case "requestAllRoomDatas":
                    {
                        await ClientSendAllRoomItemsAsync(client);
                        return true;
                    }
                default:
                    {
                        Console.WriteLine($"[알 수 없는 메시지 수신]: {messageNameString}");
                        await ClientFlashMessageAsync(client, "프로토콜 에러", FlashMessageType.Error);
                        return false;
                    }
            }
        }

        private async Task<bool> OnClientCreateRoom(Client client, JsonElement json)
        {
            var IsThisClientAlreadyJoinedAnyRoom = () =>
            {
                foreach (var gameRoom in _gameRooms)
                {
                    if (gameRoom.IsPlayerJoined(client.Id))
                    {
                        return true;
                    }
                }
                return false;
            };
            var IsClientEnteredEmptyRoomName = (string roomName) => roomName.Length == 0;

            if (IsThisClientAlreadyJoinedAnyRoom())
            {
                await ClientFlashMessageAsync(client, "에러가 발생하여 연결을 종료합니다.", FlashMessageType.Error);
                return false;
            }

            string roomName = json.GetProperty("roomName").GetString() ?? "";
            if (IsClientEnteredEmptyRoomName(roomName))
            {
                await ClientFlashMessageAsync(client, "방 이름을 입력해 주세요.", FlashMessageType.Error);
                return true;
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
                    return true;
                }
            }
            await ClientFlashMessageAsync(client, "최대 생성할 수 있는 방의 개수를 초과했습니다.", FlashMessageType.Error);
            return true;
        }
    }
}
