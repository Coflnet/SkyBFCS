using System;
using Confluent.Kafka;
using Coflnet.Sky.Core;
using Coflnet.Sky.Sniper.Services;
using Coflnet.Sky.Updater.Models;
using Coflnet.Sky.Updater;

namespace Coflnet.Sky.BFCS.Services
{
    public class FullUpdater : Updater.Updater
    {
        Sky.Sniper.Services.SniperService sniper;


        public FullUpdater(SniperService sniper) : base(null, new MockSkinHandler())
        {
            this.sniper = sniper;
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
