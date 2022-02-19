using System;
using Confluent.Kafka;
using hypixel;
using Coflnet.Sky.Sniper.Services;
using RestSharp;

namespace Coflnet.Sky.Base.Services
{
    public class FullUpdater : Updater.Updater
    {
        Sky.Sniper.Services.SniperService sniper;


        public FullUpdater(SniperService sniper) : base(null)
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

        /*  protected override RestClient GetClient()
          {
              return new RestClient("https://localhost:7013");
          }*/
    }
}
