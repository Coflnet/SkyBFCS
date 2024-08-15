using System;
using System.IO;
using System.Reflection;
using Coflnet.Sky.BFCS.Services;
using Coflnet.Sky.Sniper.Services;
using Coflnet.Sky.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Prometheus;
using StackExchange.Redis;
using Coflnet.Sky.Sniper.Client.Api;
using Coflnet.Sky.Commands.Shared;
using Coflnet.Sky.ModCommands.Services;
using Coflnet.Sky.Core.Services;
using System.Net.Http;
using Coflnet.Sky.Commands.MC;

namespace Coflnet.Sky.BFCS
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddNewtonsoftJson();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "SkyBase", Version = "v1" });
                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });

           // services.AddHostedService<UpdaterService>();
            services.AddJaeger(Configuration, 1);
            services.AddSingleton<SniperService>();
            services.AddSingleton<ITokenService, Sniper.Services.TokenService>();
            services.AddSingleton<IBlockedService, StaticBlockedService>();
            services.AddSingleton<ActiveUpdater>();
            services.AddSingleton<FullUpdater>();
            services.AddSingleton<Kafka.KafkaCreator>();
            if (Configuration["MINIO_SECRET"] != null)
                services.AddSingleton<IPersitanceManager, S3PersistanceManager>();
            else
                services.AddSingleton<IPersitanceManager, ExternalPeristenceManager>(s => new());
            services.AddSingleton<IConnectionMultiplexer>(provider =>
            {
                var logger = provider.GetRequiredService<ILogger<Startup>>();
                if (Configuration["FLIP_REDIS_OPTIONS"] == null)
                {
                    logger.LogWarning("Did not find a redis connection, keeping flips internal");
                    return null;
                }
                var redisOptions = ConfigurationOptions.Parse(Configuration["FLIP_REDIS_OPTIONS"]);
                try
                {
                    return ConnectionMultiplexer.Connect(redisOptions);
                }
                catch (Exception)
                {
                    logger.LogError("Could not connect to redis, starting without");
                    return null;
                }
            });
            services.AddSingleton<ISniperApi, SniperApi>(c => new SniperApi(Configuration["SNIPER_BASE_URL"]));
            services.AddSingleton<ExternalDataLoader>();
            services.AddSingleton<IIsSold, IsSoldMock>();
            services.AddSingleton<HypixelItemService>();
            services.AddSingleton<BfcsBackgroundService>();
            services.AddSingleton<IAttributeFlipService, AttributeFlipService>();
            services.AddSingleton<HttpClient>();
            services.AddSingleton<SnipeUpdater>();
            services.AddSingleton<ICraftCostService, CraftCostService>();
            services.AddSingleton<IIsSold, NeverIsSoldService>();
            services.AddSingleton<ITutorialService, NothingTutorialService>();
            services.AddSingleton<IDelayExemptList, DelayExemptionList>();
            services.AddCoflService();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "SkyBase v1");
                c.RoutePrefix = "api";
            });

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapMetrics();
                endpoints.MapControllers();
            });
        }
    }
}
