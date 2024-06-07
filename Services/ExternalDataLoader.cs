using System.Threading.Tasks;
using Coflnet.Sky.Sniper.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Coflnet.Sky.Sniper.Client.Api;
using Coflnet.Sky.Sniper.Models;
using System;
using Coflnet.Sky.Core;
using System.Linq;
using System.Collections.Generic;
using MessagePack;

namespace Coflnet.Sky.BFCS.Services
{
    public class ExternalDataLoader
    {
        private SniperService sniper;
        private IConfiguration config;
        private ILogger<ExternalDataLoader> logger;
        private ISniperApi api;
        private ICraftCostService craftCostService;
        public ExternalDataLoader(SniperService sniper, IConfiguration config, ILogger<ExternalDataLoader> logger, ISniperApi api, ICraftCostService craftCostService)
        {
            this.sniper = sniper;
            this.config = config;
            this.logger = logger;
            this.api = api;
            this.craftCostService = craftCostService;
        }

        public async Task Load()
        {
            try
            {
                logger.LogInformation("Loading external data");
                if (sniper.State < SniperState.Ready)
                    sniper.State = SniperState.LadingLookup;
                await Parallel.ForEachAsync(Enumerable.Range(0, 100), new ParallelOptions() { MaxDegreeOfParallelism = 3 },
                async (id, c) =>
                {
                    await LoadItemData(id);
                });
                await LoadCraftCost();
                sniper.State = SniperState.Ready;
                logger.LogInformation("done loading external data");
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Error loading external data via {api.Configuration.BasePath}");
            }
        }

        private async Task LoadCraftCost()
        {
            logger.LogInformation("Loading craft cost");
            var crafts = await api.ApiSniperDumpCraftCostGetAsync(config["SNIPER_TRANSFER_TOKEN"]);
            foreach (var item in crafts)
            {
                craftCostService.Costs[item.Key] = item.Value;
            }
            logger.LogInformation("done loading craft cost of {count} items", crafts.Count);
        }

        private async Task LoadItemData(int groupId, int retryCount = 0)
        {
            try
            {
                var data = (await api.ApiSniperLookupGroupGroupIdGetAsync(groupId, config["SNIPER_TRANSFER_TOKEN"])).Trim('"');
                if (data == null)
                    return;
                var bytes = Convert.FromBase64String(data);
                var elements = MessagePack.MessagePackSerializer.Deserialize<IEnumerable<IGrouping<int, KeyValuePair<string, PriceLookup>>>>(bytes, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block));
                foreach (var item in elements.First())
                {
                    sniper.AddLookupData(item.Key, item.Value);
                }
                logger.LogInformation("imported auction data for {0} total of {count}", groupId, elements.First().Count());
            }
            catch (Exception e)
            {
                if (retryCount > 3)
                    return;
                logger.LogError(e, $"Error loading {groupId}");
                await Task.Delay((retryCount + 1) * 2000);
                await LoadItemData(groupId, retryCount + 1);
            }
        }
    }
}
