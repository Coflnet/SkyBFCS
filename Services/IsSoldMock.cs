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
    (Guid itemId, long Price) lastPrice = (Guid.Empty, 0);

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
        var last = lastPrice;
        lastPrice = (Guid.Empty, 0);
        sniperSocket.SendToServer(Response.Create(typeof(FlipSentOnProxy).Name, new FlipSentOnProxy.Data()
        {
            AuctionId = auctionId,
            PlayerId = playerId,
            Time = when,
            Value = last.Price,
            ItemId = last.itemId
        }));
        return Task.CompletedTask;
    }

    public Task SetPrice(Guid playerUuid, Guid uuid, long value)
    {
        lastPrice = (uuid, value);
        return Task.CompletedTask;
    }
}