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
		private const int BUFFER_SIZE = 512;
        private const string DEFAULT_USER_NICKNAME = "플레이어";

        private GameRoom[] _gameRooms = new GameRoom[MAX_ROOM_COUNT];
        private SemaphoreSlim[] _gameRoomSemaphoreSlim = new SemaphoreSlim[MAX_ROOM_COUNT];

        private Dictionary<string, string> _nicknames = new Dictionary<string, string>();
		private Dictionary<string, Client> _clients = new Dictionary<string, Client>();
        private Dictionary<string, SemaphoreSlim> _clientLocks = new Dictionary<string, SemaphoreSlim>();
        private SemaphoreSlim _dictionarySemaphoreSlim = new SemaphoreSlim(1, 1);

        public OmokServer()
        {
            for (int roomId = 0; roomId<MAX_ROOM_COUNT; roomId++)
            {
                var newRoom = new GameRoom(roomId);
                _gameRooms[roomId] = newRoom;
                _gameRoomSemaphoreSlim[roomId] = new SemaphoreSlim(1, 1);
            }
        }

        public async Task HandleClientLoopAsync(Client client)
        {
            var closeWebSocket = (string message) => client.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, message, CancellationToken.None);
            await OnClientConnected(client);
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
                    if (!await HandleMessageAsync(client.Id, message))
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
            await OnClientDisconnected(client);
		}

        private async Task OnClientConnected(Client client)
        {
            Console.WriteLine($"[접속] {client}");
            await _dictionarySemaphoreSlim.WaitAsync();
            try
            {
                _nicknames.Add(client.Id, "플레이어");
                _clients.Add(client.Id, client);
                _clientLocks.Add(client.Id, new SemaphoreSlim(1, 1));
            }
            finally
            {
                _dictionarySemaphoreSlim.Release();
            }
        }

        private async Task OnClientDisconnected(Client client)
        {
            var clientId = client.Id;
            Console.WriteLine($"[접속 해제] {client}");

            await _dictionarySemaphoreSlim.WaitAsync();
            try
            {
                await _clientLocks[clientId].WaitAsync();
                try
                {
                    client.Dispose();
                }
                finally
                {
                    _clientLocks[clientId].Release();
                }
                _clientLocks.Remove(clientId);
                _clients.Remove(clientId);
                _nicknames.Remove(clientId);
            }
            finally
            {
                _dictionarySemaphoreSlim.Release();
            }

            foreach (var gameRoom in _gameRooms)
            {
                var changes = new GameRoom.GameRoomChanges();
                await _gameRoomSemaphoreSlim[gameRoom.RoomId].WaitAsync();
                try
                {
                    if (gameRoom.IsPlayerJoined(clientId))
                    {
                        changes = gameRoom.RemovePlayer(clientId);
                    }
                }
                finally
                {
                    _gameRoomSemaphoreSlim[gameRoom.RoomId].Release();
                }

                if (changes.JoinedLeftPlayers.Count > 0)
                {
                    await HandleGameRoomChanges(gameRoom, changes);
                }
            }
        }

        private async Task HandleGameRoomChanges(GameRoom gameRoom, GameRoom.GameRoomChanges gameRoomChanges)
        {
            var peerIds = await GetAllPeerIds();
			string? blackPlayerId = null;
			string? whitePlayerId = null;
            await _gameRoomSemaphoreSlim[gameRoom.RoomId].WaitAsync();
			try
			{
				blackPlayerId = gameRoom.BlackPlayer;
				whitePlayerId = gameRoom.WhitePlayer;
			}
			finally
			{
				_gameRoomSemaphoreSlim[gameRoom.RoomId].Release();
			}

			foreach (var joinedLeftPlayer in gameRoomChanges.JoinedLeftPlayers)
            {
                string clientId = joinedLeftPlayer.Item1;
                var result = joinedLeftPlayer.Item2;

                if (result == GameRoom.GameRoomChanges.PlayerJoinLeaveResult.Joined)
                {
                    string currentRoomName = "";
                    string? opponentClientId = null;
                    bool isOwner = false;
                    var roomState = GameRoom.RoomState.Inactive;
					await _gameRoomSemaphoreSlim[gameRoom.RoomId].WaitAsync();
                    try
                    {
                        currentRoomName = gameRoom.RoomName;
						opponentClientId = clientId == gameRoom.BlackPlayer ? gameRoom.WhitePlayer : gameRoom.BlackPlayer;
                        isOwner = gameRoom.RoomOwnerId == clientId;
                        roomState = gameRoom.State;
					}
                    finally
                    {
                        _gameRoomSemaphoreSlim[gameRoom.RoomId].Release();
                    }
                    
					string myNickname = "";
					string opponentNickname = "";
					await _dictionarySemaphoreSlim.WaitAsync();
                    try
                    {
                        myNickname = _nicknames[clientId];
                        if (opponentClientId != null && _nicknames.ContainsKey(opponentClientId))
                        {
                            opponentNickname = _nicknames[opponentClientId];
                        }
                    }
                    finally
                    {
                        _dictionarySemaphoreSlim.Release();
                    }

					await ClientUpdateCurrentRoomNameAsync(clientId, currentRoomName);
					await ClientEnterGameRoomAsync(clientId, gameRoom.RoomId);
					await ClientUpdateMyNameAsync(clientId, myNickname);
                    await ClientUpdateOpponentNameAsync(clientId, opponentNickname);
                    await ClientUpdateOwnershipAsync(clientId, isOwner);
                    await ClientUpdateRoomStateAsync(clientId, gameRoom.RoomId, roomState);
                    await ClientUpdateStoneColorAsync(clientId, GameRoom.StoneColor.None);

                    if (opponentClientId != null)
                    {
						await ClientUpdateCurrentRoomNameAsync(opponentClientId, currentRoomName);
						await ClientEnterGameRoomAsync(opponentClientId, gameRoom.RoomId);
						await ClientUpdateMyNameAsync(opponentClientId, opponentNickname);
						await ClientUpdateOpponentNameAsync(opponentClientId, myNickname);
						await ClientUpdateOwnershipAsync(opponentClientId, !isOwner);
						await ClientUpdateRoomStateAsync(opponentClientId, gameRoom.RoomId, roomState);
						await ClientUpdateStoneColorAsync(opponentClientId, GameRoom.StoneColor.None);
					}
                }
                else
                {
                    await ClientNotifyKickedFromGameRoomAsync(clientId);
                    var ClearOpponentNameOf = async (string? clientId) =>
                    {
                        if (clientId == null)
                        {
                            return;
                        }
                        await ClientUpdateOpponentNameAsync(clientId, "");
                    };
                    await ClearOpponentNameOf(gameRoom.BlackPlayer);
                    await ClearOpponentNameOf(gameRoom.WhitePlayer);
                }
            }

            if (gameRoomChanges.NewRoomName != null || gameRoomChanges.NewOwnerId != null || gameRoomChanges.NewRoomState != null)
            {
                foreach (var peerId in peerIds)
                {
                    await ClientSendRoomItemAsync(peerId, gameRoom.RoomId);
                }
            }

            if (gameRoomChanges.NewRoomName != null)
            {
				if (blackPlayerId != null)
				{
					await ClientUpdateCurrentRoomNameAsync(blackPlayerId, gameRoomChanges.NewRoomName);
				}
				if (whitePlayerId != null)
				{
					await ClientUpdateCurrentRoomNameAsync(whitePlayerId, gameRoomChanges.NewRoomName);
				}
			}

            if (gameRoomChanges.NewOwnerId != null)
            {
				if (blackPlayerId != null)
				{
					await ClientUpdateOwnershipAsync(blackPlayerId, gameRoomChanges.NewOwnerId == blackPlayerId);
				}
				if (whitePlayerId != null)
				{
					await ClientUpdateOwnershipAsync(whitePlayerId, gameRoomChanges.NewOwnerId == whitePlayerId);
				}
			}

            if (gameRoomChanges.NewRoomState != null)
            {
                if (gameRoomChanges.NewRoomState == GameRoom.RoomState.Playing)
                {
                    var SendStoneColorInfo = async (string? clientId) =>
                    {
                        if (clientId == null)
                        {
                            return;
                        }
                        await ClientUpdateStoneColorAsync(clientId, gameRoom.BlackPlayer == clientId ? GameRoom.StoneColor.Black : GameRoom.StoneColor.White);
                        await ClientUpdateRoomStateAsync(clientId, gameRoom.RoomId, GameRoom.RoomState.Playing);
                    };
                    await SendStoneColorInfo(gameRoom.BlackPlayer);
                    await SendStoneColorInfo(gameRoom.WhitePlayer);
                }
                else if (gameRoomChanges.NewRoomState == GameRoom.RoomState.Inactive)
                {
                    foreach (var peerId in peerIds)
                    {
                        await ClientUpdateRoomStateAsync(peerId, gameRoom.RoomId, GameRoom.RoomState.Inactive);
                    }
                }
            }

            if (gameRoomChanges.NewPlacedStone != null)
            {
                var SendPlacedStoneInfo = async (string? clientId) =>
                {
                    if (clientId == null)
                    {
                        return;
                    }
                    await ClientPlaceStoneAsync(clientId, gameRoomChanges.NewPlacedStone.Item1, gameRoomChanges.NewPlacedStone.Item2, gameRoomChanges.NewPlacedStone.Item3);
                };
                await SendPlacedStoneInfo(gameRoom.BlackPlayer);
                await SendPlacedStoneInfo(gameRoom.WhitePlayer);
            }

            if (gameRoomChanges.Message != null)
            {
                var SendGameMessage = async (string? clientId) =>
                {
                    if (clientId == null)
                    {
                        return;
                    }
                    await ClientGameMessageAsync(clientId, gameRoomChanges.Message);
                };
                await SendGameMessage(gameRoom.BlackPlayer);
                await SendGameMessage(gameRoom.WhitePlayer);
            }
        }

        private async Task<List<string>> GetAllPeerIds()
        {
            await _dictionarySemaphoreSlim.WaitAsync();
            try
            {
                var copiedPeerIds = new List<string>(_clients.Keys);
                return copiedPeerIds;
            }
            finally
            {
                _dictionarySemaphoreSlim.Release();
            }
        }
    }
}
