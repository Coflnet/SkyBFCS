using System.Threading.Tasks;
using System;
using System.Threading;
using Microsoft.Extensions.Hosting;
using Coflnet.Sky.Sniper.Services;
using System.Net.Http;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using Prometheus;
using System.Collections.Generic;
using System.Linq;

namespace Coflnet.Sky.BFCS.Services
{
    public class UpdaterService : BackgroundService
    {
        private readonly SniperService sniper;
        private static readonly HttpClient httpClient = new HttpClient();
        private readonly IConnectionMultiplexer redis;
        private readonly ExternalDataLoader externalLoader;
        private readonly FullUpdater fullUpdater;
        private readonly ILogger<UpdaterService> logger;
        private readonly SnipeUpdater updater;
        private readonly BfcsBackgroundService bfcsBackgroundService;

        private readonly Gauge firstFlipPublished = Metrics.CreateGauge("sky_update_first_flip", "Time till first flip was sent to redis");

        public UpdaterService(SniperService sniper,
                              IConnectionMultiplexer redis,
                              ExternalDataLoader externalLoader,
                              FullUpdater fullUpdater,
                              ILogger<UpdaterService> logger,
                              SnipeUpdater updater,
                              BfcsBackgroundService bfcsBackgroundService)
        {
            this.sniper = sniper;
            this.redis = redis;
            this.externalLoader = externalLoader;
            this.fullUpdater = fullUpdater;
            this.logger = logger;
            this.updater = updater;
            this.bfcsBackgroundService = bfcsBackgroundService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            new SingleBazaarUpdater(sniper).UpdateForEver(null);
            await sniper.Init();
            logger.LogInformation("Init: ran sniper init");

            _ = Task.Run(async () =>
            {
                logger.LogInformation("Init: loading external data");
                await externalLoader.Load();
                await Task.Delay(TimeSpan.FromHours(0.5), stoppingToken);
                // load again in case the main instance didn't have all items at the time
                await externalLoader.Load();
            }).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // give other services time to load
            await DoFullUpdate(stoppingToken);
            logger.LogInformation("Init: done full update");

            var prod = redis?.GetSubscriber();
            var firstPublished = DateTime.MinValue;
            sniper.FoundSnipe += (lp) =>
            {
                if (lp.TargetPrice < 2_000_000 || (float)lp.TargetPrice / lp.Auction.StartingBid < 1.08 || lp.DailyVolume < 0.2 || lp.Finder == Core.LowPricedAuction.FinderType.STONKS)
                    return;
                prod?.Publish(new RedisChannel("snipes", RedisChannel.PatternMode.Literal), MessagePack.MessagePackSerializer.Serialize(lp), CommandFlags.FireAndForget);
                if (firstPublished < lp.Auction.FindTime - TimeSpan.FromSeconds(20))
                {
                    var apiUpdateTime = lp.Auction.FindTime;
                    firstPublished = DateTime.UtcNow; // prevent multiple updates in same minute
                    firstFlipPublished.Set(DateTime.UtcNow.Subtract(apiUpdateTime).TotalSeconds);
                }
                Console.WriteLine($"found {lp.Finder} :O {lp.Auction.Uuid} {lp.Auction.ItemName}");
                var timestamp = (long)DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
                Task.Run(async () =>
                {
                    await Task.Delay(500).ConfigureAwait(false);
                    await httpClient.PostAsync($"https://sky.coflnet.com/api/flip/track/found/{lp.Auction.Uuid}?finder=test&price={lp.TargetPrice}&timeStamp={timestamp}", null);
                }).ConfigureAwait(false);
            };
            await bfcsBackgroundService.SubscribeToRedisSnipes(stoppingToken);
            logger.LogInformation("Init: subscribed to redis");

            var stopping = stoppingToken;
            StartBackgroundFullUpdates(stopping);
            logger.LogInformation("Init: starting updates");
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
                var waitChannel = Channel.CreateBounded<bool>(1);
                updater.UpdateProcessed += () =>
                {
                    waitChannel.Writer.TryWrite(true);
                };
                logger.LogInformation("starting updater");
                while (!stopping.IsCancellationRequested)
                {
                    try
                    {
                        // wait for fast update to be done
                        await waitChannel.Reader.ReadAsync(stopping);
                        await Task.Delay(TimeSpan.FromSeconds(1), stopping);

                        logger.LogInformation("doing full update");
                        await fullUpdater.Update(true);
                        await Task.Delay(TimeSpan.FromMinutes(1), stopping);
                        // wait for event updater.UpdateProcessed
                        await waitChannel.Reader.ReadAsync(stopping);
                        await Task.Delay(TimeSpan.FromSeconds(1), stopping);
                        await RefreshAllMedians();
                        await Task.Delay(TimeSpan.FromMinutes(1), stopping);
                    }
                    catch (System.Exception e)
                    {
                        logger.LogError(e, "Error updating full");
                    }
                }
            }).ConfigureAwait(false);
        }

        private async Task RefreshAllMedians()
        {
            logger.LogInformation("Refreshing all medians");
            var lowValueItems = new HashSet<string>();
            foreach (var item in sniper.Lookups)
            {
                foreach (var bucket in item.Value.Lookup)
                {
                    if (bucket.Value.References.Count < 4)
                        continue; // can't have a median
                    // make sure all medians are up to date
                    sniper.UpdateMedian(bucket.Value, (item.Key, sniper.GetBreakdownKey(bucket.Key, item.Key)));
                }
                await Task.Delay(5); // prevent blocking the thread
                if (item.Value.Lookup.All(l => l.Value.Price < 3_000_000))
                { // can't be more than 3m profit (usually minprofit) if the median is lower than that
                    lowValueItems.Add(item.Key);
                }
            }
            updater.LowValueItems = lowValueItems;
            logger.LogInformation("Done refreshing all medians, low value are {lowValueItems}", string.Join(", ", lowValueItems));
        }
    }
}
