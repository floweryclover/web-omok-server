namespace WebOmokServer
{ 
	public class GameRoom
	{
		public struct GameRoomChanges
		{
			public enum PlayerJoinLeaveResult { Joined, Left }
			public GameRoomChanges()
			{
				JoinedLeftPlayers = new List<Tuple<string, PlayerJoinLeaveResult>>();
				NewOwnerId = null;
				NewRoomState = null;
				NewRoomName = null;
			}
			public List<Tuple<string, PlayerJoinLeaveResult>> JoinedLeftPlayers { get; set; }
			public string? NewOwnerId { get; set; }

			public string? NewRoomName { get; set; }

			public RoomState? NewRoomState { get; set; }

			public static GameRoomChanges Empty = new GameRoomChanges();
		}
		public enum RoomState
		{
			Inactive,
			Waiting,
			Playing,
		}

		public enum StoneColor
		{
			Black,
			White,
			None,
		}

		private const string INITIAL_GAME_ROOM_NAME = "오목";

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
		public string? BlackPlayer
		{
			get => _blackPlayer;
		}

		private string? _whitePlayer;
		public string? WhitePlayer
		{
			get => _whitePlayer;
		}

		public GameRoom(int roomId)
		{
			RoomId = roomId;
			_roomName = INITIAL_GAME_ROOM_NAME;
			_blackPlayer = null;
			_whitePlayer = null;
			_roomOwnerId = null;
		}

		public GameRoomChanges AddPlayer(string playerId)
		{
			var Switch = (bool condition, Action andThen, Action orElse) => { if (condition) andThen(); else orElse(); };
			var changes = GameRoomChanges.Empty;

            if (State == RoomState.Playing)
            {
                return GameRoomChanges.Empty;
            }

            if (_blackPlayer != null && _whitePlayer != null)
            {
                return GameRoomChanges.Empty;
            }
            else if (_blackPlayer == null && _whitePlayer == null)
            {
                _state = RoomState.Waiting;
				changes.NewRoomState = RoomState.Waiting;

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
					return GameRoomChanges.Empty;
				}

				Switch(_blackPlayer == null, () => _blackPlayer = playerId, () => _whitePlayer = playerId);
            }

			changes.JoinedLeftPlayers.Add(new Tuple<string, GameRoomChanges.PlayerJoinLeaveResult>(playerId, GameRoomChanges.PlayerJoinLeaveResult.Joined));

			return changes;
		}

		public GameRoomChanges InactivateRoom()
		{
			var changesOne = SetPlayerId(ref _blackPlayer, null, _whitePlayer);
			var changesTwo = SetPlayerId(ref _whitePlayer, null, _blackPlayer);

			var union = new GameRoomChanges();
			union.JoinedLeftPlayers.AddRange(changesOne.JoinedLeftPlayers);
			union.JoinedLeftPlayers.AddRange(changesTwo.JoinedLeftPlayers);
			union.NewRoomState = State;
			union.NewOwnerId = RoomOwnerId;
			union.NewRoomName = RoomName;

			return union;
		}

		public bool IsPlayerJoined(string playerId) => (WhitePlayer != null && WhitePlayer == playerId) || (BlackPlayer != null && BlackPlayer == playerId);

		public bool IsJoinable() => BlackPlayer != null || WhitePlayer != null;

		public GameRoomChanges RemovePlayer(string playerId)
		{
			if (_blackPlayer == playerId)
			{
				return SetPlayerId(ref _blackPlayer, null, _whitePlayer);
			}
			else if (_whitePlayer == playerId)
			{
				return SetPlayerId(ref _whitePlayer, null, _blackPlayer);
			}
			else
			{
				return GameRoomChanges.Empty;
			}
		}
		public GameRoomChanges SetBlackPlayerId(string? newValue) => SetPlayerId(ref _blackPlayer, newValue, _whitePlayer);
		public GameRoomChanges SetWhitePlayerId(string? newValue) => SetPlayerId(ref _whitePlayer, newValue, _blackPlayer);

		public GameRoomChanges SetRoomName(string newName)
		{
			GameRoomChanges changes = new GameRoomChanges();
			_roomName = newName;
			changes.NewRoomName = newName;

			return changes;
		}

		private GameRoomChanges SetPlayerId(ref string? currentPlayerField, string? newValue, string? otherPlayerField)
		{
			GameRoomChanges changes = GameRoomChanges.Empty;
            if (currentPlayerField == newValue)
            {
                return changes;
            }

            if (currentPlayerField != null)
            {
				changes.JoinedLeftPlayers.Add(new Tuple<string, GameRoomChanges.PlayerJoinLeaveResult>(currentPlayerField, GameRoomChanges.PlayerJoinLeaveResult.Left));
            }

            currentPlayerField = newValue;

            if (newValue == null)
            {
                if (otherPlayerField != null)
                {
                    _roomOwnerId = otherPlayerField;
					changes.NewOwnerId = otherPlayerField;
                }
                else
                {
                    _roomOwnerId = null;
					_state = RoomState.Inactive;
					_roomName = INITIAL_GAME_ROOM_NAME;
					changes.NewRoomState = RoomState.Inactive;
                }
            }

			return changes;
        }

		public GameRoomChanges StartGame()
		{
			if (State != RoomState.Waiting || BlackPlayer == null || WhitePlayer == null)
			{
				return GameRoomChanges.Empty;
			}

			_state = RoomState.Playing;

			var changes = GameRoomChanges.Empty;
			changes.NewRoomState = RoomState.Playing;
			return changes;
		}
	}
}
