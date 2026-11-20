using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Mark.TaskbarFolders.Services;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private const long MaxLogBytes = 1024 * 1024;

    private readonly string _path;
    private readonly object _sync = new();
    private long _size;
    private bool _initialized;

    public FileLoggerProvider(string path)
    {
        _path = path;
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
    }

    private void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _size = File.Exists(_path) ? new FileInfo(_path).Length : 0;
        if (_size > MaxLogBytes)
        {
            RotateIfNeeded();
            _size = 0;
        }
    }

    private void Write(string line)
    {
        lock (_sync)
        {
            try
            {
                EnsureInitialized();
                var payload = line + Environment.NewLine;
                var length = Encoding.UTF8.GetByteCount(payload);
                if (_size + length > MaxLogBytes)
                {
                    RotateIfNeeded();
                    _size = 0;
                }

                File.AppendAllText(_path, payload);
                _size += length;
            }
            catch
            {
                // Logging must never take down the application.
            }
        }
    }

    private void RotateIfNeeded()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return;
            }

            var backup = _path + ".1";
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }

            File.Move(_path, backup);
        }
        catch
        {
            // Rotation is best effort; keep logging to the existing file.
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _category;

        public FileLogger(FileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {_category}: {message}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            _provider.Write(line);
        }
    }
}
