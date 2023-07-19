using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Coflnet.Sky.BFCS.Models;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Collections;
using System.Collections.Generic;
using Coflnet.Sky.BFCS.Services;

namespace Coflnet.Sky.BFCS.Controllers
{
    /// <summary>
    /// Main Controller handling tracking
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class ApiController : ControllerBase
    {
        private readonly BaseDbContext db;
        private readonly UpdaterService service;

        /// <summary>
        /// Creates a new instance of <see cref="ApiController"/>
        /// </summary>
        /// <param name="context"></param>
        /// <param name="service"></param>
        public ApiController(BaseDbContext context, UpdaterService service)
        {
            db = context;
            this.service = service;
        }
    }
}
