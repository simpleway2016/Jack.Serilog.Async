using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;

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
    }

    struct MessageItem
    {
        public Serilog.ILogger Logger;
        public LogLevel Level;
        public DateTime Time;
        public string Message;
        public Exception Exception;
    }

    class QueueLogger : Microsoft.Extensions.Logging.ILogger
    {
        static ConcurrentQueue<MessageItem> Queue = new ConcurrentQueue<MessageItem>();
        static AutoResetEvent WaitEvent = new AutoResetEvent(false);
        string _categoryName;
        Serilog.ILogger _logger;

        static QueueLogger()
        {
            new Thread(() => {
                while (true)
                {
                    if (Queue.TryDequeue(out MessageItem item))
                    {
                        var level = (Serilog.Events.LogEventLevel)item.Level;
                        if (item.Exception != null)
                        {
                            item.Logger.Write(level, $"{item.Time.ToString("HH:mm:ss")} {item.Message}\r\n{item.Exception}");
                        }
                        else
                        {
                            item.Logger.Write(level, $"{item.Time.ToString("HH:mm:ss")} {item.Message}");
                        }
                    }
                    else
                    {
                        WaitEvent.WaitOne();
                    }
                }
            }).Start();
        }

        public QueueLogger(string categoryName)
        {
            this._categoryName = categoryName;
            _logger = Serilog.Log.Logger.ForContext("SourceContext", _categoryName);

        }
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return Serilog.Log.Logger.IsEnabled((Serilog.Events.LogEventLevel)logLevel);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                var msg = formatter(state, exception);
                Queue.Enqueue(new MessageItem
                {
                    Level = logLevel,
                    Logger = _logger,
                    Message = state?.ToString(),
                    Time = DateTime.Now,
                    Exception = exception
                });
                WaitEvent.Set();
            }
        }
    }

    class AsyncLoggerFactory : ILoggerFactory
    {
        ILoggerProvider _loggerProvider;
        public AsyncLoggerFactory()
        {
            _loggerProvider = new AsyncLoggerProvider();
        }
        public void AddProvider(ILoggerProvider provider)
        {

        }

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
        {
            return _loggerProvider.CreateLogger(categoryName);
        }

        public void Dispose()
        {

        }
    }

    class AsyncLoggerProvider : Microsoft.Extensions.Logging.ILoggerProvider
    {

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
        {
            return new QueueLogger(categoryName);
        }

        public void Dispose()
        {

        }
    }
}
