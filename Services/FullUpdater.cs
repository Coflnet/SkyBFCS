using System;
using Confluent.Kafka;
using Coflnet.Sky.Core;
using Coflnet.Sky.Sniper.Services;
using Coflnet.Sky.Updater.Models;
using Coflnet.Sky.Updater;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace Coflnet.Sky.BFCS.Services;
public class FullUpdater : Updater.Updater
{
    readonly SniperService sniper;
    readonly ActiveUpdater activeUpdater;
    readonly ILogger<FullUpdater> logger;

    public FullUpdater(SniperService sniper, ActiveUpdater activeUpdater, ILogger<FullUpdater> logger, ActivitySource activitySource)
        : base(null, new MockSkinHandler(), activitySource, null)
    {
        this.sniper = sniper;
        this.activeUpdater = activeUpdater;
        this.logger = logger;
        MillisecondsDelay = 400;
    }

    protected override IProducer<string, SaveAuction> GetP()
    {
        return new MockProd<SaveAuction>(a =>
        {
            if (!a.Bin)
                return;
            ForwardAuction(a);
        });
    }

    protected override Task<int> Save(AuctionPage res, DateTime lastUpdate, AhStateSumary sumary, IProducer<string, SaveAuction> prod, ActivityContext pageSpanContext)
    {
        var a = res.Auctions//.Where(item => item.BuyItNow)
                .Select(a => ConvertAuction(a, res.LastUpdated));
        foreach (var auction in a)
        {
            if (auction.Bin)
                ForwardAuction(auction);
            sumary.ActiveAuctions[auction.UId] = auction.End.Ticks;
        }
        logger.LogInformation($"saving {a.Count()} bin auctions");
        return Task.FromResult(a.Count());
    }

    private void ForwardAuction(SaveAuction auction)
    {
        var isNew = auction.Start > DateTime.UtcNow.AddMinutes(-1);
        sniper.TestNewAuction(auction, isNew);
        if (isNew)
            logger.LogInformation($"New auction {auction.Uuid} {auction.UId} {auction.Tag} {auction.Start}");
    }

    protected override IProducer<string, AhStateSumary> GetSumaryProducer()
    {
        var semaphore = new SemaphoreSlim(1);
        return new MockProd<AhStateSumary>((sum) =>
        {
            Console.WriteLine("received sum");
            Task.Run(async () =>
            {
                try
                {
                    await semaphore.WaitAsync();
                    if (sum == null)
                        return;
                    await activeUpdater.ProcessSumary(sum);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Error processing sumary");
                }
                finally
                {
                    semaphore.Release();
                }
            });
        });
    }

    public override void AddSoldAuctions(List<SaveAuction> auctionsToAdd, Activity span)
    {
        foreach (var item in auctionsToAdd)
        {
            sniper.AddSoldItem(item);
        }
    }

    private class MockSkinHandler : IItemSkinHandler
    {
        public void StoreIfNeeded(SaveAuction parsed, Auction auction)
        {
        }
    }
}
