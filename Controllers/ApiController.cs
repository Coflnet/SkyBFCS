using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System;
using Coflnet.Sky.Core;
using Coflnet.Sky.Sniper.Services;

namespace Coflnet.Sky.BFCS.Controllers
{
    /// <summary>
    /// Main Controller handling tracking
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class ApiController : ControllerBase
    {
        private readonly SniperService service;

        /// <summary>
        /// Creates a new instance of <see cref="ApiController"/>
        /// </summary>
        /// <param name="service"></param>
        public ApiController(SniperService service)
        {
            this.service = service;
        }


        /// <summary>
        /// Indicates status of service, should be 200 (OK)
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("mockFlip")]
        public void MockFlip([FromBody] LowPricedAuction[] flips)
        {
            foreach (var item in flips)
            {
                item.Auction.Uuid = Guid.NewGuid().ToString().Replace("-", "");
                item.Auction.Context = new() { { "pre-api", "" }, { "cname", item.Auction.ItemName } };
                service.MockFoundFlip(item);
            }
        }
    }
}
