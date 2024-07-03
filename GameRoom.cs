using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace WebOmokServer
{ 
	public class GameRoom
	{
		public class ClientIdArgs : EventArgs
		{
			public string ClientId { get; set; }

			public ClientIdArgs(string clientId)
			{
				ClientId = clientId;
			}
		}

		public enum RoomState
		{
			Inactive,
			Waiting,
			Playing,
		}

		private readonly object _lock = new object();

		public int RoomId { get; }
		private RoomState _state;
		public RoomState State
		{
			get => _state;
		}
		private string _roomName;
		public string RoomName 
		{
			get => _roomName;
		}

		private string? _roomOwnerId;
		public string? RoomOwnerId {
			get =>  _roomOwnerId;
		}
		private string? _blackPlayer;
		private string? BlackPlayer
		{
			get => _blackPlayer;
		}

		private string? _whitePlayer;
		private string? WhitePlayer
		{
			get => _whitePlayer;
		}

		public event EventHandler<ClientIdArgs>? PlayerKicked;
		public event EventHandler? GameRoomInactivated;
		public event EventHandler<ClientIdArgs>? RoomOwnerChanged;
		public event EventHandler? RoomNameChanged;

		public GameRoom(int roomId)
		{
			RoomId = roomId;
			_roomName = "같이 게임해요";
			_blackPlayer = null;
			_whitePlayer = null;
			_roomOwnerId = null;
		}

		public bool AddPlayer(string playerId)
		{
			var Switch = (bool condition, Action andThen, Action orElse) =>
			{
				if (condition)
				{
                    andThen();
				}
				else
				{
                    orElse();
				}
			};
			lock(_lock)
			{
                if (State == RoomState.Playing)
                {
                    return false;
                }

                if (_blackPlayer != null && _whitePlayer != null)
                {
                    return false;
                }
                else if (_blackPlayer == null && _whitePlayer == null)
                {
                    _state = RoomState.Waiting;
                    var random = new Random();
                    bool isBlack = random.Next(1) == 0;

					Switch(isBlack, () => _blackPlayer = playerId, () => _whitePlayer = playerId);
                    _roomOwnerId = playerId;
                }
                else
                {
					var otherPlayer = _blackPlayer == null ? _whitePlayer : _blackPlayer;
					if (otherPlayer == playerId)
					{
						return false;
					}

					Switch(_blackPlayer == null, () => _blackPlayer = playerId, () => _whitePlayer = playerId);
                }
            }
			return true;
		}

		public void InactivateRoom()
		{
			lock (_lock)
			{
				SetPlayerId(ref _blackPlayer, null, _whitePlayer);
				SetPlayerId(ref _whitePlayer, null, _blackPlayer);
			}
		}

		public bool IsPlayerJoined(string playerId)
		{
			lock (_lock)
			{
                return (WhitePlayer != null && WhitePlayer == playerId) || (BlackPlayer != null && BlackPlayer == playerId);
            }
		}

		public bool IsJoinable()
		{
			lock (_lock)
			{
                return BlackPlayer != null || WhitePlayer != null;
            }
		}

		public void RemovePlayer(string playerId)
		{
			lock (_lock)
			{
				if (_blackPlayer == playerId)
				{
					SetPlayerId(ref _blackPlayer, null, _whitePlayer);
				}
				else
				{
					SetPlayerId(ref _whitePlayer, null, _blackPlayer);
				}
			}
		}
		public void SetBlackPlayerId(string? newValue)
		{
			lock (_lock)
			{
                SetPlayerId(ref _blackPlayer, newValue, _whitePlayer);
            }
		}
		public void SetWhitePlayerId(string? newValue)
		{
			lock (_lock)
			{
                SetPlayerId(ref _whitePlayer, newValue, _blackPlayer);
            }
		}

		public void SetRoomName(string newName)
		{
			lock (_lock)
			{
				_roomName = newName;
				RoomNameChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// 동기화 없음
		/// </summary>
		/// <param name="currentPlayerField"></param>
		/// <param name="newValue"></param>
		/// <param name="otherPlayerField"></param>
		private void SetPlayerId(ref string? currentPlayerField, string? newValue, string? otherPlayerField)
		{
            if (currentPlayerField == newValue)
            {
                return;
            }

            if (currentPlayerField != null)
            {
                PlayerKicked?.Invoke(this, new ClientIdArgs(currentPlayerField));
            }

            currentPlayerField = newValue;

            if (newValue == null)
            {
                if (otherPlayerField != null)
                {
                    _roomOwnerId = otherPlayerField;
                    RoomOwnerChanged?.Invoke(this, new ClientIdArgs(otherPlayerField));
                }
                else
                {
					Console.WriteLine($"{RoomId} IS NOW INACTIVE");
                    _roomOwnerId = null;
					_state = RoomState.Inactive;
                    GameRoomInactivated?.Invoke(this, EventArgs.Empty);
                }
            }
        }
	}
}
