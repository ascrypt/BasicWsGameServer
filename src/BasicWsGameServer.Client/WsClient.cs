using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Serilog;

namespace BasicWsGameServer.Client
{
    public static class WsClient
    {
        private static ClientWebSocket _socket;
        private static BlockingCollection<string> _keystrokeQueue = new BlockingCollection<string>();
        private static CancellationTokenSource _socketLoopTokenSource;
        private static CancellationTokenSource _keystrokeLoopTokenSource;

        public static async Task StartAsync(string wsUri)
            => await StartAsync(new Uri(wsUri));

        public static async Task StartAsync(Uri wsUri)
        {
            Log.Information("Connecting to server {S}", wsUri.ToString());

            _socketLoopTokenSource = new CancellationTokenSource();
            _keystrokeLoopTokenSource = new CancellationTokenSource();

            try
            {
                _socket = new ClientWebSocket();
                await _socket.ConnectAsync(wsUri, CancellationToken.None);
                _ = Task.Run(() => SocketProcessingLoopAsync().ConfigureAwait(false));
                _ = Task.Run(() => KeystrokeTransmitLoopAsync().ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
            }
        }

        public static async Task StopAsync()
        {
            Log.Information($"\nClosing connection");
            _keystrokeLoopTokenSource.Cancel();
            if (_socket == null || _socket.State != WebSocketState.Open) return;
            var timeout = new CancellationTokenSource(Program.CloseSocketTimeoutMs);
            try
            {
                await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", timeout.Token);
                while (_socket.State != WebSocketState.Closed && !timeout.Token.IsCancellationRequested) ;
            }
            catch (OperationCanceledException)
            {
            }
            _socketLoopTokenSource.Cancel();
        }

        public static WebSocketState State => _socket?.State ?? WebSocketState.None;

        public static void QueueKeystroke(string message)
            => _keystrokeQueue.Add(message);

        private static async Task SocketProcessingLoopAsync()
        {
            var cancellationToken = _socketLoopTokenSource.Token;
            try
            {
                var buffer = WebSocket.CreateClientBuffer(4096, 4096);
                while (_socket.State != WebSocketState.Closed && !cancellationToken.IsCancellationRequested)
                {
                    var receiveResult = await _socket.ReceiveAsync(buffer, cancellationToken);
                    if (cancellationToken.IsCancellationRequested) continue;
                    if (_socket.State == WebSocketState.CloseReceived && receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        Log.Information($"\nAcknowledging Close frame received from server");
                        _keystrokeLoopTokenSource.Cancel();
                        await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledge Close frame", CancellationToken.None);
                    }

                    // display text or binary data
                    if (_socket.State != WebSocketState.Open ||
                        receiveResult.MessageType == WebSocketMessageType.Close) continue;
                    var message = Encoding.UTF8.GetString(buffer.Array, 0, receiveResult.Count);
                    if (message.Length > 1) message = "\n" + message + "\n";
                    Log.Information(message);
                }
                Log.Information("Ending processing loop in state {SocketState}", _socket.State);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Program.ReportException(ex);
            }
            finally
            {
                _keystrokeLoopTokenSource.Cancel();
                _socket.Dispose();
                _socket = null;
            }
        }

        private static async Task KeystrokeTransmitLoopAsync()
        {
            var cancellationToken = _keystrokeLoopTokenSource.Token;
            while(!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(Program.KeystrokeTransmitIntervalMs, cancellationToken);
                    if(!cancellationToken.IsCancellationRequested && _keystrokeQueue.TryTake(out var message))
                    {
                        var msgBuff = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
                        await _socket.SendAsync(msgBuff, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
                    }
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
