using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coflnet.Sky.Commands.MC;
using Coflnet.Sky.ModCommands.Services;

public class StaticBlockedService : IBlockedService
{
    public Task ArchiveBlockedFlipsUntil(ConcurrentQueue<MinecraftSocket.BlockedElement> topBlocked, string userId, int v)
    {
        return Task.CompletedTask;
    }

    public Task<IEnumerable<BlockedService.BlockedReason>> GetBlockedReasons(string userId, Guid auctionUuid)
    {
        throw new NotImplementedException();
    }
}