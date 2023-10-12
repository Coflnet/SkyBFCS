using System.Threading.Tasks;
using System;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Coflnet.Sky.Sniper.Services;
using System.Net.Http;
using StackExchange.Redis;
using Coflnet.Sky.Updater;
using Microsoft.Extensions.Logging;

namespace Coflnet.Sky.BFCS.Services
{
    public class UpdaterService : BackgroundService
    {
        private SniperService sniper;
        private static HttpClient httpClient = new HttpClient();
        private IConnectionMultiplexer redis;
        private ExternalDataLoader externalLoader;
        private FullUpdater fullUpdater;
        private ILogger<UpdaterService> logger;

        public UpdaterService(SniperService sniper, IConnectionMultiplexer redis, ExternalDataLoader externalLoader, FullUpdater fullUpdater, ILogger<UpdaterService> logger)
        {
            this.sniper = sniper;
            this.redis = redis;
            this.externalLoader = externalLoader;
            this.fullUpdater = fullUpdater;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await DoFullUpdate(stoppingToken);

            var prod = redis?.GetSubscriber();
            sniper.FoundSnipe += (lp) =>
            {
                if (lp.TargetPrice < 2_000_000 || (float)lp.TargetPrice / lp.Auction.StartingBid < 1.08 || lp.DailyVolume < 0.2 || lp.Finder == Core.LowPricedAuction.FinderType.STONKS)
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
            StartBackgroundFullUpdates(stopping);
            new SingleBazaarUpdater(sniper).UpdateForEver(null);
            await updater.DoUpdates(0, stoppingToken).ConfigureAwait(false);
        }

        private async Task DoFullUpdate(CancellationToken stoppingToken)
        {
            Console.WriteLine("doing full update");
            while (true)
            {
                try
                {
                    await fullUpdater.Update(true);
                    break;
                }
                catch (System.Exception e)
                {
                    logger.LogError(e, "Error updating full");
                    await Task.Delay(1000 * 60, stoppingToken);
                }
            }
            Console.WriteLine("=================\ndone full update");
        }

        private void StartBackgroundFullUpdates(CancellationToken stopping)
        {
            _ = Task.Run(async () =>
            {
                Console.WriteLine("starting updater");
                while (!stopping.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(8), stopping);
                        await fullUpdater.Update(true);
                        await Task.Delay(TimeSpan.FromMinutes(2), stopping);
                        RefreshAllMedians();
                    }
                    catch (System.Exception e)
                    {
                        logger.LogError(e, "Error updating full");
                    }
                }
            }).ConfigureAwait(false);
        }

        private void RefreshAllMedians()
        {
            foreach (var item in sniper.Lookups)
            {
                foreach (var bucket in item.Value.Lookup)
                {
                    // make sure all medians are up to date
                    sniper.UpdateMedian(bucket.Value, (item.Key, bucket.Key));
                }
            }
        }
    }
}
