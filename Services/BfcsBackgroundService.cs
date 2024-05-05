using Coflnet.Sky.Core;
using System.Threading.Tasks;
using Coflnet.Sky.ModCommands.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Coflnet.Sky.Commands.Shared;
using System;

public class BfcsBackgroundService : ModBackgroundService
{
    public BfcsBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<ModBackgroundService> logger)
        : base(scopeFactory, config, logger, null, null)
    {
    }

    public event Action<LowPricedAuction> FoundSnipe;

    protected override Task DistributeFlipOnServer(LowPricedAuction flip)
    {
        FoundSnipe?.Invoke(flip);
        return Task.CompletedTask;
    }
}