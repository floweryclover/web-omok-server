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
				NewPlacedStone = null;
				Message = null;
			}
			public List<Tuple<string, PlayerJoinLeaveResult>> JoinedLeftPlayers { get; set; }
			public string? NewOwnerId { get; set; }

			public string? NewRoomName { get; set; }

			public RoomState? NewRoomState { get; set; }

			public Tuple<StoneColor, int, int>? NewPlacedStone { get; set; }

			public string? Message { get; set; }
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
		private StoneColor[,] _board;
		private StoneColor _turn;

		public GameRoom(int roomId)
		{
			RoomId = roomId;
			_roomName = INITIAL_GAME_ROOM_NAME;
			_blackPlayer = null;
			_whitePlayer = null;
			_roomOwnerId = null;
			_board = new StoneColor[15, 15];
			for (int row=0; row<15; row++)
			{
				for (int column=0; column<15; column++)
				{
					_board[row, column] = StoneColor.None;
				}
			}
			_turn = StoneColor.Black;
		}

		public GameRoomChanges AddPlayer(string playerId)
		{
			var Switch = (bool condition, Action andThen, Action orElse) => { if (condition) andThen(); else orElse(); };
			var changes = new GameRoomChanges();

            if (State == RoomState.Playing)
            {
                return changes;
            }

            if (_blackPlayer != null && _whitePlayer != null)
            {
                return changes;
            }
            else if (_blackPlayer == null && _whitePlayer == null)
            {
                _state = RoomState.Waiting;
				changes.NewRoomState = RoomState.Waiting;

                var random = new Random();
                bool isBlack = random.Next(1) == 0;

				Switch(isBlack, () => SetBlackPlayerId(playerId), () => SetWhitePlayerId(playerId));
                _roomOwnerId = playerId;
            }
            else
            {
				var otherPlayer = _blackPlayer == null ? _whitePlayer : _blackPlayer;
				if (otherPlayer == playerId)
				{
					return changes;
				}

				Switch(_blackPlayer == null, () => SetBlackPlayerId(playerId), () => SetWhitePlayerId(playerId));
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
				return new GameRoomChanges();
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
			var changes = new GameRoomChanges();
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
                if (State == RoomState.Waiting && otherPlayerField != null)
                {
                    _roomOwnerId = otherPlayerField;
					changes.NewOwnerId = otherPlayerField;
                }
                else
                {
					if (otherPlayerField != null)
					{
						changes.JoinedLeftPlayers.Add(new Tuple<string, GameRoomChanges.PlayerJoinLeaveResult>(otherPlayerField, GameRoomChanges.PlayerJoinLeaveResult.Left));
					}
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
			var changes = new GameRoomChanges();
			if (State != RoomState.Waiting || BlackPlayer == null || WhitePlayer == null)
			{
				return changes;
			}

			_state = RoomState.Playing;
			_turn = StoneColor.Black;
			for (int row = 0; row < 15; row++)
			{
				for (int column = 0; column < 15; column++)
				{
					_board[row, column] = StoneColor.None;
				}
			}
			changes.NewRoomState = RoomState.Playing;
			changes.Message = "흑돌의 차례";

			return changes;
		}

		public GameRoomChanges PlaceStone(string playerId, int row, int column)
		{
			var changes = new GameRoomChanges();
			if (playerId != BlackPlayer && playerId != WhitePlayer
				|| row < 0 || row >= 15
				|| column < 0 || column >= 15
				|| _board[row, column] != StoneColor.None)
			{
				return changes;
			}

			var stoneColor = playerId == BlackPlayer ? StoneColor.Black : StoneColor.White;
			if (_turn != stoneColor)
			{
				return changes;
			}

			_board[row, column] = _turn;
			changes.NewPlacedStone = new Tuple<StoneColor, int, int>(_turn, row, column);

			var winner = CheckIfWinnerExists();
			if (winner != StoneColor.None)
			{
				_turn = StoneColor.None;
				changes.Message = winner == StoneColor.Black ? "흑돌의 승리입니다. 게임이 종료되었습니다." : "백돌의 승리입니다. 게임이 종료되었습니다.";
			}
			else
			{

				_turn = _turn == StoneColor.Black ? StoneColor.White : StoneColor.Black;
				changes.Message = _turn == StoneColor.Black ? "흑돌의 차례" : "백돌의 차례";
			}

			return changes;
		}

		private StoneColor CheckIfWinnerExists()
		{
			for (int row=0; row < 15; row++)
			{
				for (int column=0; column < 15-4; column++)
				{
					if (_board[row, column] == StoneColor.None)
						continue;
					if (
						_board[row, column] == _board[row, column + 1]
						&& _board[row, column] == _board[row, column + 2]
						&& _board[row, column] == _board[row, column + 3]
						&& _board[row, column] == _board[row, column + 4])
						return _board[row, column];
				}
			}

			for (int row = 0; row < 15-4; row++)
			{
				for (int column = 0; column < 15; column++)
				{
					if (_board[row, column] == StoneColor.None)
						continue;
					if (
						_board[row, column] == _board[row+1, column]
						&& _board[row, column] == _board[row+2, column]
						&& _board[row, column] == _board[row+3, column]
						&& _board[row, column] == _board[row+4, column])
						return _board[row, column];
				}
			}

			for (int row = 0; row < 15 - 4; row++)
			{
				for (int column = 0; column < 15 - 4; column++)
				{
					if (_board[row, column] == StoneColor.None)
						continue;
					if (
						_board[row, column] == _board[row + 1, column + 1]
						&& _board[row, column] == _board[row + 2, column + 2]
						&& _board[row, column] == _board[row + 3, column + 3]
						&& _board[row, column] == _board[row + 4, column + 4])
						return _board[row, column];
				}
			}

			for (int row = 0; row < 15 - 4; row++)
			{
				for (int column = 0; column < 15 - 4; column++)
				{
					if (_board[row, column + 4] == StoneColor.None)
						continue;
					if (
						_board[row, column + 4] == _board[row + 1, column + 3]
						&& _board[row, column + 4] == _board[row + 2, column + 2]
						&& _board[row, column + 4] == _board[row + 3, column + 1]
						&& _board[row, column + 4] == _board[row + 4, column])
						return _board[row, column + 4];
				}
			}

			return StoneColor.None;
		}
	}
}
