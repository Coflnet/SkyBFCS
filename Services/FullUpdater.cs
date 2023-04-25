using System;
using Confluent.Kafka;
using Coflnet.Sky.Core;
using Coflnet.Sky.Sniper.Services;
using Coflnet.Sky.Updater.Models;
using Coflnet.Sky.Updater;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Coflnet.Sky.BFCS.Services
{
    public class FullUpdater : Updater.Updater
    {
        Sky.Sniper.Services.SniperService sniper;
        ActiveUpdater activeUpdater;
        ILogger<FullUpdater> logger;


        public FullUpdater(SniperService sniper, ActiveUpdater activeUpdater, ILogger<FullUpdater> logger, ActivitySource activitySource, Kafka.KafkaCreator kafkaCreator) 
            : base(null, new MockSkinHandler(), activitySource, kafkaCreator)
        {
            this.sniper = sniper;
            this.activeUpdater = activeUpdater;
            this.logger = logger;
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
            return new MockProd<AhStateSumary>((sum) =>
            {
                Console.WriteLine("received sum");
                Task.Run(async () =>
                {
                    try
                    {
                        if(sum == null)
                            return;
                        await activeUpdater.ProcessSumary(sum);
                    }
                    catch (System.Exception e)
                    {
                        logger.LogError(e, "Error processing sumary");
                    }
                });
            });
        }

        private class MockSkinHandler : IItemSkinHandler
        {
            public void StoreIfNeeded(SaveAuction parsed, Auction auction)
            {
            }
        }
    }
}
