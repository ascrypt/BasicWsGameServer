using System;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace BasicWsGameServer.Server
{
    public class Program
    {
        public const int TIMESTAMP_INTERVAL_SEC = 15;
        public const int BROADCAST_TRANSMIT_INTERVAL_MS = 250;
        public const int CLOSE_SOCKET_TIMEOUT_MS = 2500;

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateBootstrapLogger();
            var builder = WebApplication.CreateBuilder();
            builder.Host.UseSerilog();
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls(new string[] { @"http://localhost:8080/" });
                    webBuilder.UseStartup<Startup>();
                })
                .Build()
                .Run();
        }

        public static void ReportException(Exception ex, [CallerMemberName]string location = "(Caller name not set)")
        {
          Log.Error("\\n{Location}:\\n  Exception {Name}: {ExMessage}", location, ex.GetType().Name, ex.Message);
            if (ex.InnerException != null) Log.Error("  Inner Exception {Name}: {InnerExceptionMessage}", ex.InnerException.GetType().Name, ex.InnerException.Message);
        }
    }
}