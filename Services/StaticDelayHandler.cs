using System;
using Coflnet.Sky.Commands.MC;
using Coflnet.Sky.Commands.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Coflnet.Sky.BFCS.Services;

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
        // simple calculation
        if (IsLikelyBot(flipInstance))
            return DateTime.UtcNow;
        if (CurrentDelay > TimeSpan.Zero)
            await Task.Delay(CurrentDelay / 2);
        return DateTime.UtcNow;
    }

    public bool IsLikelyBot(FlipInstance flipInstance)
    {
        return flipInstance.ProfitPercentage > 500 || flipInstance.Profit > 100_000_000 / Math.Min(flipInstance.Volume, 10);
    }

    public Task<DelayHandler.Summary> Update(IEnumerable<string> ids, DateTime lastCaptchaSolveTime)
    {
        // nothing todo, gets set by the socket
        return Task.FromResult(new DelayHandler.Summary() { VerifiedMc = true, Penalty = CurrentDelay });
    }
}