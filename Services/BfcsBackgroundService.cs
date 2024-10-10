using Coflnet.Sky.Core;
using System.Threading.Tasks;
using Coflnet.Sky.ModCommands.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using Coflnet.Sky.Commands.MC;
using Coflnet.Sky.Commands.Shared;

namespace Coflnet.Sky.BFCS.Services;

public class BfcsBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<ModBackgroundService> logger, IDelayExemptList exemptList,
    FilterStateService filterStateService) 
    : ModBackgroundService(scopeFactory, config, logger, null, null, exemptList, filterStateService)
{
    public event Action<LowPricedAuction> FoundSnipe;

    protected override Task DistributeFlipOnServer(LowPricedAuction flip)
    {
        FoundSnipe?.Invoke(flip);
        return Task.CompletedTask;
    }
}