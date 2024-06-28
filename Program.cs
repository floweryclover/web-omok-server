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
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        var ws = await context.WebSockets.AcceptWebSocketAsync();
        var client = new Client(ws, context.Connection);
        await omokServer.HandleClientLoopAsync(client);
    });

app.Run();
