using System;
using Coflnet.Sky.Commands.MC;
using Coflnet.Sky.Commands.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net;
using System.Linq;
using System.Diagnostics;
using System.Threading;

namespace Coflnet.Sky.BFCS.Services;

public class StaticDelayHandler : IDelayHandler
{
    public TimeSpan CurrentDelay { get; set; }
    public TimeSpan MacroDelay => default;
    private readonly SessionInfo sessionInfo;
    private HashSet<string> highCompetitionKeys = [" Any [exp, 6] EPIC 1", " Any [exp, 6] LEGENDARY 1", " Any [candyUsed, 0],[exp, 0] LEGENDARY 1"];

    public event Action<TimeSpan> OnDelayChange;
    public bool isDatacenterIp { get; private set; }
    Random userRandom;
    int skipOn = 0;
    private static long ExemptCounter = 0;
    private static readonly int SkipGroups = 10;

    public StaticDelayHandler(TimeSpan currentDelay, SessionInfo sessionInfo, string clientIP)
    {
        CurrentDelay = currentDelay;
        this.sessionInfo = sessionInfo;
        if (clientIP == null)
            return;
        userRandom = new Random(sessionInfo.McUuid.GetHashCode());
    }

    public async Task<DateTime> AwaitDelayForFlip(FlipInstance flipInstance)
    {
        // simple calculation
        if (flipInstance.Profit > 200_000_000 || (flipInstance.Profit > 100_000_000 || flipInstance.ProfitPercentage > 900) && Random.Shared.NextDouble() < 0.5 || userRandom.NextDouble() < 0.01)
            return DateTime.UtcNow;
        if (IsLikelyBot(flipInstance))
            return DateTime.UtcNow;
        if (IsHighCompetitionKey(flipInstance) && CurrentDelay > TimeSpan.Zero)
        { // somebody else seems to be macroing this key, so cut the delay short
            await Task.Delay(Math.Min(CurrentDelay.Milliseconds, Random.Shared.Next(10, 40)));
            return DateTime.UtcNow;
        }
        if (CurrentDelay > TimeSpan.Zero)
            await Task.Delay(CurrentDelay);
        if (userRandom.NextDouble() < 0.1 * (sessionInfo.Purse == 0 ? 2 : 0.5)) // sampling dropout
        {
            Activity.Current.Log("Dropout");
            await Task.Delay(TimeSpan.FromSeconds(6)).ConfigureAwait(false);
        }
        if (sessionInfo.IsMacroBot && flipInstance.Profit > 1_000_000)
        {
            var delayAmunt = flipInstance.Profit / 1_000_000 * 55;
            if (CurrentDelay < TimeSpan.FromMilliseconds(100))
                delayAmunt /= 2;
            Activity.Current.Log($"BAF {delayAmunt}");
            await Task.Delay(TimeSpan.FromMilliseconds(flipInstance.Profit / 1_000_000 * 55)).ConfigureAwait(false);
        }
        return DateTime.UtcNow;
    }

    /// <summary>
    /// Flips that any bot would also find are not delayed
    /// There is a 30% chance in the transition phase
    /// </summary>
    /// <param name="flipInstance"></param>
    /// <returns></returns>
    public bool IsLikelyBot(FlipInstance flipInstance)
    {
        return (flipInstance.ProfitPercentage > 300
            || flipInstance.Profit < 1_100_000
            || flipInstance.Auction.Enchantments.Count == 0 && flipInstance.Auction.FlatenedNBT.Count < 4
            || flipInstance.Profit > 50_000_000 / Math.Min(Math.Max(flipInstance.Volume, 2), 8) || flipInstance.Volume >= 24 || IsHighCompetitionKey(flipInstance) && flipInstance.Volume > 10)
            && Math.Abs(flipInstance.Auction.UId % SkipGroups) == skipOn;
    }

    private bool IsHighCompetitionKey(FlipInstance flipInstance)
    {
        return flipInstance.Context != null && highCompetitionKeys.Contains(flipInstance.Context.GetValueOrDefault("key", "nope"));
    }

    public Task<DelayHandler.Summary> Update(IEnumerable<string> ids, DateTime lastCaptchaSolveTime, string licenseOn = null)
    {
        Interlocked.Increment(ref ExemptCounter);
        skipOn = (int)(ExemptCounter % SkipGroups);
        Console.WriteLine($"Updated delay, now skipping {skipOn} for {sessionInfo.McUuid}");
        // nothing todo, gets set by the socket
        return Task.FromResult(new DelayHandler.Summary() { VerifiedMc = true, Penalty = CurrentDelay });
    }
}