using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coflnet.Sky.BFCS.Services;
using Coflnet.Sky.Commands.MC;
using Coflnet.Sky.ModCommands.Services;

public class StaticBlockedService : IBlockedService
{
    private SniperSocket sniperSocket;

    public StaticBlockedService(SniperSocket sniperSocket)
    {
        this.sniperSocket = sniperSocket;
    }

    public async Task ArchiveBlockedFlipsUntil(ConcurrentQueue<MinecraftSocket.BlockedElement> topBlocked, string userId, int v)
    {
        var batch = new List<MinecraftSocket.BlockedElement>();
        while (topBlocked.TryDequeue(out var item))
        {
            batch.Add(item);
            if (batch.Count > 50 || topBlocked.IsEmpty)
            {
                sniperSocket.SendToServer(Response.Create(typeof(BlockedImportCommand).Name, batch));
                batch.Clear();
                await Task.Delay(50);
            }
        }
    }

    public Task<IEnumerable<BlockedService.BlockedReason>> GetBlockedReasons(string userId, Guid auctionUuid)
    {
        throw new NotImplementedException();
    }
}