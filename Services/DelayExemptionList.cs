using Coflnet.Sky.Commands.MC;
using System.Collections.Generic;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.BFCS.Services;

public class DelayExemptionList : IDelayExemptList
{
    public HashSet<(string, string)> Exemptions { get; set; } = [];

    public bool IsExempt(LowPricedAuction flipInstance)
    {
        var exempted = Exemptions.Contains((flipInstance.Auction.Tag, flipInstance.AdditionalProps.GetValueOrDefault("key", "nope")));
        if (exempted)
        {
            System.Console.WriteLine($"Exempted {flipInstance.Auction.Tag} {flipInstance.AdditionalProps.GetValueOrDefault("key", "nope")}");
        }
        return exempted;
    }
}