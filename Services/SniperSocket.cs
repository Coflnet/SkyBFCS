using System;
using WebSocketSharp;
using Coflnet.Sky.Commands.MC;

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
            base.OnOpen();
            var args = System.Web.HttpUtility.ParseQueryString(Context.RequestUri.Query);
            Console.WriteLine(Context.RequestUri.Query);
            clientSocket = new WebSocket("wss://sky.coflnet.com/modsocket" + Context.RequestUri.Query);
            clientSocket.OnMessage += (s, ev) =>
            {
                // forward
                Send(ev.Data);
                Console.WriteLine("rec: " + ev.Data);
            };
            clientSocket.OnOpen += (s, ev) =>
            {
                SendMessage("Welcome to Äkwav special test sniper");
            };
            clientSocket.OnError += (s, e) =>
            {
                Console.WriteLine("error " + e.Message);
            };

            clientSocket.Connect();
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Console.WriteLine("error " + e.Reason);
            base.OnClose(e);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            clientSocket.Send(e.Data);
        }
    }
}
