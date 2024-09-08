using System;
using Coflnet.Sky.Commands.MC;
using Coflnet.Sky.Commands.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net;
using System.Linq;

namespace Coflnet.Sky.BFCS.Services;

public class StaticDelayHandler : IDelayHandler
{
    public TimeSpan CurrentDelay { get; set; }
    public TimeSpan MacroDelay => default;
    private readonly SessionInfo sessionInfo;
    private HashSet<string> highCompetitionKeys = [" Any [exp, 6] EPIC 1", " Any [exp, 6] LEGENDARY 1", " Any [candyUsed, 0],[exp, 0] LEGENDARY 1"];

    public event Action<TimeSpan> OnDelayChange;
    IMinecraftSocket socket;
    public bool isDatacenterIp { get; private set; }
    Random userRandom;
    int skipOn = 0;
    private static readonly int SkipGroups = 10;

    public StaticDelayHandler(TimeSpan currentDelay, SessionInfo sessionInfo, string clientIP)
    {
        CurrentDelay = currentDelay;
        this.sessionInfo = sessionInfo;
        if (clientIP == null)
            return;
        userRandom = new Random(sessionInfo.McUuid.GetHashCode());
    }

    public (uint lower, uint upper) GetIpRange(string mask)
    {
        var parts = mask.Split('/');
        var ip = IPAddress.Parse(parts[0]);
        var maskLength = int.Parse(parts[1]);
        var maskBytes = new byte[4];
        for (int i = 0; i < maskLength; i++)
        {
            maskBytes[i / 8] |= (byte)(1 << (7 - i % 8));
        }
        var ipBytes = ip.GetAddressBytes();
        var lowerBytes = new byte[4];
        var upperBytes = new byte[4];
        for (int i = 0; i < 4; i++)
        {
            lowerBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
            upperBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
        }
        return (BitConverter.ToUInt32(lowerBytes.Reverse().ToArray(), 0), BitConverter.ToUInt32(upperBytes.Reverse().ToArray(), 0));
    }

    public async Task<DateTime> AwaitDelayForFlip(FlipInstance flipInstance)
    {
        // simple calculation
        if (flipInstance.Profit > 200_000_000 || (flipInstance.Profit > 100_000_000 || flipInstance.ProfitPercentage > 900) && Random.Shared.NextDouble() < 0.5)
            return DateTime.UtcNow;
        if (IsLikelyBot(flipInstance))
            return DateTime.UtcNow;
        if (CurrentDelay > TimeSpan.Zero)
            await Task.Delay(CurrentDelay);
        if (!sessionInfo.IsMacroBot && isDatacenterIp && CurrentDelay < TimeSpan.FromSeconds(0.1) && userRandom.NextDouble() < 0.8)
            await Task.Delay(TimeSpan.FromSeconds(4) - CurrentDelay).ConfigureAwait(false);
        else if (userRandom.NextDouble() < 0.1 * (sessionInfo.Purse == 0 ? 5 : 0.5)) // sampling dropout
            await Task.Delay(TimeSpan.FromSeconds(6)).ConfigureAwait(false);
        if (sessionInfo.IsMacroBot && flipInstance.Profit > 1_000_000)
            await Task.Delay(TimeSpan.FromMilliseconds(flipInstance.Profit / 1_000_000 * 55)).ConfigureAwait(false);
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
            || flipInstance.Profit > 50_000_000 / Math.Min(Math.Max(flipInstance.Volume, 2), 8) || flipInstance.Volume >= 24 || IsHighCompetitionKey(flipInstance)) && flipInstance.Auction.UId % SkipGroups == skipOn;
    }

    private bool IsHighCompetitionKey(FlipInstance flipInstance)
    {
        return flipInstance.Context != null && highCompetitionKeys.Contains(flipInstance.Context.GetValueOrDefault("key", "nope")) && flipInstance.Volume > 10;
    }

    public Task<DelayHandler.Summary> Update(IEnumerable<string> ids, DateTime lastCaptchaSolveTime)
    {
        skipOn = userRandom.Next(0, SkipGroups);
        Console.WriteLine($"Updated delay, now skipping {skipOn}");
        // nothing todo, gets set by the socket
        return Task.FromResult(new DelayHandler.Summary() { VerifiedMc = true, Penalty = CurrentDelay });
    }
}