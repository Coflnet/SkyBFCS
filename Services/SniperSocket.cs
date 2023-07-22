using System;
using WebSocketSharp;
using Coflnet.Sky.Commands.MC;
using Newtonsoft.Json;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Sniper.Services;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.BFCS.Services
{
    public class SniperSocket : MinecraftSocket
    {
        private WebSocket clientSocket;
        public SniperSocket()
        {
        }

        protected override void OnOpen()
        {
            ConnectClient();
        }

        private void ConnectClient()
        {
            var args = System.Web.HttpUtility.ParseQueryString(Context.RequestUri.Query);
            Console.WriteLine(Context.RequestUri.Query);
            clientSocket = new WebSocket("wss://sky.coflnet.com/modsocket" + Context.RequestUri.Query);
            clientSocket.OnMessage += (s, ev) =>
            {
                HandleServerCommand(ev);
            };
            clientSocket.OnOpen += (s, ev) =>
            {
                SendMessage("Welcome to Äkwav special test sniper, connecting to main instance");
            };
            clientSocket.OnError += (s, e) =>
            {
                Console.WriteLine("error " + e.Message);
            };
            clientSocket.OnClose += (s, e) =>
            {
                if (ConnectionState == WebSocketState.Open)
                {
                    ConnectClient();
                    Console.WriteLine("reconnecting ");
                }
                else
                    Console.WriteLine("closing because " + e.Reason);
            };

            clientSocket.Connect();
        }

        private void HandleServerCommand(MessageEventArgs ev)
        {
            var deserialized = JsonConvert.DeserializeObject<Response>(ev.Data);
            switch (deserialized.type)
            {
                case "proxySync":
                    var data = JsonConvert.DeserializeObject<ProxyReqSyncCommand.Format>(deserialized.data);
                    this.SessionInfo = SelfUpdatingValue<SessionInfo>.CreateNoUpdate(data.SessionInfo);
                    if (this.sessionLifesycle == null)
                    {
                        this.sessionLifesycle = new ModSessionLifesycle(this);
                        SendMessage("received account info, ready to speed up flips");
                        DiHandler.GetService<SniperService>().FoundSnipe += SendSnipe;
                    }
                    this.sessionLifesycle.FlipSettings = SelfUpdatingValue<FlipSettings>.CreateNoUpdate(data.Settings);
                    this.sessionLifesycle.AccountInfo = SelfUpdatingValue<AccountInfo>.CreateNoUpdate(data.AccountInfo);
                    break;
                case "loggedIn":
                    var command = Response.Create("ProxyReqSync", 0);
                    clientSocket.Send(JsonConvert.SerializeObject(command));
                    SendMessage("Special test sniper connected to main instance, requesting account info");
                    break;
                default:
                    // forward
                    Send(ev.Data);
                    Console.WriteLine("rec: " + ev.Data);
                    break;
            }
        }

        private void SendSnipe(LowPricedAuction snipe)
        {
            TryAsyncTimes(async () =>
            {
                if (snipe.Auction.Context.ContainsKey("cname"))
                    snipe.Auction.Context["cname"] = "-us";
                await this.SendFlip(snipe);
            }, "sending flip", 1);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Console.WriteLine("error " + e.Reason);
            base.OnClose(e);
            DiHandler.GetService<SniperService>().FoundSnipe -= SendSnipe;
            clientSocket.Close();
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            var deserialized = JsonConvert.DeserializeObject<Response>(e.Data);
            switch (deserialized.type)
            {
                default:
                    clientSocket.Send(e.Data);
                    break;
            }
        }
    }
}
