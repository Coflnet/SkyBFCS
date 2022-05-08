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
    public class BaseController : ControllerBase
    {
        private readonly BaseDbContext db;
        private readonly UpdaterService service;

        /// <summary>
        /// Creates a new instance of <see cref="BaseController"/>
        /// </summary>
        /// <param name="context"></param>
        public BaseController(BaseDbContext context, UpdaterService service)
        {
            db = context;
            this.service = service;
        }
    }
}
