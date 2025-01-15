using Coflnet.Sky.Updater;
using Confluent.Kafka;
using Coflnet.Sky.Core;
using Coflnet.Sky.Sniper.Services;
using Coflnet.Sky.Updater.Models;
using System.Threading.Channels;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Diagnostics;
using Prometheus;

namespace Coflnet.Sky.BFCS.Services;
public class SnipeUpdater : NewUpdater
{
    SniperService sniper;
    // protected override string ApiBaseUrl => "https://localhost:7013";
    Channel<Element> newAuctions;
    Channel<SaveAuction> userFinder;
    public event Action<SaveAuction> NewAuction;
    public HashSet<string> LowValueItems = new();
    public event Action UpdateProcessed;
    private Channel<SaveAuction> postProcessing;
    private int coreCount;
    Counter lowValueSkipped = Metrics.CreateCounter("sky_bfcs_low_value_skipped", "Number of low value items skipped");

    public SnipeUpdater(SniperService sniper) : base(Updater.Updater.activitySource, null)
    {
        this.sniper = sniper;
        newAuctions = Channel.CreateBounded<Element>(500);
        userFinder = Channel.CreateBounded<SaveAuction>(1000);
        postProcessing = Channel.CreateBounded<SaveAuction>(1000);
        SpawnWorker(sniper);
        SpawnWorker(sniper);
        SpawnWorker(sniper);
        SpawnUserFinder();
        // get the number of cores
        coreCount = Environment.ProcessorCount;
        Console.WriteLine("Info: Using " + coreCount + " processors");
    }

    private void SpawnUserFinder()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    var next = await userFinder.Reader.ReadAsync();
                    sniper.TestNewAuction(next);
                    NewAuction?.Invoke(next);
                    await postProcessing.Writer.WriteAsync(next);
                }
                catch (Exception e)
                {
                    dev.Logger.Instance.Error(e, "Testing user new auction");
                }
            }
        });
    }

    private void SpawnWorker(SniperService sniper)
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    var next = await newAuctions.Reader.ReadAsync().ConfigureAwait(false);
                    var isLast = newAuctions.Reader.Count == 0;
                    if (!next.auction.BuyItNow)
                        continue;
                    var a = Updater.Updater.ConvertAuction(next.auction, next.lastUpdated, next.findTime);
                    if (LowValueItems.Contains(a.Tag))
                    {
                        lowValueSkipped.Inc();
                        continue;
                    }
                    a.Context["upage"] = next.pageId.ToString();
                    a.Context["utry"] = next.tryCount.ToString();
                    a.Context["ucount"] = next.offset.ToString();
                    a.Context["frec"] = (DateTime.UtcNow - a.FindTime).ToString();
                    sniper.TestNewAuction(a, true, true);
                    await userFinder.Writer.WriteAsync(a);
                }
                catch (Exception e)
                {
                    dev.Logger.Instance.Error(e, "Testing new auction");
                }
            }
        });
    }

    protected override async Task<DateTime> DoOneUpdate(DateTime lastUpdate, IProducer<string, SaveAuction> p, int page, Activity siteSpan)
    {
        var pageToken = new CancellationTokenSource(20000);
        while (sniper.AllocatedDicts.Count < 1000)
        {
            sniper.AllocatedDicts.Enqueue(new(5));
        }
        var queries = new List<Task<(DateTime, int)>> { GetAndSavePage(page, p, lastUpdate, siteSpan, pageToken, 0) };
        if (coreCount > 1)
        {
            queries.Add(GetAndSavePage(page, p, lastUpdate, siteSpan, pageToken, 4));
        }
        if (coreCount > 3)
        {
            queries.Add(GetAndSavePage(page, p, lastUpdate, siteSpan, pageToken, 2));
        }
        var result = await Task.WhenAll(queries.ToArray());
        pageToken.Cancel();
        await Task.Delay(3);
        Console.WriteLine("Info: No more auctions");
        UpdateProcessed?.Invoke();
        // wait for other processing to finish before updating lbin
        await Task.Delay(2000);
        sniper.FinishedUpdate();
        sniper.PrintLogQueue();
        var all = new List<SaveAuction>();
        while (postProcessing.Reader.Count > 0)
        {
            all.Add(await postProcessing.Reader.ReadAsync());
        }
        var uuids = all.Select(a => a.Uuid).ToList();
        Console.WriteLine("Info: uuids found - " + all.Count + " " + string.Join(", ", uuids));

        return result.Max(a => a.Item1);
    }

    protected override void ProduceSells(List<SaveAuction> binupdate)
    {
        foreach (var item in binupdate)
            sniper.AddSoldItem(item);
        dev.Logger.Instance.Info("Recieved " + binupdate.Count + " sold items");
    }

    protected override IProducer<string, SaveAuction> GetProducer()
    {
        return new MockProd<SaveAuction>(a => sniper.TestNewAuction(a));
    }

    protected override void FoundNew(int pageId, IProducer<string, SaveAuction> p, AuctionPage page, int tryCount, Auction auction, Activity prodSpan, int count)
    {
        newAuctions.Writer.WriteAsync(new Element()
        {
            auction = auction,
            lastUpdated = page.LastUpdated,
            pageId = pageId,
            tryCount = tryCount,
            offset = count,
            findTime = DateTime.UtcNow
        }).ConfigureAwait(false);
    }

    public class Element
    {
        public Auction auction;
        public int pageId;
        public int tryCount;
        public DateTime lastUpdated;
        public DateTime findTime;
        public int offset;
    }
}
