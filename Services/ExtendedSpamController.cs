using System;
using Coflnet.Sky.Commands.MC;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.BFCS.Services;

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