using System.Threading.Tasks;
using Coflnet.Sky.Sniper.Services;
using Coflnet.Sky.Updater;

namespace Coflnet.Sky.BFCS.Services
{
    public class SingleBazaarUpdater : BazaarUpdater
    {
        private SniperService sniper;

        public SingleBazaarUpdater(SniperService sniper)
        {
            this.sniper = sniper;
            SecondsBetweenUpdates = 30;
        }

        protected override Task ProduceIntoQueue(dev.BazaarPull pull)
        {
            sniper.UpdateBazaar(pull);
            return Task.CompletedTask;
        }
    }
}
