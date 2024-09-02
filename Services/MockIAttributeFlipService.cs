using System.Collections.Concurrent;
using System.Threading.Tasks;
using Coflnet.Sky.Sniper.Models;
using Coflnet.Sky.Sniper.Services;

namespace Coflnet.Sky.BFCS.Services;

public class MockIAttributeFlipService : IAttributeFlipService
{
    public ConcurrentDictionary<(string, AuctionKey), AttributeFlip> Flips { get; } = new();

    public Task Update()
    {
        return Task.CompletedTask;
    }
}