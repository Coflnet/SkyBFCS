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
using System.Collections.Concurrent;
using Coflnet.Sky.BFCS.Models;

namespace Coflnet.Sky.BFCS.Services;
public class SniperSocket : MinecraftSocket
{
    private WebSocket clientSocket;
    public override string CurrentRegion => "us";
    private static readonly ClassNameDictonary<McCommand> TryLocalFirst;
    private ConcurrentQueue<string> flipUuidsSent = new ConcurrentQueue<string>();

    static SniperSocket()
    {
        TryLocalFirst = new ClassNameDictonary<McCommand>();
        TryLocalFirst.Add<DialogCommand>();
        TryLocalFirst.Add<TimeCommand>();
        TryLocalFirst.Add<PingCommand>();
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
        var args = QueryString;
        var x = System.Web.HttpUtility.ParseQueryString("");
        Console.WriteLine(QueryString.ToString());
        clientSocket = new WebSocket("wss://sky.coflnet.com/modsocket?" + QueryString + "&type=us-proxy");
        clientSocket.OnMessage += (s, ev) =>
        {
            TryAsyncTimes(async () =>
            {
                await HandleServerCommand(ev);
            }, "handling server command", 1);
        };
        clientSocket.OnOpen += (s, ev) =>
        {
            SendMessage("Welcome to Coflnet special test sniper, connecting to main instance");
        };
        clientSocket.OnError += (s, e) =>
        {
            Console.WriteLine("error " + e.Message);
        };
        clientSocket.OnClose += (s, e) =>
        {
            if (ReadyState == WebSocketState.Open)
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
        if (ReadyState == WebSocketState.Closed)
        {
            clientSocket.Close();
            return;
        }
        var deserialized = JsonConvert.DeserializeObject<Response>(ev.Data);
        switch (deserialized.type)
        {
            case "proxySync":
                HandleProxySettingsSync(deserialized);
                break;
            case "filterData":
                UpdateFilterData(deserialized);
                break;
            case "loggedIn":
                var command = Response.Create("ProxyReqSync", 0);
                clientSocket.Send(JsonConvert.SerializeObject(command));
                SendMessage("Special test sniper connected to main instance, requesting account info");
                break;
            case "flip":
                // forward
                if (ReadyState == WebSocketState.Closed)
                    Close();
                var snipe = JsonConvert.DeserializeObject<ForwardedFlip>(deserialized.data);
                if (IsReceived(snipe.Id))
                    return; // already sent
                Send(ev.Data);
                break;
            default:
                // forward
                Send(ev.Data);
                if (this.sessionLifesycle != null)
                {
                    this.sessionLifesycle.HouseKeeping();
                }
                break;
        }
        await Task.Delay(0);
    }

    private void UpdateFilterData(Response deserialized)
    {
        var state = JsonConvert.DeserializeObject<FilterStateService.FilterState>(deserialized.data);
        var local = GetService<FilterStateService>().State;
        local.CurrentMayor = state.CurrentMayor;
        local.NextMayor = state.NextMayor;
        local.PreviousMayor = state.PreviousMayor;
        foreach (var item in state.ExistingTags)
        {
            local.ExistingTags.Add(item);
        }
        foreach (var day in state.IntroductionAge)
        {
            if (local.IntroductionAge.TryAdd(day.Key, day.Value))
                continue;
            foreach (var item in day.Value)
            {
                local.IntroductionAge[day.Key].Add(item);
            }
        }
        foreach (var item in state.itemCategories)
        {
            local.itemCategories.AddOrUpdate(item.Key, item.Value, (k,v)=>v.Union(item.Value).ToHashSet());
        }
        state.LastUpdate = DateTime.UtcNow;
    }

    private void HandleProxySettingsSync(Response deserialized)
    {
        var data = JsonConvert.DeserializeObject<ProxyReqSyncCommand.Format>(deserialized.data);
        Console.WriteLine(deserialized.data);
        if (data.AccountInfo.Tier < AccountTier.PREMIUM_PLUS)
        {
            Dialog(db => db.Break.MsgLine("Sorry, your account does not have premium plus, redirecting back", null, "Prem+ is required for this service")
                .CoflCommand<PurchaseCommand>($"{McColorCodes.GREEN}Click here to purchase Prem+", "prem+", "Start purchasing Prem+"));
            ExecuteCommand("/cofl switchregion eu");
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
            if (data.Settings.AllowedFinders.HasFlag(LowPricedAuction.FinderType.USER))
                DiHandler.GetService<SnipeUpdater>().NewAuction += UserFlip;
        }
        FixFilter(data.Settings.BlackList);
        FixFilter(data.Settings.WhiteList);
        data.Settings.MatchesSettings(BlacklistCommand.GetTestFlip("test"));
        this.sessionLifesycle.FlipSettings = SelfUpdatingValue<FlipSettings>.CreateNoUpdate(data.Settings);
        this.sessionLifesycle.AccountInfo = SelfUpdatingValue<AccountInfo>.CreateNoUpdate(data.AccountInfo);
        sessionLifesycle.DelayHandler = new StaticDelayHandler(TimeSpan.FromMilliseconds(data.ApproxDelay));
    }

    private void UserFlip(SaveAuction obj)
    {
        SendSnipe(new LowPricedAuction()
        {
            AdditionalProps = new(),
            Auction = obj,
            DailyVolume = 0,
            Finder = LowPricedAuction.FinderType.USER,
            TargetPrice = obj.StartingBid
        });
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
            if (snipe?.Auction?.Context == null || Settings == null || snipe.TargetPrice - snipe.Auction.StartingBid < Settings.MinProfit)
                return;
            if (snipe.Auction.Context.ContainsKey("cname") && !snipe.Auction.Context["cname"].EndsWith("-us"))
            {
                snipe.Auction.Context["cname"] += McColorCodes.GRAY + "-us";
            }
            if (IsReceived(snipe.Auction.Uuid))
                return; // already sent
            if (await this.SendFlip(snipe))
                Console.WriteLine("sending failed :(");
        }, "sending flip", 1);
    }

    public override string Error(Exception exception, string message = null, string additionalLog = null)
    {
        clientSocket.Send(JsonConvert.SerializeObject(Response.Create("clienterror", JsonConvert.SerializeObject(new {
            message,
            exception = exception?.ToString(),
            additionalLog
        }).Truncate(10000))));
        return base.Error(exception, message, additionalLog);
    }

    private bool IsReceived(string uuid)
    {
        if(uuid == null)
            return false;
        if (flipUuidsSent.Contains(uuid))
            return true;
        flipUuidsSent.Enqueue(uuid);
        if (flipUuidsSent.Count > 20)
            flipUuidsSent.TryDequeue(out _);
        return false;
    }

    protected override void OnClose(CloseEventArgs e)
    {
        DiHandler.GetService<SniperService>().FoundSnipe -= SendSnipe;
        clientSocket.Close();
        Console.WriteLine("error " + e.Reason);
        base.OnClose(e);
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
            case "report":
                clientSocket.Send(JsonConvert.SerializeObject(Response.Create("clienterror", $"Sent {JsonConvert.SerializeObject(LastSent)}")));
                clientSocket.Send(e.Data);
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