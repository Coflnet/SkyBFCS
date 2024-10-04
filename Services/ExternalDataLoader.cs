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
using System.Collections.Concurrent;

namespace Coflnet.Sky.BFCS.Services
{
    public class ExternalDataLoader
    {
        private SniperService sniper;
        private IConfiguration config;
        private ILogger<ExternalDataLoader> logger;
        private ISniperApi api;
        private ICraftCostService craftCostService;
        private IPersitanceManager persitanceManager;
        public ExternalDataLoader(SniperService sniper, IConfiguration config, ILogger<ExternalDataLoader> logger, ISniperApi api, ICraftCostService craftCostService, IPersitanceManager persitanceManager)
        {
            this.sniper = sniper;
            this.config = config;
            this.logger = logger;
            this.api = api;
            this.craftCostService = craftCostService;
            this.persitanceManager = persitanceManager;
        }

        public async Task Load()
        {
            try
            {
                logger.LogInformation("Loading external data");
                if (sniper.State < SniperState.Ready)
                    sniper.State = SniperState.LadingLookup;
                var errorCount = 0;
                await Parallel.ForEachAsync(Enumerable.Range(0, 100), new ParallelOptions() { MaxDegreeOfParallelism = 3 },
                async (id, c) =>
                {
                    var loadErrors = await LoadItemData(id);
                    errorCount += loadErrors;
                });
                await LoadCraftCost();
                if (errorCount > 0)
                    sniper.State = SniperState.Ready;
                else
                    sniper.State = SniperState.FullyLoaded;
                logger.LogInformation($"done loading external data {sniper.State}");
            }
            catch (Exception e)
            {
                logger.LogError(e, $"Error loading external data via {api.Configuration.BasePath}");
            }
        }

        private async Task LoadCraftCost()
        {
            logger.LogInformation("Loading craft cost");
            var crafts = await api.ApiSniperCraftCostGetAsync();
            foreach (var item in crafts)
            {
                craftCostService.Costs[item.Key] = item.Value;
            }
            logger.LogInformation("done loading craft cost of {count} items", crafts.Count);
        }

        private async Task<int> LoadItemData(int groupId, int retryCount = 0)
        {
            try
            {
                var data = await NewMethod(groupId);
                foreach (var item in data)
                {
                    sniper.AddLookupData(item.Key, item.Value);
                }
                logger.LogInformation("imported auction data for {0} total of {count}", groupId, data.Count());
            }
            catch (Exception e)
            {
                if (retryCount > 3)
                    return 1;
                logger.LogError(e, $"Error loading {groupId}");
                await Task.Delay((retryCount + 1) * 2000);
                return await LoadItemData(groupId, retryCount + 1);
            }
            return 0;
        }

        private async Task<List<KeyValuePair<string, PriceLookup>>> NewMethod(int groupId)
        {
            var internalLoad = persitanceManager.LoadGroup(groupId);
            if (internalLoad != null)
            {
                return await internalLoad;
            }
            var data = (await api.ApiSniperLookupGroupGroupIdGetAsync(groupId)).Trim('"');
            if (data == null)
                throw new Exception("No data found " + groupId);
            var bytes = Convert.FromBase64String(data);
            return MessagePackSerializer.Deserialize<IEnumerable<IGrouping<int, KeyValuePair<string, PriceLookup>>>>(bytes, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block)).First().ToList();
        }
    }

    /// <summary>
    /// actually works differently than designed intialially via the external data loader
    /// </summary>
    public class ExternalPeristenceManager : IPersitanceManager
    {
        public Task<ConcurrentDictionary<string, AttributeLookup>> GetWeigths()
        {
            throw new NotImplementedException();
        }

        public Task<List<KeyValuePair<string, PriceLookup>>> LoadGroup(int groupId)
        {
            return null;
        }

        public Task LoadLookups(SniperService service)
        {
            throw new NotImplementedException();
        }

        public Task SaveLookup(ConcurrentDictionary<string, PriceLookup> lookups)
        {
            throw new NotImplementedException();
        }

        public Task SaveWeigths(ConcurrentDictionary<string, AttributeLookup> lookups)
        {
            throw new NotImplementedException();
        }
    }
}
