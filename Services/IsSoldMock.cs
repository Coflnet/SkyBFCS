using Coflnet.Sky.Updater.Models;
using Coflnet.Sky.ModCommands.Services;
using Coflnet.Sky.Commands;
using System.Threading.Tasks;
using System;

namespace Coflnet.Sky.BFCS.Services;
public class IsSoldMock : IIsSold
{
    public bool IsSold(string uuid)
    {
        return false;
    }
}

public class FlipReceiveTrackerMock : IFlipReceiveTracker
{
    public Task ReceiveFlip(string auctionId, string playerId, DateTime when = default)
    {
        return Task.CompletedTask;
    }
}