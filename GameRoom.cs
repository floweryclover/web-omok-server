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
		public int RoomId { get; }
		private RoomState _state;
		public RoomState State { get { return _state; } }
		private string _roomName;
		public string RoomName
		{
			get
			{
				return _roomName;
			}
			set 
			{
				_roomName = value;
				RoomNameChanged?.Invoke(this, new EventArgs());
			} 
		}

		private string? _roomOwnerId;
		public string? RoomOwnerId { get { return _roomOwnerId; } }

		private void _playerAssign(ref string? target, string? newValue)
		{
			if (target != null)
			{
				PlayerKicked?.Invoke(this, new ClientIdArgs(target));
			}
			target = newValue;
		}
		private string? _blackPlayer;
		private string? BlackPlayer {
			get
			{
				return _blackPlayer;
			}
			set
			{
				_playerAssign(ref _blackPlayer, value);
				if (value == null)
				{
					if (WhitePlayer != null)
					{
						_roomOwnerId = WhitePlayer;
						RoomOwnerChanged?.Invoke(this, new ClientIdArgs(WhitePlayer));
					}
                    else
                    {
                        _roomOwnerId = null;
                    }
                }
			}
		}

		private string? _whitePlayer;
		private string? WhitePlayer
		{
			get
			{
				return _whitePlayer;
			}
			set
			{
				_playerAssign(ref _whitePlayer, value);
				if (value == null)
				{
					if (BlackPlayer != null)
					{
						_roomOwnerId = BlackPlayer;
						RoomOwnerChanged?.Invoke(this, new ClientIdArgs(BlackPlayer));
					}
					else
					{
						_roomOwnerId = null;
					}
				}
			}
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
				
				if (isBlack)
				{
					_blackPlayer = playerId;
				}
				else
				{
					_whitePlayer = playerId;
				}
				_roomOwnerId = playerId;
			}
			else
			{
				if (_blackPlayer == null)
				{
					_blackPlayer = playerId;
				}
				else
				{
					_whitePlayer = playerId;
				}
			}
			return true;
		}

		public void RemovePlayer(string playerId)
		{
			bool someoneLeft = false;
			if (BlackPlayer != null & BlackPlayer == playerId)
			{
				BlackPlayer = null;
				someoneLeft = true;
			}
			else if (WhitePlayer != null & WhitePlayer == playerId)
			{
				WhitePlayer = null;
				someoneLeft = true;
			}

			if (someoneLeft)
			{
				if (BlackPlayer == null && WhitePlayer == null)
				{
					InactivateRoom();
				}
			}
		}

		public void InactivateRoom()
		{
			BlackPlayer = null;
			WhitePlayer = null;
			GameRoomInactivated?.Invoke(this, new EventArgs());
		}

		public bool IsPlayerJoined(string playerId)
		{
			return (WhitePlayer != null && WhitePlayer == playerId) || (BlackPlayer != null && BlackPlayer == playerId);
		}
	}
}
