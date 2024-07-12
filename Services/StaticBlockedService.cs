using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coflnet.Sky.ModCommands.Services;

public class StaticBlockedService : IBlockedService
{
    public Task AddBlockedReason(BlockedService.BlockedReason reason)
    {
        return Task.CompletedTask; // already saved by the sniper/mod eu instance
    }

    public Task<IEnumerable<BlockedService.BlockedReason>> GetBlockedReasons(string userId, Guid auctionUuid)
    {
        throw new NotImplementedException();
    }
}