using Coflnet.Sky.ModCommands.Services;
using Coflnet.Sky.Commands;
using System.Threading.Tasks;
using System;
using System.Threading;
using Coflnet.Sky.FlipTracker.Client.Model;
using Coflnet.Sky.Commands.MC;
using System.Collections.Concurrent;

namespace Coflnet.Sky.BFCS.Services;
public class IsSoldMock : IIsSold
{
    public bool IsSold(string uuid)
    {
        return false;
    }
}

public class FlipReceiveTrackerClient : IFlipReceiveTracker, IPriceStorageService
{

    SniperSocket sniperSocket;
    ConcurrentDictionary<Guid, long> prices = new ConcurrentDictionary<Guid, long>();

    public FlipReceiveTrackerClient(SniperSocket sniperSocket)
    {
        this.sniperSocket = sniperSocket;
    }

    public Task<long> GetPrice(Guid playerUuid, Guid uuid)
    {
        throw new Exception("Not implemented on this instance, should not be called in this context");
    }

    public Task ReceiveFlip(string auctionId, string playerId, DateTime when = default)
    {
        prices.TryRemove(Guid.Parse(auctionId), out var price);
        sniperSocket.SendToServer(Response.Create(typeof(FlipSentOnProxy).Name, new FlipSentOnProxy.Data()
        {
            AuctionId = auctionId,
            PlayerId = playerId,
            Time = when,
            Value = price
        }));
        return Task.CompletedTask;
    }

    public Task SetPrice(Guid playerUuid, Guid uuid, long value)
    {
        prices.TryAdd(uuid,  value);
        return Task.CompletedTask;
    }
}