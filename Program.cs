using System.Net;
using System.Text;
using System.Net.WebSockets;
using System.Text.Json;
using System.Security.Cryptography;
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
        if (context.WebSockets.IsWebSocketRequest)
        {
            using var ws = await context.WebSockets.AcceptWebSocketAsync();
            omokServer.HandleClient(ws);
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        }
    });

app.Run();
