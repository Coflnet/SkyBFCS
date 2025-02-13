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
using System.Threading;

namespace Coflnet.Sky.BFCS.Services;
public class SniperSocket : MinecraftSocket
{
    private WebSocket clientSocket;
    public override string CurrentRegion => "us";
    private static readonly ClassNameDictonary<McCommand> TryLocalFirst;
    private static readonly ClassNameDictonary<McCommand> ExecuteBoth;
    private bool WindingDown = false;
    private ConcurrentQueue<string> flipUuidsSent = new ConcurrentQueue<string>();
    private Timer housekeepingTimer;
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

        ConnectionTester.Start();
    }
    public SniperSocket()
    {
        ConSpan = CreateActivity("SniperSocket");
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton<IMinecraftSocket>(this);
        services.AddSingleton(this);
        services.AddSingleton<IFlipReceiveTracker>(new FlipReceiveTrackerClient(this));
        services.AddSingleton<IBlockedService>(new StaticBlockedService(this));
        services.AddSingleton<IPriceStorageService>(di => di.GetService<IFlipReceiveTracker>() as IPriceStorageService ?? throw new Exception("No IPriceStorageService"));
        this.services = services.BuildServiceProvider();
    }

    protected override void OnOpen()
    {
        ConnectClient();
        TryAsyncTimes(() =>
        {
            SetupModAdapter();
            housekeepingTimer = new Timer(async _ =>
            {
                await HouseKeeping();
            }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            return Task.CompletedTask;
        }, "mod con setup", 1);
    }

    private void ConnectClient()
    {
        using var span = CreateActivity("connecting", ConSpan);
        var args = QueryString;
        var x = System.Web.HttpUtility.ParseQueryString("");
        Console.WriteLine(QueryString.ToString());
        try
        {
            clientSocket?.Close();
        }
        catch (Exception e)
        {
            Console.WriteLine("client close error " + e);
        }
        clientSocket = new WebSocket(GetService<IConfiguration>()["SOCKET_BASE_URL"] + "/modsocket?" + QueryString + "&type=us-proxy&ip=" + ClientIp);
        span.Log("Connecting to " + clientSocket.Url);
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
        if (ReadyState == WebSocketState.Closed)
        {
            clientSocket.Close();
            return;
        }
        var deserialized = JsonConvert.DeserializeObject<Response>(ev.Data);
        using var activity = SessionInfo.IsDebug ? CreateActivity("ServerCommand", ConSpan) : null;
        activity?.AddTag("type", deserialized.type);
        activity.Log(deserialized.data);
        switch (deserialized.type)
        {
            case "proxySync":
                await HandleProxySettingsSync(deserialized);
                break;
            case "filterData":
                await UpdateFilterData(deserialized);
                break;
            case "exemptKeys":
                await UpdateExemptKeys(deserialized);
                break;
            case "loggedIn":
                var command = Response.Create("ProxyReqSync", ConSpan?.Context.TraceId.ToString());
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
                    return; // duplicates
                }
                Send(ev.Data);
                activity.Log("Sent flip");
                break;
            case "ping":
            case "countdown":
                Send(ev.Data);
                await HouseKeeping();
                break;
            case "stop":
                WindingDown = true;
                clientSocket.Close();
                Close();
                break;
            case "chatMessage":
                if (!ev.Data.Contains("WhichBLEntry", StringComparison.OrdinalIgnoreCase) || !LastSent.Any(l => ev.Data.Contains(l.Auction.Uuid)))
                    Send(ev.Data);
                break;
            default:
                // forward
                Send(ev.Data);
                break;
        }
        await Task.Delay(0);
    }

    private Task HouseKeeping()
    {
        if (sessionLifesycle == null)
            return Task.CompletedTask;
        _ = TryAsyncTimes(async () =>
        {
            sessionLifesycle.HouseKeeping();
            await sessionLifesycle.DelayHandler.Update(SessionInfo.MinecraftUuids, SessionInfo.LastCaptchaSolve);
            await Task.Delay(12000);
            await Task.Delay(Random.Shared.Next(0, 3000));
            using var activity = CreateActivity("HouseKeeping", ConSpan);
            var service = GetService<IBlockedService>();
            await service.ArchiveBlockedFlipsUntil(TopBlocked, UserId, 0);
            activity.Log($"Delay is {sessionLifesycle.DelayHandler.CurrentDelay.TotalSeconds}");
            activity?.AddTag("uuid", SessionInfo.McUuid);
        }, "housekeeping", 1);
        return Task.CompletedTask;
    }

    private Task UpdateExemptKeys(Response deserialized)
    {
        GetService<IDelayExemptList>().Exemptions = JsonConvert.DeserializeObject<HashSet<(string, string)>>(deserialized.data);
        return Task.CompletedTask;
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
        var connectionId = SessionInfo.ConnectionId;
        if (data.SessionInfo.McName == "test")
        {
            SessionInfo.ConnectionId = "test";
            SessionInfo.McName = "test";
            SessionInfo.McUuid = "test";
            SessionInfo.SessionId = "test" + Random.Shared.Next();
            return Task.CompletedTask;
        }
        else if (data.SessionInfo.SessionTier < AccountTier.PREMIUM_PLUS)
        {
            Dialog(db => db.Break.MsgLine($"Sorry, your account does not have {McColorCodes.GOLD}premium plus{McColorCodes.RESET}, redirecting back", null, "Prem+ is required for this service")
                .CoflCommand<PurchaseCommand>($"{McColorCodes.GOLD}Click here to purchase Prem+", "prem+", "Start purchasing Prem+"));
            ExecuteCommand("/cofl start");
            Close();
            return Task.CompletedTask;
        }
        try
        {
            CopySessionInfoInfoContent(data);
        }
        catch (Exception e)
        {
            Error(e, "Error copying session info", JsonConvert.SerializeObject(data.SessionInfo));
        }

        if (sessionLifesycle == null)
        {
            sessionLifesycle = new ModSessionLifesycle(this)
            {
                UserId = SelfUpdatingValue<string>.CreateNoUpdate(data.AccountInfo.UserId)
            };
            GetService<SniperService>().FoundSnipe += SendSnipe;
            GetService<BfcsBackgroundService>().FoundSnipe += SendSnipe;
            if (data.Settings.AllowedFinders.HasFlag(LowPricedAuction.FinderType.USER))
                GetService<SnipeUpdater>().NewAuction += UserFlip;
        }
        if (connectionId != SessionInfo.ConnectionId)
        {
            SendMessage("received account info, ready to speed up flips");
        }

        sessionLifesycle.AccountInfo = SelfUpdatingValue<AccountInfo>.CreateNoUpdate(data.AccountInfo);
        var dl = sessionLifesycle.DelayHandler as StaticDelayHandler;
        if (dl == null)
        {
            sessionLifesycle.DelayHandler = new StaticDelayHandler(TimeSpan.FromMilliseconds(data.ApproxDelay), SessionInfo, ClientIp);
            var extended = new ExtendedSpamController(f => IsReceived(f.Uuid));
            sessionLifesycle.FlipProcessor = new FlipProcesser(this, extended, sessionLifesycle.DelayHandler);
        }
        else
        {
            dl.CurrentDelay = TimeSpan.FromMilliseconds(data.ApproxDelay);
            sessionLifesycle.FlipProcessor.MinuteCleanup();
        }
        Activity.Current.Log($"Set delay");
        if (sessionLifesycle.TierManager == null)
            sessionLifesycle.TierManager = new StaticTierManager(data);
        else
            (sessionLifesycle.TierManager as StaticTierManager)?.Update(data);

        UpdateSettings(data);
        return Task.CompletedTask;
    }

    private void CopySessionInfoInfoContent(ProxyReqSyncCommand.Format data)
    {
        var properties = typeof(SessionInfo).GetProperties();
        foreach (var prop in properties)
        {
            var value = prop.GetValue(data.SessionInfo);
            if (value != null && prop.CanWrite)
            {
                prop.SetValue(SessionInfo, value);
            }
        }
        var fields = typeof(SessionInfo).GetFields();
        foreach (var field in fields)
        {
            var value = field.GetValue(data.SessionInfo);
            if (value != null)
            {
                field.SetValue(SessionInfo, value);
            }
        }
    }

    private void UpdateSettings(ProxyReqSyncCommand.Format data)
    {
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
        var changed = previousSettings == null || JsonConvert.SerializeObject(settings) != JsonConvert.SerializeObject(previousSettings);
        if (!changed)
        {
            Activity.Current.Log($"Settings not changed");
            return;
        }
        Activity.Current.Log($"Settings changed {JsonConvert.SerializeObject(settings.WhiteList).Truncate(100)} {JsonConvert.SerializeObject(previousSettings?.WhiteList).Truncate(100)}");
        var testFlip = BlacklistCommand.GetTestFlip("test");
        try
        {
            settings.PlayerInfo = SessionInfo;
            previousSettings?.CancelCompilation();
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
            return;
        }
        sessionLifesycle.FlipSettings = SelfUpdatingValue<FlipSettings>.CreateNoUpdate(settings);
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
        var Lookup = FlipFilter.AllFilters.ToLookup(a => a);
        var caseInvariantLookup = FlipFilter.AllFilters.ToLookup(a => a.ToLower());
        foreach (var elem in list)
        {
            var dict = elem.filter;
            if (dict == null)
                continue;
            // uppercase each keys first letter
            foreach (var item in dict.Keys.ToList())
            {
                if (Lookup.Contains(item) || item.EndsWith("Rune")) // rune options are not available
                {
                    continue;
                }
                var newKey = item[0].ToString().ToUpper() + item.Substring(1);
                if (newKey != item)
                {
                    dict.Add(newKey, dict[item]);
                    dict.Remove(item);
                }
                else
                {
                    Console.WriteLine($"Could not correct filter {item}");
                }
            }
        }
    }

    private void SendSnipe(LowPricedAuction lp)
    {
        if (lp.TargetPrice - lp.Auction.StartingBid < 250_000)
            return; // not interesting
        if (!lp.Auction.Context.TryGetValue("csh", out string name))
        {
            lp.Auction.Context["csh"] = (DateTime.UtcNow - lp.Auction.FindTime).ToString();
        }
        var snipe = new LowPricedAuction()
        {
            AdditionalProps = lp.AdditionalProps == null ? [] : new Dictionary<string, string>(lp.AdditionalProps),
            Auction = lp.Auction,
            DailyVolume = lp.DailyVolume,
            Finder = lp.Finder,
            TargetPrice = lp.TargetPrice
        };
        TryAsyncTimes(async () =>
        {
            if (snipe?.Auction?.Context == null || Settings == null || snipe.TargetPrice - snipe.Auction.StartingBid < Settings.MinProfit)
                return;
            if (snipe.Auction.Context.TryGetValue("cname", out string name) && !name.Contains("-us"))
            {
                snipe.Auction.Context["cname"] = name + McColorCodes.DARK_GRAY + "-us";
            }
            snipe.AdditionalProps["da"] = (DateTime.UtcNow - snipe.Auction.FindTime).ToString();
            if (!await SendFlip(snipe))
                Console.WriteLine($"sending failed :( {snipe.Auction.Uuid} on {ConSpan?.Context.TraceId}");
            else if (snipe.TargetPrice - snipe.Auction.StartingBid > 10_000_000)
            {
                var blockedReson = TopBlocked.Where(b => b.Flip.Auction.Uuid == snipe.Auction.Uuid && b.Flip.Finder == snipe.Finder && b.Flip.TargetPrice == snipe.TargetPrice).FirstOrDefault();
                Console.WriteLine($"sent {snipe.Auction.Uuid} to {SessionInfo?.McUuid} on {ConSpan?.Context.TraceId}, blocked {blockedReson?.Reason}");
            }
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
        if (SessionInfo.IsDebug)
        {
            return false;
        }
        if (flipUuidsSent.Contains(uuid))
        {
            Activity.Current.Log("Already sent us");
            return true;
        }
        flipUuidsSent.Enqueue(uuid);
        while (flipUuidsSent.Count > 15)
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
        Console.WriteLine("close error " + e?.Reason);
        base.OnClose(e);
        services.Dispose();
        // disable timer
        housekeepingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        housekeepingTimer?.Dispose();
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
        using var activity = SessionInfo.IsDebug ? CreateActivity("ClientCommand", ConSpan) : null;
        activity.Log(e.Data.Truncate(50));
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
                var traceId = this.ConSpan?.Context.TraceId;
                clientSocket.Send(JsonConvert.SerializeObject(Response.Create("clienterror", $"local report id {traceId}")));
                await Task.Delay(1000);
                clientSocket.Send(JsonConvert.SerializeObject(Response.Create("clienterror", $"local report id {traceId}")));
                Dialog(db => db.MsgLine($"Us error id: {traceId}", "http://" + traceId));
                break;
            default:
                clientSocket.Send(e.Data);
                break;
        }
    }
}
