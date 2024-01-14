using System.Threading.Tasks;
using Coflnet.Sky.Sniper.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Coflnet.Sky.Sniper.Client.Api;
using Coflnet.Sky.Sniper.Models;
using System;

namespace Coflnet.Sky.BFCS.Services
{
    public class ExternalDataLoader
    {
        private SniperService sniper;
        private IConfiguration config;
        private ILogger<ExternalDataLoader> logger;
        private ISniperApi api;
        public ExternalDataLoader(SniperService sniper, IConfiguration config, ILogger<ExternalDataLoader> logger, ISniperApi api)
        {
            this.sniper = sniper;
            this.config = config;
            this.logger = logger;
            this.api = api;
        }

        public async Task Load()
        {
            try
            {
                logger.LogInformation("Loading external data");
                if (sniper.State < SniperState.Ready)
                    sniper.State = SniperState.LadingLookup;
                var ids = await api.ApiSniperLookupGetAsync();
                logger.LogInformation("done with ids");
                foreach (var id in ids)
                {
                    await LoadItemData(id);
                }
                sniper.State = SniperState.Ready;
                logger.LogInformation("done loading external data");
            }
            catch (System.Exception e)
            {
                logger.LogError(e, "Error loading external data");
            }
        }

        private async Task LoadItemData(string id, int retryCount = 0)
        {
            var data = (await api.ApiSniperLookupItemIdGetAsync(id, config["SNIPER_TRANSFER_TOKEN"])).Trim('"');
            if (data == null)
                return;
            try
            {
                var bytes = Convert.FromBase64String(data);
                var elements = MessagePack.MessagePackSerializer.Deserialize<PriceLookup>(bytes);
                sniper.AddLookupData(id, elements);
                logger.LogInformation("imported auction data for {0} total of {count}", id, elements.Lookup.Count);
            }
            catch (System.Exception e)
            {
                if (retryCount > 3)
                    return;
                logger.LogError(e, $"Error loading {id}\n{data}");
                await Task.Delay(2000);
                await LoadItemData(id, retryCount + 1);
            }
        }
    }
}
