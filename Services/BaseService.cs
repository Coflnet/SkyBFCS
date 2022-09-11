using System.Threading.Tasks;
using Coflnet.Sky.BFCS.Models;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Coflnet.Sky.Updater;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Coflnet.Sky.Sniper.Services;
using Hypixel.NET;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using WebSocketSharp;
using System.Net.Http;
using StackExchange.Redis;
using Coflnet.Sky.Core;
using Microsoft.Extensions.Configuration;

namespace Coflnet.Sky.BFCS.Services
{
    public class UpdaterService : BackgroundService
    {
        private SniperService sniper;
        private static HttpClient httpClient = new HttpClient();
        private IConnectionMultiplexer redis;
        private ExternalDataLoader externalLoader;

        public UpdaterService(SniperService sniper, IConnectionMultiplexer redis, ExternalDataLoader externalLoader)
        {
            this.sniper = sniper;
            this.redis = redis;
            this.externalLoader = externalLoader;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("doing full update");
            await new FullUpdater(sniper).Update(true);
            Console.WriteLine("=================\ndone full update");

            var prod = redis?.GetSubscriber();
            sniper.FoundSnipe += (lp) =>
            {
                if (lp.TargetPrice < 2_000_000 || (float)lp.TargetPrice / lp.Auction.StartingBid < 1.08 || lp.DailyVolume < 0.2)
                    return;
                prod?.Publish("snipes", MessagePack.MessagePackSerializer.Serialize(lp), CommandFlags.FireAndForget);
                Console.WriteLine($"found {lp.Finder} :O {lp.Auction.Uuid} {lp.Auction.ItemName}");
                var timestamp = (long)DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
                Task.Run(async () =>
                {
                    await Task.Delay(500).ConfigureAwait(false);
                    await httpClient.PostAsync($"https://sky.coflnet.com/api/flip/track/found/{lp.Auction.Uuid}?finder=test&price={lp.TargetPrice}&timeStamp={timestamp}", null);
                }).ConfigureAwait(false);
            };

            var updater = new SnipeUpdater(sniper);
            var stopping = stoppingToken;
            _ = Task.Run(async () =>
            {
                Console.WriteLine("loading external");
                await externalLoader.Load();
            }).ConfigureAwait(false);
            await updater.DoUpdates(0, stoppingToken).ConfigureAwait(false);
        }
    }
}
