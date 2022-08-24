using System;
using System.IO;
using System.Reflection;
using Coflnet.Sky.BFCS.Models;
using Coflnet.Sky.BFCS.Services;
using Coflnet.Sky.Sniper.Services;
using Coflnet.Sky.Core;
using Jaeger.Samplers;
using Jaeger.Senders;
using Jaeger.Senders.Thrift;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using OpenTracing;
using OpenTracing.Util;
using Prometheus;
using StackExchange.Redis;
using Coflnet.Sky.Sniper.Client.Api;
using System.Threading.Tasks;
using System.Net;
using StackExchange.Redis.Profiling;

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
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "SkyBase", Version = "v1" });
                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });

            services.AddHostedService<UpdaterService>();
            services.AddJaeger();
            services.AddTransient<SniperService>();
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
                catch (System.Exception)
                {
                    logger.LogError("Could not connect to redis, starting without");
                    return null;
                }
            });
            services.AddSingleton<ISniperApi, SniperApi>(c => new SniperApi(Configuration["SNIPER_BASE_URL"]));
            services.AddSingleton<ExternalDataLoader>();
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

        public class NoRedisRedisConnection : IConnectionMultiplexer
        {
            public string ClientName => throw new NotImplementedException();

            public string Configuration => throw new NotImplementedException();

            public int TimeoutMilliseconds => throw new NotImplementedException();

            public long OperationCount => throw new NotImplementedException();

            public bool PreserveAsyncOrder { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public bool IsConnected => throw new NotImplementedException();

            public bool IsConnecting => throw new NotImplementedException();

            public bool IncludeDetailInExceptions { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public int StormLogThreshold { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public event EventHandler<RedisErrorEventArgs> ErrorMessage;
            public event EventHandler<ConnectionFailedEventArgs> ConnectionFailed;
            public event EventHandler<InternalErrorEventArgs> InternalError;
            public event EventHandler<ConnectionFailedEventArgs> ConnectionRestored;
            public event EventHandler<EndPointEventArgs> ConfigurationChanged;
            public event EventHandler<EndPointEventArgs> ConfigurationChangedBroadcast;
            public event EventHandler<HashSlotMovedEventArgs> HashSlotMoved;

            public void Close(bool allowCommandsToComplete = true)
            {
                throw new NotImplementedException();
            }

            public Task CloseAsync(bool allowCommandsToComplete = true)
            {
                throw new NotImplementedException();
            }

            public bool Configure(TextWriter log = null)
            {
                throw new NotImplementedException();
            }

            public Task<bool> ConfigureAsync(TextWriter log = null)
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public void ExportConfiguration(Stream destination, ExportOptions options = (ExportOptions)(-1))
            {
                throw new NotImplementedException();
            }

            public ServerCounters GetCounters()
            {
                throw new NotImplementedException();
            }

            public IDatabase GetDatabase(int db = -1, object asyncState = null)
            {
                throw new NotImplementedException();
            }

            public EndPoint[] GetEndPoints(bool configuredOnly = false)
            {
                throw new NotImplementedException();
            }

            public int GetHashSlot(RedisKey key)
            {
                throw new NotImplementedException();
            }

            public IServer GetServer(string host, int port, object asyncState = null)
            {
                throw new NotImplementedException();
            }

            public IServer GetServer(string hostAndPort, object asyncState = null)
            {
                throw new NotImplementedException();
            }

            public IServer GetServer(IPAddress host, int port)
            {
                throw new NotImplementedException();
            }

            public IServer GetServer(EndPoint endpoint, object asyncState = null)
            {
                throw new NotImplementedException();
            }

            public string GetStatus()
            {
                throw new NotImplementedException();
            }

            public void GetStatus(TextWriter log)
            {
                throw new NotImplementedException();
            }

            public string GetStormLog()
            {
                throw new NotImplementedException();
            }

            public ISubscriber GetSubscriber(object asyncState = null)
            {
                throw new NotImplementedException();
            }

            public int HashSlot(RedisKey key)
            {
                throw new NotImplementedException();
            }

            public long PublishReconfigure(CommandFlags flags = CommandFlags.None)
            {
                throw new NotImplementedException();
            }

            public Task<long> PublishReconfigureAsync(CommandFlags flags = CommandFlags.None)
            {
                throw new NotImplementedException();
            }

            public void RegisterProfiler(Func<ProfilingSession> profilingSessionProvider)
            {
                throw new NotImplementedException();
            }

            public void ResetStormLog()
            {
                throw new NotImplementedException();
            }

            public void Wait(Task task)
            {
                throw new NotImplementedException();
            }

            public T Wait<T>(Task<T> task)
            {
                throw new NotImplementedException();
            }

            public void WaitAll(params Task[] tasks)
            {
                throw new NotImplementedException();
            }
        }
    }
}
