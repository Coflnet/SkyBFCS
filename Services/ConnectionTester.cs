using System;
using WebSocketSharp;
using Coflnet.Sky.Commands.MC;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Coflnet.Sky.BFCS.Services;

public class ConnectionTester
{
    public static void Start()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    Console.WriteLine("Staring connection test");
                    await TestWith("ws://localhost:8888/socket");
                    await TestWith("ws://sky-us.coflnet.com/modsocket");
                    await Task.Delay(90000);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    await Task.Delay(100000);
                }
            }
        });
    }

    private static async Task TestWith(string url)
    {
        var client = new WebSocket(url + "?SId=123123123123123123&type=us-proxy&player=test&version=1.5.6-Alpha");
        client.OnOpen += (s, e) =>
        {
            Console.WriteLine("connected");
        };
        client.OnMessage += (s, e) =>
        {
            var response = JsonConvert.DeserializeObject<Response>(e.Data);
            if (response.type == "chatMessage" && response.data.Contains("click this [LINK] to login"))
            {
                Console.WriteLine("starting ping test ");
                client.Send(JsonConvert.SerializeObject(Response.Create("ping", "")));
            }
            if (response.type == "execute")
            {
                var command = JsonConvert.DeserializeObject<string>(response.data);
                var commandType = command.Split(' ')[1];
                var commandData = command[(("/cofl " + commandType).Length + 1)..];
                client.Send(JsonConvert.SerializeObject(Response.Create(commandType, commandData)));
            }
        };
        client.Connect();
        await Task.Delay(10000);
        client.Close();
    }
}
