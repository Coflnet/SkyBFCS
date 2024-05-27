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
    private readonly SessionInfo sessionInfo;

    public event Action<TimeSpan> OnDelayChange;
    IMinecraftSocket socket;
    public bool isDatacenterIp { get; private set; }

    public StaticDelayHandler(TimeSpan currentDelay, SessionInfo sessionInfo, string clientIP)
    {
        CurrentDelay = currentDelay;
        this.sessionInfo = sessionInfo;
        if (clientIP == null)
            return;
        // check if ip is from datacenter range 107.152.32.0/20
        uint addressAsInt = BitConverter.ToUInt32(IPAddress.Parse(clientIP).GetAddressBytes().Reverse().ToArray(), 0);
        (uint lower, uint upper) = GetIpRange("107.152.32.0/20");
        isDatacenterIp = lower < addressAsInt && addressAsInt < upper;
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
        if (flipInstance.Profit > 100_000_000)
            return DateTime.UtcNow;
        if (IsLikelyBot(flipInstance) && Random.Shared.NextDouble() < 0.5)
            return DateTime.UtcNow;
        if (CurrentDelay > TimeSpan.Zero)
            await Task.Delay(CurrentDelay);
        if (!sessionInfo.IsMacroBot && isDatacenterIp && CurrentDelay < TimeSpan.FromSeconds(0.1) && Random.Shared.NextDouble() < 0.8)
            await Task.Delay(TimeSpan.FromSeconds(4) - CurrentDelay).ConfigureAwait(false);
        else if (Random.Shared.NextDouble() < 0.1 * (sessionInfo.Purse == 0 ? 5 : 0.5)) // sampling dropout
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
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