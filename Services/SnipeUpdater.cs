using Coflnet.Sky.Updater;
using Confluent.Kafka;
using hypixel;
using Coflnet.Sky.Sniper.Services;
using RestSharp;
using Coflnet.Sky.Updater.Models;
using OpenTracing;
using System.Threading.Channels;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace Coflnet.Sky.Base.Services
{
    public class SnipeUpdater : NewUpdater
    {
        Sky.Sniper.Services.SniperService sniper;
        // protected override string ApiBaseUrl => "https://localhost:7013";
        Channel<Element> newAuctions;

        public SnipeUpdater(SniperService sniper)
        {
            this.sniper = sniper;
            newAuctions = Channel.CreateUnbounded<Element>();
            Task.Run(async () =>
            {
                while (true)
                {
                    var next = await newAuctions.Reader.ReadAsync();
                    var a = Updater.Updater.ConvertAuction(next.auction, next.lastUpdated);
                    if (!a.Bin)
                        continue;
                    a.Context["upage"] = next.pageId.ToString();
                    a.Context["utry"] = next.tryCount.ToString();
                    sniper.TestNewAuction(a);
                }
            });
        }

        protected override async Task<DateTime> DoOneUpdate(DateTime lastUpdate, IProducer<string, SaveAuction> p, int page, OpenTracing.IScope siteSpan)
        {
            var pageToken = new CancellationTokenSource(20000);
            var result = await Task.WhenAll(
                GetAndSavePage(page, p, lastUpdate, siteSpan, pageToken, 4),
                // GetAndSavePage(page, p, lastUpdate, siteSpan, pageToken, 2),
                GetAndSavePage(page, p, lastUpdate, siteSpan, pageToken, 0));
            pageToken.Cancel();
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

        protected override void FoundNew(int pageId, IProducer<string, SaveAuction> p, AuctionPage page, int tryCount, Auction auction, ISpan prodSpan)
        {
            newAuctions.Writer.WriteAsync(new Element()
            {
                auction = auction,
                lastUpdated = page.LastUpdated,
                pageId = pageId,
                tryCount = tryCount
            });
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
