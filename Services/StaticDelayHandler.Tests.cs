using System;
using Coflnet.Sky.Commands.Shared;
using NUnit.Framework;

namespace Coflnet.Sky.BFCS.Services;

public class StaticDelayHandlerTest
{
    StaticDelayHandler staticDelayHandler = new StaticDelayHandler(TimeSpan.FromSeconds(5), new());

    [TestCase(1_000_000, 11_000_000, 10)]
    [TestCase(1_000_000, 110_000_000, 1)]
    [TestCase(3_000_000, 10_000_000, 1, false)]
    public void SkipDelay(int startingBid, int medianPrice, int volume, bool match = true)
    {
        var flipInstance = new FlipInstance()
        {
            Auction = new Core.SaveAuction()
            {
                StartingBid = startingBid
            },
            MedianPrice = medianPrice,
            Volume = volume
        };

        var result = staticDelayHandler.IsLikelyBot(flipInstance);
        Assert.That(result, Is.EqualTo(match));
    }
}