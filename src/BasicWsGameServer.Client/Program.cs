using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using Serilog;

namespace BasicWsGameServer.Client
{
    public class Program
    {
        public const int KeystrokeTransmitIntervalMs = 100;
        public const int CloseSocketTimeoutMs = 10000;

        static async Task Main(string[] args) 
        {
            var running = true;
            while(running)
            {
                Console.Clear();
                await MainThreadUiLoop();
                Log.Information("\nPress R to re-connect or any other key to exit");
                var key = Console.ReadKey(intercept: true);
                running = (key.Key == ConsoleKey.R);
            }
        }

        static async Task MainThreadUiLoop()
        {
            try
            {
                await WsClient.StartAsync(@"ws://localhost:8080/");
                Log.Information("Press ESC to exit. Other keystrokes are sent to the echo server.\n\n");
                var running = true;
                while (running && WsClient.State == WebSocketState.Open)
                {
                    if (!Console.KeyAvailable) continue;
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.Escape)
                    {
                        running = false;
                    }
                    else
                    {
                        WsClient.QueueKeystroke(key.KeyChar.ToString());
                    }
                }
                await WsClient.StopAsync();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ReportException(ex);
            }
        }

        public static void ReportException(Exception ex, [CallerMemberName]string location = "(Caller name not set)")
        {
            Log.Error("\\n{Location}:\\n  Exception {Name}: {ExMessage}", location, ex.GetType().Name, ex.Message);
            if (ex.InnerException != null) Log.Error("  Inner Exception {Name}: {InnerExceptionMessage}", ex.InnerException.GetType().Name, ex.InnerException.Message);
        }
    }
}
