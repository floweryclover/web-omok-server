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
    }
}
