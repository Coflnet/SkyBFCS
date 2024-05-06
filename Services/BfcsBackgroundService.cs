using Coflnet.Sky.Core;
using System.Threading.Tasks;
using Coflnet.Sky.ModCommands.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;

namespace Coflnet.Sky.BFCS.Services;

public class BfcsBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<ModBackgroundService> logger) 
    : ModBackgroundService(scopeFactory, config, logger, null, null)
{
    public event Action<LowPricedAuction> FoundSnipe;

    protected override Task DistributeFlipOnServer(LowPricedAuction flip)
    {
        FoundSnipe?.Invoke(flip);
        return Task.CompletedTask;
    }
}