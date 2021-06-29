using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace Serilog
{
   
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
                        string msg;
                        if (item.Exception != null)
                        {
                            msg = $"{item.Time.ToString("HH:mm:ss")} {item.Message}\r\n{item.Exception}";
                        }
                        else
                        {
                            msg = $"{item.Time.ToString("HH:mm:ss")} {item.Message}";
                        }
                        item.Logger.Write(level, msg);

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
