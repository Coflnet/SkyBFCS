using System.Threading;
using Coflnet.Sky.Base.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using WebSocketSharp.Server;

namespace Coflnet.Sky.Base
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var server = new HttpServer(8888);
            server.KeepClean = false;
            server.AddWebSocketService<SniperSocket>("/socket");
            server.AddWebSocketService<SniperSocket>("/modsocket");
            server.Start();
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
