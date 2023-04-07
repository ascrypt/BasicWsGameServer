using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Serilog;

namespace BasicWsGameServer.Server
{
    public class WsMiddleware : IMiddleware
    {
        private static int _socketCounter = 0;
        
        private static ConcurrentDictionary<int, ConnectedPlayer> _clients = new ConcurrentDictionary<int, ConnectedPlayer>();

        public static CancellationTokenSource SocketLoopTokenSource = new CancellationTokenSource();

        private static bool _serverIsRunning = true;

        private static CancellationTokenRegistration _appShutdownHandler;

        // use dependency injection to grab a reference to the hosting container's lifetime cancellation tokens
        public WsMiddleware(IHostApplicationLifetime hostLifetime)
        {
            // gracefully close all websockets during shutdown (only register on first instantiation)
            if(_appShutdownHandler.Token.Equals(CancellationToken.None))
                _appShutdownHandler = hostLifetime.ApplicationStopping.Register(ApplicationShutdownHandler);
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                if(_serverIsRunning)
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        var playerId = Interlocked.Increment(ref _socketCounter);
                        var socket = await context.WebSockets.AcceptWebSocketAsync();
                        var completion = new TaskCompletionSource<object>();
                        var client = new ConnectedPlayer(playerId, socket, completion);
                        if (_clients.TryAdd(playerId, client))
                        {
                            Log.Information("Player {PlayerId}: New connection", playerId);
                        }
                        else
                        {
                            Log.Warning("Player {PlayerId}: Failed to add already connected player", playerId);
                        }
                        
                        _ = Task.Run(() => SocketProcessingLoopAsync(client).ConfigureAwait(false));
                        await completion.Task;
                    }
                }
                else
                {
                    // ServerIsRunning = false
                    // HTTP 409 Conflict (with server's current state)
                    context.Response.StatusCode = 409;
                }
            }
            catch (Exception ex)
            {
                // HTTP 500 Internal server error
                context.Response.StatusCode = 500;
                Program.ReportException(ex);
            }
            finally
            {
                // if this middleware didn't handle the request, pass it on
                if(!context.Response.HasStarted)
                    await next(context);
            }
        }

        public static void Broadcast(string message)
        {
            Log.Information("Broadcast: {Message}", message);
            foreach (var kvp in _clients)
                kvp.Value.BroadcastQueue.Add(message);
        }

        public static async void ApplicationShutdownHandler()
        {
            _serverIsRunning = false;
            await CloseAllSocketsAsync();
        }

        private static async Task CloseAllSocketsAsync()
        {
            var disposeQueue = new List<WebSocket>(_clients.Count);

            while (!_clients.IsEmpty)
            {
                var client = _clients.ElementAt(0).Value;
                Log.Information("Closing Socket {ClientPlayerId}", client.PlayerId);

                Log.Information("... ending broadcast loop");
                client.BroadcastLoopTokenSource.Cancel();

                if (client.Socket.State != WebSocketState.Open)
                {
                    Log.Error("... socket not open, state = {SocketState}", client.Socket.State);
                }
                else
                {
                    var timeout = new CancellationTokenSource(Program.CLOSE_SOCKET_TIMEOUT_MS);
                    try
                    {
                        Log.Information("... starting close handshake");
                        await client.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", timeout.Token);
                    }
                    catch (OperationCanceledException ex)
                    {
                        Program.ReportException(ex);
                    }
                }

                if (_clients.TryRemove(client.PlayerId, out _))
                {
                    disposeQueue.Add(client.Socket);
                }

                Log.Information("... done");
            }

            SocketLoopTokenSource.Cancel();

            // dispose all resources
            foreach (var socket in disposeQueue)
                socket.Dispose();
        }

        private static async Task SocketProcessingLoopAsync(ConnectedPlayer player)
        {
            _ = Task.Run(() => player.BroadcastLoopAsync().ConfigureAwait(false));

            var socket = player.Socket;
            var loopToken = SocketLoopTokenSource.Token;
            var broadcastTokenSource = player.BroadcastLoopTokenSource; 
            try
            {
                var buffer = WebSocket.CreateServerBuffer(4096);
                while (socket.State != WebSocketState.Closed && socket.State != WebSocketState.Aborted && !loopToken.IsCancellationRequested)
                {
                    var receiveResult = await player.Socket.ReceiveAsync(buffer, loopToken);
                    if (loopToken.IsCancellationRequested) continue;
                    if (player.Socket.State == WebSocketState.CloseReceived && receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        Log.Information("Socket {ClientPlayerId}: Acknowledging Close frame received from player", player.PlayerId);
                        broadcastTokenSource.Cancel();
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledge Close frame", CancellationToken.None);
                    }

                    // echo text or binary data to the broadcast queue
                    if (player.Socket.State != WebSocketState.Open) continue;
                    Log.Information("Socket {ClientPlayerId}: Received {ReceiveResultMessageType} frame ({ReceiveResultCount} bytes)", player.PlayerId, receiveResult.MessageType, receiveResult.Count);
                    Log.Information("Socket {ClientPlayerId}: Echoing data to queue", player.PlayerId);
                    var message = Encoding.UTF8.GetString(buffer.Array, 0, receiveResult.Count);
                    switch (message)
                    {
                        case "updateresource":
                            //update resource logic
                            break;
                        case "sendgifts":
                            //send gifts logic
                            break;
                    }
                    player.BroadcastQueue.Add(message);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log.Warning("Socket {ClientPlayerId}:", player.PlayerId);
                Program.ReportException(ex);
            }
            finally
            {
                broadcastTokenSource.Cancel();

                Log.Information("Socket {ClientPlayerId}: Ended processing loop in state {SocketState}", player.PlayerId, socket.State);

                if (player.Socket.State != WebSocketState.Closed)
                    player.Socket.Abort();

                if (_clients.TryRemove(player.PlayerId, out _))
                    socket.Dispose();

                player.TaskCompletion.SetResult(true);
            }
        }
    }
}
