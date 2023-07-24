using System;
using WebSocketSharp;
using Coflnet.Sky.Commands.MC;
using Newtonsoft.Json;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Sniper.Services;
using Coflnet.Sky.Core;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Coflnet.Sky.BFCS.Services;
public class SniperSocket : MinecraftSocket
{
    private WebSocket clientSocket;
    private static readonly ClassNameDictonary<McCommand> TryLocalFirst;

    static SniperSocket()
    {
        TryLocalFirst = new ClassNameDictonary<McCommand>();
        TryLocalFirst.Add<DialogCommand>();
    }
    public SniperSocket()
    {
    }

    protected override void OnOpen()
    {
        ConnectClient();
        TryAsyncTimes(() =>
        {
            SetupModAdapter();
            return Task.CompletedTask;
        }, "mod con setup", 1);
    }

    private void ConnectClient()
    {
        var args = System.Web.HttpUtility.ParseQueryString(Context.RequestUri.Query);
        Console.WriteLine(Context.RequestUri.Query);
        clientSocket = new WebSocket("wss://sky.coflnet.com/modsocket" + Context.RequestUri.Query);
        clientSocket.OnMessage += (s, ev) =>
        {
            TryAsyncTimes(async () =>
            {
                await HandleServerCommand(ev);
            }, "handling server command", 1);
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

    private async Task HandleServerCommand(MessageEventArgs ev)
    {
        var deserialized = JsonConvert.DeserializeObject<Response>(ev.Data);
        switch (deserialized.type)
        {
            case "proxySync":
                var data = JsonConvert.DeserializeObject<ProxyReqSyncCommand.Format>(deserialized.data);
                Console.WriteLine(deserialized.data);
                if (data.AccountInfo.Tier < AccountTier.PREMIUM_PLUS)
                {
                    SendMessage("Sorry, your account does not have premium plus, redirecting back", null, "Prem+ is required for this service");
                    ExecuteCommand("/cofl start");
                    Close();
                    return;
                }
                this.SessionInfo = SelfUpdatingValue<SessionInfo>.CreateNoUpdate(data.SessionInfo);
                if (this.sessionLifesycle == null)
                {
                    this.sessionLifesycle = new ModSessionLifesycle(this);
                    SendMessage("received account info, ready to speed up flips");
                    DiHandler.GetService<SniperService>().FoundSnipe += SendSnipe;
                }
                FixFilter(data.Settings.BlackList);
                FixFilter(data.Settings.WhiteList);
                data.Settings.MatchesSettings(BlacklistCommand.GetTestFlip("test"));
                this.sessionLifesycle.FlipSettings = SelfUpdatingValue<FlipSettings>.CreateNoUpdate(data.Settings);
                this.sessionLifesycle.AccountInfo = SelfUpdatingValue<AccountInfo>.CreateNoUpdate(data.AccountInfo);
                sessionLifesycle.DelayHandler = new StaticDelayHandler(TimeSpan.FromMilliseconds(data.ApproxDelay));
                break;
            case "loggedIn":
                var command = Response.Create("ProxyReqSync", 0);
                clientSocket.Send(JsonConvert.SerializeObject(command));
                SendMessage("Special test sniper connected to main instance, requesting account info");
                break;
            case "flip":
                // forward
                if (ConnectionState == WebSocketState.Closed)
                    Close();
                Send(ev.Data);
                break;
            default:
                // forward
                Send(ev.Data);
                Console.WriteLine("rec: " + ev.Data);
                if (this.sessionLifesycle != null)
                {
                    this.sessionLifesycle.HouseKeeping();
                }
                break;
        }
    }

    private static void FixFilter(List<ListEntry> list)
    {
        foreach (var elem in list)
        {
            var dict = elem.filter;
            if (dict == null)
                continue;
            // uppercase each keys first letter
            foreach (var item in dict.Keys.ToList())
            {
                var newKey = item[0].ToString().ToUpper() + item.Substring(1);
                if (newKey != item)
                {
                    dict.Add(newKey, dict[item]);
                    dict.Remove(item);
                }
            }
        }
    }

    private void SendSnipe(LowPricedAuction snipe)
    {
        TryAsyncTimes(async () =>
        {
            if (snipe.TargetPrice - snipe.Auction.StartingBid < Settings.MinProfit)
                return;
            if (snipe.Auction.Context.ContainsKey("cname"))
            {
                snipe.Auction.Context["cname"] += "-us";
                Console.WriteLine("\n!!!!!!!!!!!!!!!!!!!!\nsending " + JsonConvert.SerializeObject(snipe));
            }
            if (await this.SendFlip(snipe))
                Console.WriteLine("sending failed :(");
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
        TryAsyncTimes(async () =>
        {
            await this.HandleCommand(e);
        }, "handling command", 1);
    }

    private async Task HandleCommand(MessageEventArgs e)
    {
        var deserialized = JsonConvert.DeserializeObject<Response>(e.Data);
        if (TryLocalFirst.ContainsKey(deserialized.type.ToLower()))
        {
            try
            {
                await Commands[deserialized.type.ToLower()].Execute(this, deserialized.data);
                return;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex);
                // fall back to sending to server
            }
        }
        switch (deserialized.type)
        {
            case "sinfo":
                await Commands["blocked"].Execute(this, deserialized.data);
                break;
            default:
                clientSocket.Send(e.Data);
                break;
        }
    }
}

public class StaticDelayHandler : IDelayHandler
{
    public TimeSpan CurrentDelay { get; set; }

    public event Action<TimeSpan> OnDelayChange;

    public StaticDelayHandler(TimeSpan currentDelay)
    {
        CurrentDelay = currentDelay;
    }

    public async Task<DateTime> AwaitDelayForFlip(FlipInstance flipInstance)
    {
        await Task.Delay(CurrentDelay / 2);
        return DateTime.UtcNow;
    }

    public bool IsLikelyBot(FlipInstance flipInstance)
    {
        return true;
    }

    public Task<DelayHandler.Summary> Update(IEnumerable<string> ids, DateTime lastCaptchaSolveTime)
    {
        // nothing todo, gets set by the socket
        return Task.FromResult(new DelayHandler.Summary());
    }
}