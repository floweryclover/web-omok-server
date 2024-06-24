using System.Drawing;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace WebOmokServer
{
    public class OmokServer
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
        private Dictionary<string, WebSocket> _webSockets = new Dictionary<string, WebSocket>();
        private const int BUFFER_SIZE = 256;

        enum FlashMessageType { INFO, WARNING, ERROR }

        public async Task HandleClientAsync(WebSocket client, ConnectionInfo connectionInfo)
        {
            var clientAddressInfo = string.Format($"클라이언트 {connectionInfo.Id}({connectionInfo.RemoteIpAddress.MapToIPv4()}:{connectionInfo.RemotePort})");
            Console.WriteLine($"[접속] {clientAddressInfo}");
            var buffer = new byte[BUFFER_SIZE];
            try
            {
				while (client.State == WebSocketState.Open)
				{
					var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
					Console.WriteLine("[{0} 스레드가 처리중]", Thread.CurrentThread.ManagedThreadId);
					if (result.MessageType == WebSocketMessageType.Close)
					{
						await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "클라이언트가 연결을 종료하였습니다.", CancellationToken.None);
						break;
					}
                    
                    if (result.Count > BUFFER_SIZE)
                    {
                        throw new MessageSizeOverFlowException(result.Count);
                    }
					string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine("메시지 크기: " + result.Count);
                    await HandleMessageAsync(client, clientAddressInfo, message);
				}
			}
            catch (WebSocketException webSocketException)
            {
                if (webSocketException.ErrorCode != 0)
                    Console.WriteLine($"[웹소켓 에러] {clientAddressInfo}: {webSocketException.Message}");
            }
            catch (JsonException jsonException)
            {
                Console.WriteLine($"[JSON 에러] {clientAddressInfo}: {jsonException.Message}");
            }
            catch (KeyNotFoundException keyNotFoundException)
            {
                Console.WriteLine($"[프로토콜 에러] {clientAddressInfo}: {keyNotFoundException.Message}");
            }
            catch (MessageSizeOverFlowException messageSizeOverFlowException)
            {
                Console.WriteLine($"[메시지 크기 에러] {clientAddressInfo}: {messageSizeOverFlowException.Message}");
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[처리되지 않은 예외] {clientAddressInfo}: {exception.Message}");
            }
            finally
            {
                if (client.State == WebSocketState.Open || client.State == WebSocketState.CloseReceived || client.State == WebSocketState.CloseSent)
                {
                    await client.CloseAsync(WebSocketCloseStatus.InternalServerError, "서버 로직 에러로 연결을 종료합니다.", CancellationToken.None);
                }
            }
			Console.WriteLine($"[접속 해제] {clientAddressInfo}");
		}

        private async Task HandleMessageAsync(WebSocket client, string clientAddressInfo, string message)
        {
            using JsonDocument jsonDocument = JsonDocument.Parse(message);
            JsonElement rootElement = jsonDocument.RootElement;
            JsonElement messageName = rootElement.GetProperty("msg");

            var messageNameString = messageName.GetString();
            switch (messageNameString)
            {
                case "createRoom":
                    {
                        var roomName = rootElement.GetProperty("roomName").GetString();
                        await ClientFlashMessageAsync(client, roomName, FlashMessageType.INFO);
                        return;
                    }
                default:
                    {
                        Console.WriteLine($"[알 수 없는 메시지 수신]: {messageNameString}");
						return;
                    }
            }
        }

        private async Task ClientFlashMessageAsync(WebSocket client, string message, FlashMessageType messageType)
        {
            int messageTypeNumber;
            if (messageType == FlashMessageType.INFO)
                messageTypeNumber = 0;
            else if (messageType == FlashMessageType.WARNING)
                messageTypeNumber = 1;
            else
                messageTypeNumber = 2;
            var messageObject = new
            {
                msg = "flash",
                text = message,
                flashType = messageTypeNumber
            };
            await SendClientAsync(client, messageObject);
        }

        private async Task SendClientAsync(WebSocket client, object jsonObject)
        {
            string jsonString = JsonSerializer.Serialize(jsonObject);
            byte[] bytes = Encoding.UTF8.GetBytes(jsonString);
            var segment = new ArraySegment<byte>(bytes);

            if (segment.Count > BUFFER_SIZE)
            {
                throw new MessageSizeOverFlowException(segment.Count);
            }

            await client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
