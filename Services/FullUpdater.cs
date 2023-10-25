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

namespace Coflnet.Sky.BFCS.Services
{
    public class FullUpdater : Updater.Updater
    {
        Sky.Sniper.Services.SniperService sniper;
        ActiveUpdater activeUpdater;
        ILogger<FullUpdater> logger;


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
                if (a.Bin)
                    sniper.TestNewAuction(a, false);
            });
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
                    catch (System.Exception e)
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

        public override void AddSoldAuctions(IEnumerable<SaveAuction> auctionsToAdd, Activity span)
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
}
