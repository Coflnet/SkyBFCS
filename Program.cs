using System;
using System.Threading.Tasks;
using Coflnet.Sky.BFCS.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using WebSocketSharp.Server;

namespace Coflnet.Sky.BFCS
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var server = new HttpServer(8888);
            server.KeepClean = false;
            server.AddWebSocketService<SniperSocket>("/socket");
            server.AddWebSocketService<SniperSocket>("/modsocket");
            server.OnGet += (arg, e) =>
            {
                e.Response.StatusCode = 204;
                if (e.Request.RawUrl.Contains("modsocket?version"))
                {
                    Console.WriteLine("Got request to " + e.Request.RawUrl);
                    var redirectUrl = "ws://sky-us.coflnet.com/modsocket";
                    e.Response.Headers.Add("Location", redirectUrl);
                    e.Response.StatusCode = 302;
                }
                return Task.CompletedTask;
            };
            server.Log.Level = WebSocketSharp.LogLevel.Debug;
            server.Log.Output = (data, s) => System.Console.WriteLine("ws:" + data);
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
