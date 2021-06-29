using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serilog
{
    public static class JackSerilogExtens
    {

        static IConfiguration Configuration;
        static ILoggingBuilder LoggingBuilder;
        static void ConfigurationChangeCallback(object p)
        {
            Configuration.GetReloadToken().RegisterChangeCallback(ConfigurationChangeCallback, null);

            string minimumLevel = Configuration["Serilog:MinimumLevel:Default"];
            var level = (LogLevel)Enum.Parse<Serilog.Events.LogEventLevel>(minimumLevel);
            LoggingBuilder.SetMinimumLevel(level);
        }

        /// <summary>
        /// 使用Serilog异步日志
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configuration"></param>
        public static void UseSerilogAsyncLogger(this ILoggingBuilder builder, IConfiguration configuration)
        {
            Configuration = configuration;

            LoggingBuilder = builder;
            ConfigurationChangeCallback(null);

            builder.AddProvider(new AsyncLoggerProvider());
        }

        /// <summary>
        /// 使用Serilog异步日志
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IHostBuilder UseSerilogAsyncLogger(this IHostBuilder builder)
        {
            builder.ConfigureServices(services => {
                services.AddSingleton<ILoggerFactory, AsyncLoggerFactory>();
            });
            return builder;
        }

        /// <summary>
        /// 允许网页实时查看日志（考虑中）
        /// </summary>
        /// <param name="app"></param>
        static void UseWebLogViewer(IApplicationBuilder app)
        {
            app.UseWebSockets();
            app.Use((context, next) => {
                if (context.WebSockets.IsWebSocketRequest && context.Request.Query.ContainsKey("JackSerilogAsyncWebLogViewer"))
                {
                    //do something 
                    var t = context.WebSockets.AcceptWebSocketAsync();
                    t.Wait();
                    WebSocket webSocket = t.Result;
                    return ProcessWebSocketRequest(webSocket, context);
                }

                return next();
            });
        }

        static async Task ProcessWebSocketRequest(WebSocket socket, HttpContext context)
        {
            QueueLogger.WebSocketList.TryAdd(socket, true);
            var bs = new byte[2048];
            while (true)
            {
                if (socket.State == WebSocketState.Open)
                {
                    try
                    {
                        ArraySegment<byte> buffer = new ArraySegment<byte>(bs);
                        WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, CancellationToken.None);

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            
                            break;

                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }
                else
                {
                    break;
                }
            }

            QueueLogger.WebSocketList.TryRemove(socket, out bool o);
        }
    }
}
