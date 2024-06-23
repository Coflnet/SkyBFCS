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
using Coflnet.Sky.Commands;
using Microsoft.Extensions.DependencyInjection;
using Coflnet.Sky.ModCommands.Services;
using Microsoft.Extensions.Configuration;
using Coflnet.Sky.ModCommands.Dialogs;
using System.Diagnostics;

namespace Coflnet.Sky.BFCS.Services;
public class SniperSocket : MinecraftSocket
{
    private WebSocket clientSocket;
    public override string CurrentRegion => "us";
    private static readonly ClassNameDictonary<McCommand> TryLocalFirst;
    private static readonly ClassNameDictonary<McCommand> ExecuteBoth;
    private bool WindingDown = false;
    private ConcurrentQueue<string> flipUuidsSent = new ConcurrentQueue<string>();
    // private static ConcurrentDictionary<Type, Func<SniperSocket, object>> serviceConstructors = new ConcurrentDictionary<Type, Func<SniperSocket, object>>();
    private ServiceProvider services;
    static SniperSocket()
    {
        TryLocalFirst = new ClassNameDictonary<McCommand>();
        TryLocalFirst.Add<DialogCommand>();
        TryLocalFirst.Add<TimeCommand>();
        TryLocalFirst.Add<PingCommand>();

        ExecuteBoth = new ClassNameDictonary<McCommand>();
        ExecuteBoth.Add<UploadInventory>();
        ExecuteBoth.Add<FlipCommand>();
        ExecuteBoth.Add<UploadScoreboardCommand>();
        ExecuteBoth.Add<DebugSCommand>();
    }
    public SniperSocket()
    {
        ConSpan = this.CreateActivity("SniperSocket");
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton<IMinecraftSocket>(this);
        services.AddSingleton(this);
        services.AddSingleton<IFlipReceiveTracker>(new FlipReceiveTrackerClient(this));
        services.AddSingleton<IPriceStorageService>(di => di.GetService<IFlipReceiveTracker>() as IPriceStorageService ?? throw new Exception("No IPriceStorageService"));
        // all services:
        Console.WriteLine("added services");
        foreach (var service in services)
        {
            Console.WriteLine(service.ServiceType);
        }
        this.services = services.BuildServiceProvider();
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
        clientSocket = new WebSocket(GetService<IConfiguration>()["SOCKET_BASE_URL"] + "/modsocket?" + QueryString + "&type=us-proxy&ip=" + ClientIp);
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
            if (ReadyState == WebSocketState.Open && !WindingDown)
            {
                ConnectClient();
                var accountId = sessionLifesycle.AccountInfo?.Value?.UserId;
                Console.WriteLine("reconnecting " + accountId);
            }
            else
                Console.WriteLine("closing because " + e.Reason);
        };
        clientSocket.Connect();
    }

    private async Task HandleServerCommand(MessageEventArgs ev)
    {
        using var activity = CreateActivity("ServerCommand", ConSpan);
        if (ReadyState == WebSocketState.Closed)
        {
            clientSocket.Close();
            return;
        }
        var deserialized = JsonConvert.DeserializeObject<Response>(ev.Data);
        activity.AddTag("type", deserialized.type);
        activity.Log(deserialized.data);
        switch (deserialized.type)
        {
            case "proxySync":
                await HandleProxySettingsSync(deserialized);
                break;
            case "filterData":
                await UpdateFilterData(deserialized);
                break;
            case "loggedIn":
                var command = Response.Create("ProxyReqSync", 0);
                SendToServer(command);
                SendMessage("Special test sniper connected to main instance, requesting account info");
                break;
            case "flip":
                // forward
                if (ReadyState == WebSocketState.Closed)
                    Close();
                var snipe = JsonConvert.DeserializeObject<ForwardedFlip>(deserialized.data);
                if (IsReceived(snipe.Id))
                {
                    TopBlocked.Enqueue(new BlockedElement()
                    {
                        Flip = new LowPricedAuction()
                        {
                            Auction = new SaveAuction()
                            {
                                Uuid = snipe.Id
                            }
                        },
                        Reason = "Already sent"
                    });
                    activity.Log("Already sent");
                    return; // already sent
                }
                Send(ev.Data);
                activity.Log("Sent flip");
                break;
            case "ping":
            case "countdown":
                Send(ev.Data);
                if (this.sessionLifesycle != null)
                {
                    this.sessionLifesycle.HouseKeeping();
                }
                break;
            case "stop":
                WindingDown = true;
                clientSocket.Close();
                Close();
                break;
            default:
                // forward
                Send(ev.Data);
                break;
        }
        await Task.Delay(0);
    }

    public void SendToServer(Response command)
    {
        clientSocket.Send(JsonConvert.SerializeObject(command));
    }

    private async Task UpdateFilterData(Response deserialized)
    {
        var state = JsonConvert.DeserializeObject<FilterStateService.FilterState>(deserialized.data);
        await GetService<FilterStateService>().UpdateState(state);
    }

    public override T GetService<T>()
    {
        var localService = services.GetService<T>();
        if (localService != null)
        {
            return localService;
        }
        return base.GetService<T>();
    }

    private Task HandleProxySettingsSync(Response deserialized)
    {
        var data = JsonConvert.DeserializeObject<ProxyReqSyncCommand.Format>(deserialized.data);
        if (data.SessionInfo.SessionTier < AccountTier.PREMIUM_PLUS)
        {
            Dialog(db => db.Break.MsgLine("Sorry, your account does not have premium plus, redirecting back", null, "Prem+ is required for this service")
                .CoflCommand<PurchaseCommand>($"{McColorCodes.GREEN}Click here to purchase Prem+", "prem+", "Start purchasing Prem+"));
            ExecuteCommand("/cofl start");
            Close();
            return Task.CompletedTask;
        }
        this.SessionInfo = SelfUpdatingValue<SessionInfo>.CreateNoUpdate(data.SessionInfo);
        if (this.sessionLifesycle == null)
        {
            sessionLifesycle = new ModSessionLifesycle(this)
            {
                UserId = SelfUpdatingValue<string>.CreateNoUpdate(data.AccountInfo.UserId)
            };
            SendMessage("received account info, ready to speed up flips");
            GetService<SniperService>().FoundSnipe += SendSnipe;
            GetService<BfcsBackgroundService>().FoundSnipe += SendSnipe;
            if (data.Settings.AllowedFinders.HasFlag(LowPricedAuction.FinderType.USER))
                GetService<SnipeUpdater>().NewAuction += UserFlip;

            if (data.SessionInfo.McName == "Ekwav")
                foreach (var item in Headers)
                {
                    Dialog(db => db.Msg($"{item}:{Headers[item.ToString()]}"));
                }
        }
        var settings = data.Settings;
        if (settings.Visibility.Seller)
        {
            Dialog(db => db.Break.MsgLine("You had seller name in your flip settings enabled, this does not work on the us-instance because would slow down flips", null, "Seller visibility is not allowed")
                .CoflCommand<SetCommand>($"{McColorCodes.GREEN}Click here to disable that setting", "showseller false", "Disable to speed up flips"));
            settings.Visibility.Seller = false;
        }
        var previousSettings = sessionLifesycle.FlipSettings?.Value;
        Activity.Current.Log($"Fixing filters");
        FixFilter(settings.BlackList);
        FixFilter(settings.WhiteList);
        var testFlip = BlacklistCommand.GetTestFlip("test");
        try
        {
            Activity.Current.Log($"Copying filter");
            settings.CopyListMatchers(sessionLifesycle.FlipSettings);
            Activity.Current.Log($"Copied matchers");
            settings.MatchesSettings(testFlip);
            Activity.Current.Log($"Ran test flip");
        }
        catch (System.Exception)
        {
            sessionLifesycle.CheckListValidity(testFlip, settings.BlackList);
            sessionLifesycle.CheckListValidity(testFlip, settings.WhiteList, true);
            return Task.CompletedTask;
        }
        this.sessionLifesycle.FlipSettings = SelfUpdatingValue<FlipSettings>.CreateNoUpdate(settings);
        this.sessionLifesycle.AccountInfo = SelfUpdatingValue<AccountInfo>.CreateNoUpdate(data.AccountInfo);
        previousSettings?.CancelCompilation();
        var dl = sessionLifesycle.DelayHandler as StaticDelayHandler;
        if (dl == null)
        {
            sessionLifesycle.DelayHandler = new StaticDelayHandler(TimeSpan.FromMilliseconds(data.ApproxDelay), this.SessionInfo, this.ClientIp);
            var extended = new ExtendedSpamController(f => IsReceived(f.Uuid));
            sessionLifesycle.FlipProcessor = new FlipProcesser(this, extended, sessionLifesycle.DelayHandler);
        }
        else
            dl.CurrentDelay = TimeSpan.FromMilliseconds(data.ApproxDelay);
        Activity.Current.Log($"Set delay");
        if (sessionLifesycle.TierManager == null)
            sessionLifesycle.TierManager = new StaticTierManager(data);
        else
            (sessionLifesycle.TierManager as StaticTierManager)?.Update(data);
        return Task.CompletedTask;
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
        var filterEngine = DiHandler.GetService<Filter.FilterEngine>();
        var Lookup = filterEngine.AvailableFilters.ToLookup(a => a.Name);
        foreach (var elem in list)
        {
            var dict = elem.filter;
            if (dict == null)
                continue;
            // uppercase each keys first letter
            foreach (var item in dict.Keys.ToList())
            {
                if (Lookup.Contains(item))
                {
                    continue;
                }
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
            if (snipe.Auction.Context.TryGetValue("cname", out string name) && !name.EndsWith("-us"))
            {
                snipe.Auction.Context["cname"] = name + McColorCodes.GRAY + "-us";
            }
            if (await SendFlip(snipe))
                Console.WriteLine("sending failed :( " + snipe.Auction.Uuid);
            else if (snipe.TargetPrice - snipe.Auction.StartingBid > 10_000_000)
                Console.WriteLine($"sent {snipe.Auction.Uuid} to {SessionInfo?.McUuid}");
        }, "sending flip", 1);
    }

    public override string Error(Exception exception, string message = null, string additionalLog = null)
    {
        clientSocket.Send(JsonConvert.SerializeObject(Response.Create("clienterror", JsonConvert.SerializeObject(new
        {
            message,
            exception = exception?.ToString(),
            additionalLog
        }).Truncate(10000))));
        return base.Error(exception, message, additionalLog);
    }

    private bool IsReceived(string uuid)
    {
        if (uuid == null)
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
        WindingDown = true;
        var sniper = DiHandler.GetService<SniperService>();
        sniper.FoundSnipe -= SendSnipe;
        GetService<BfcsBackgroundService>().FoundSnipe -= SendSnipe;
        GetService<SnipeUpdater>().NewAuction -= UserFlip;
        clientSocket.Close();
        services.Dispose();
        Console.WriteLine("error " + e.Reason);
        base.OnClose(e);
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        TryAsyncTimes(async () =>
        {
            await HandleCommand(e);
        }, "handling command", 1);
    }

    private bool SuppressNextDialog = false;
    public override void Dialog(Func<SocketDialogBuilder, DialogBuilder> creation)
    {
        if (SuppressNextDialog)
        {
            SuppressNextDialog = false;
            return;
        }
        base.Dialog(creation);
    }

    private async Task HandleCommand(MessageEventArgs e)
    {
        using var activity = CreateActivity("ClientCommand", ConSpan);
        var deserialized = JsonConvert.DeserializeObject<Response>(e.Data);
        if (TryLocalFirst.ContainsKey(deserialized.type.ToLower()))
        {
            try
            {
                await Commands[deserialized.type.ToLower()].Execute(this, deserialized.data);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                // fall back to sending to server
            }
        }
        if (ExecuteBoth.ContainsKey(deserialized.type.ToLower()))
        {
            try
            {
                SuppressNextDialog = true;
                await Commands[deserialized.type.ToLower()].Execute(this, deserialized.data);
                SuppressNextDialog = false;
            }
            catch (Exception ex)
            {
                Error(ex, "Error executing command", deserialized.data);
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
                var traceId = activity?.Context.TraceId;
                clientSocket.Send(JsonConvert.SerializeObject(Response.Create("clienterror", $"local report id {traceId}")));
                await Task.Delay(1000);
                clientSocket.Send(JsonConvert.SerializeObject(Response.Create("clienterror", $"local report id {traceId}")));
                Dialog(db => db.MsgLine($"Us error id: {traceId}"));
                break;
            default:
                clientSocket.Send(e.Data);
                break;
        }
    }
}

public class ExtendedSpamController : SpamController
{
    private Func<SaveAuction, bool> shouldBlock;
    public ExtendedSpamController(Func<SaveAuction, bool> shouldBlock)
    {
        this.shouldBlock = shouldBlock;
    }

    public override bool ShouldBeSent(FlipInstance flip)
    {
        if (shouldBlock(flip.Auction))
        {
            return false;
        }
        return base.ShouldBeSent(flip);
    }
}