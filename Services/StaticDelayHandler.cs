using System;
using Coflnet.Sky.Commands.MC;
using Coflnet.Sky.Commands.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Coflnet.Sky.BFCS.Services;

public class StaticDelayHandler : IDelayHandler
{
    public TimeSpan CurrentDelay { get; set; }
    private readonly SessionInfo sessionInfo;

    public event Action<TimeSpan> OnDelayChange;

    public StaticDelayHandler(TimeSpan currentDelay, SessionInfo sessionInfo)
    {
        CurrentDelay = currentDelay;
        this.sessionInfo = sessionInfo;
    }

    public async Task<DateTime> AwaitDelayForFlip(FlipInstance flipInstance)
    {
        // simple calculation
        if(flipInstance.Profit > 100_000_000)
            return DateTime.UtcNow;
        if (IsLikelyBot(flipInstance) && Random.Shared.NextDouble() < 0.5)
            return DateTime.UtcNow;
        if (CurrentDelay > TimeSpan.Zero)
            await Task.Delay(CurrentDelay);
        if (sessionInfo.IsMacroBot && flipInstance.Profit > 1_000_000)
            await Task.Delay(TimeSpan.FromMicroseconds(flipInstance.Profit / 20000 * 1.1)).ConfigureAwait(false);
        return DateTime.UtcNow;
    }

    public bool IsLikelyBot(FlipInstance flipInstance)
    {
        return flipInstance.ProfitPercentage > 500 || flipInstance.Profit > 50_000_000 / Math.Min(flipInstance.Volume, 10);
    }

    public Task<DelayHandler.Summary> Update(IEnumerable<string> ids, DateTime lastCaptchaSolveTime)
    {
        // nothing todo, gets set by the socket
        return Task.FromResult(new DelayHandler.Summary() { VerifiedMc = true, Penalty = CurrentDelay });
    }
}