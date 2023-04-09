using Coflnet.Sky.Updater;
using Confluent.Kafka;
using Coflnet.Sky.Core;
using Coflnet.Sky.Sniper.Services;
using Coflnet.Sky.Updater.Models;
using OpenTracing;
using System.Threading.Channels;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Diagnostics;

namespace Coflnet.Sky.BFCS.Services
{
    public class SnipeUpdater : NewUpdater
    {
        Sky.Sniper.Services.SniperService sniper;
        // protected override string ApiBaseUrl => "https://localhost:7013";
        Channel<Element> newAuctions;

        public SnipeUpdater(SniperService sniper) : base(Updater.Updater.activitySource)
        {
            this.sniper = sniper;
            newAuctions = Channel.CreateUnbounded<Element>();
            SpawnWorker(sniper);
            SpawnWorker(sniper);
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
                        if (!next.auction.BuyItNow)
                            continue;
                        var a = Updater.Updater.ConvertAuction(next.auction, next.lastUpdated);
                        a.Context["upage"] = next.pageId.ToString();
                        a.Context["utry"] = next.tryCount.ToString();
                        sniper.TestNewAuction(a);
                    }
                    catch (System.Exception e)
                    {
                        dev.Logger.Instance.Error(e, "Testing new auction");
                    }
                }
            });
        }

        protected override async Task<DateTime> DoOneUpdate(DateTime lastUpdate, IProducer<string, SaveAuction> p, int page, Activity siteSpan)
        {
            var pageToken = new CancellationTokenSource(20000);
            var result = await Task.WhenAll(
                GetAndSavePage(page, p, lastUpdate, siteSpan, pageToken, 4),
                //GetAndSavePage(page, p, lastUpdate, siteSpan, pageToken, 2),
                GetAndSavePage(page, p, lastUpdate, siteSpan, pageToken, 0));
            pageToken.Cancel();
            // wait for other processing to finish before updating lbin
            await Task.Delay(1000);
            sniper.FinishedUpdate();
            sniper.PrintLogQueue();
            return result.Max(a => a.Item1);
        }

        protected override void ProduceSells(List<SaveAuction> binupdate)
        {
            foreach (var item in binupdate)
                sniper.AddSoldItem(item);
        }

        protected override IProducer<string, SaveAuction> GetProducer()
        {
            return new MockProd<SaveAuction>(a => sniper.TestNewAuction(a));
        }

        protected override void FoundNew(int pageId, IProducer<string, SaveAuction> p, AuctionPage page, int tryCount, Auction auction, Activity prodSpan)
        {
            newAuctions.Writer.WriteAsync(new Element()
            {
                auction = auction,
                lastUpdated = page.LastUpdated,
                pageId = pageId,
                tryCount = tryCount
            }).ConfigureAwait(false);
        }

        public class Element
        {
            public Auction auction;
            public int pageId;
            public int tryCount;
            public DateTime lastUpdated;
        }
    }
}
