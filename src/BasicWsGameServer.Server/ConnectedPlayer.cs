using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using BasicWsGameServer.Server.Entities;
using Serilog;

namespace BasicWsGameServer.Server
{
    public class ConnectedPlayer
    {
        public ConnectedPlayer(int playerId, WebSocket socket, TaskCompletionSource<object> taskCompletion)
        {
            PlayerId = playerId;
            Socket = socket;
            TaskCompletion = taskCompletion;
            Player = new Player();
        }

        public int PlayerId { get; private set; }
        public Player Player { get; set; }

        public WebSocket Socket { get; private set; }

        public TaskCompletionSource<object> TaskCompletion { get; private set; }

        public BlockingCollection<string> BroadcastQueue { get; } = new BlockingCollection<string>();

        public CancellationTokenSource BroadcastLoopTokenSource { get; set; } = new CancellationTokenSource();

        public async Task BroadcastLoopAsync()
        {
            var cancellationToken = BroadcastLoopTokenSource.Token;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(Program.BROADCAST_TRANSMIT_INTERVAL_MS, cancellationToken);
                    if (cancellationToken.IsCancellationRequested || Socket.State != WebSocketState.Open ||
                        !BroadcastQueue.TryTake(out var message)) continue;
                    Log.Information("Socket {PlayerId}: Sending from queue", PlayerId);
                    var msgBuff = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
                    await Socket.SendAsync(msgBuff, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                    
                }
                catch (Exception ex)
                {
                    Program.ReportException(ex);
                }
            }
        }
    }
}
