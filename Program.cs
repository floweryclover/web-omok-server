using System.Net;
using WebOmokServer;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};

var omokServer = new OmokServer();

app.UseWebSockets(webSocketOptions);
app.Map("ws/",
    async context =>
    {
        Console.WriteLine("[{0} �����尡 �� ��û�� ����]", Thread.CurrentThread.ManagedThreadId);
        if (context.WebSockets.IsWebSocketRequest)
        {
            using var ws = await context.WebSockets.AcceptWebSocketAsync();
            await omokServer.HandleClientAsync(ws, context.Connection);
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        }
    });

Console.WriteLine("\n\n���� �غ� �Ϸ�\n\n");

app.Run();
