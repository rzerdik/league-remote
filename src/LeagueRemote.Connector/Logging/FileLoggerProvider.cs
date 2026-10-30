using System.Text;
using System.Threading.Channels;
using LeagueRemote.Connector.Pairing;

namespace LeagueRemote.Connector.Logging;

internal static class FileLoggingExtensions
{
    /// <summary>
    /// Adds the log file. The caller owns <paramref name="provider"/> and disposes it after the host, so the last lines land.
    /// The pairing code stays out of the file: the file outlives the code and gets attached to bug reports.
    /// </summary>
    public static ILoggingBuilder AddFileLog(this ILoggingBuilder builder, FileLoggerProvider provider) => builder
        .AddProvider(provider)
        .AddFilter<FileLoggerProvider>(PairingStatusService.CodeCategory, LogLevel.None);
}

/// <summary>
/// Writes the log to <c>connector.log</c> in a folder, so a tray run (no console) still leaves something to attach to a
/// bug report. When the file would pass <c>maxFileBytes</c> it becomes <c>connector.1.log</c> (older ones shift up) and
/// at most <c>maxFiles</c> files are kept, so the folder stays bounded.
/// Logging never blocks or throws: entries go into a bounded queue (dropped when it is full) and one background task
/// writes them. A file that cannot be written is skipped for a while and the entries are lost.
/// </summary>
internal sealed class FileLoggerProvider : ILoggerProvider
{
    public const string FileName = "connector.log";
    public const long DefaultMaxFileBytes = 2 * 1024 * 1024;
    public const int DefaultMaxFiles = 5;

    /// <summary>A longer entry (a huge exception, say) is cut, so one entry can't fill a file on its own.</summary>
    internal const int MaxEntryChars = 16 * 1024;

    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromSeconds(30);
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly string _directory;
    private readonly long _maxFileBytes;
    private readonly int _maxFiles;
    private readonly TimeProvider _time;
    private readonly Channel<string> _queue = Channel.CreateBounded<string>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
    private readonly Task _writing;
    private StreamWriter? _writer;
    private long _size;
    private DateTimeOffset _retryAt = DateTimeOffset.MinValue;

    public FileLoggerProvider(string directory, TimeProvider? time = null,
        long maxFileBytes = DefaultMaxFileBytes, int maxFiles = DefaultMaxFiles)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFileBytes, 1024);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFiles, 2);
        _directory = directory;
        _maxFileBytes = maxFileBytes;
        _maxFiles = maxFiles;
        _time = time ?? TimeProvider.System;
        _writing = Task.Run(WriteLoopAsync);
    }

    public string CurrentPath => Path.Combine(_directory, FileName);

    public string ArchivePath(int index) => Path.Combine(_directory, $"connector.{index}.log");

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    /// <summary>Stops taking entries and waits briefly for the queue to reach the file.</summary>
    public void Dispose()
    {
        if (!_queue.Writer.TryComplete())
            return;
        try
        {
            _writing.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // The write loop catches its own errors; nothing more to do on the way out.
        }
    }

    private void Enqueue(string entry) => _queue.Writer.TryWrite(entry);

    private async Task WriteLoopAsync()
    {
        var reader = _queue.Reader;
        while (await reader.WaitToReadAsync().ConfigureAwait(false))
        {
            while (reader.TryRead(out var entry))
                Write(entry);
            Flush();
        }

        Close();
    }

    private void Write(string entry)
    {
        if (_writer is null && !TryOpen())
            return;

        try
        {
            var bytes = Utf8.GetByteCount(entry);
            if (_size > 0 && _size + bytes > _maxFileBytes)
            {
                Roll();
                if (!TryOpen())
                    return;
            }

            _writer!.Write(entry);
            _size += bytes;
        }
        catch (Exception)
        {
            // Any I/O trouble (disk full, file locked, folder gone): drop the entry, never take the app down.
            Fail();
        }
    }

    private void Flush()
    {
        try
        {
            _writer?.Flush();
        }
        catch (Exception)
        {
            Fail();
        }
    }

    private bool TryOpen()
    {
        if (_time.GetUtcNow() < _retryAt)
            return false;

        try
        {
            Directory.CreateDirectory(_directory);
            // Shared, so the file can be opened (or the folder cleaned up) while the app runs.
            var stream = new FileStream(CurrentPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            _writer = new StreamWriter(stream, Utf8);
            _size = stream.Length;
            return true;
        }
        catch (Exception)
        {
            Fail();
            return false;
        }
    }

    /// <summary>connector.log → connector.1.log → … → connector.{maxFiles-1}.log; the oldest is overwritten.</summary>
    private void Roll()
    {
        Close();
        for (var i = _maxFiles - 1; i >= 1; i--)
        {
            var from = i == 1 ? CurrentPath : ArchivePath(i - 1);
            if (File.Exists(from))
                File.Move(from, ArchivePath(i), overwrite: true);
        }
    }

    private void Fail()
    {
        Close();
        _retryAt = _time.GetUtcNow() + RetryAfterFailure;
    }

    private void Close()
    {
        try
        {
            _writer?.Dispose();
        }
        catch (Exception)
        {
            // Flushing the last bytes failed; they are lost.
        }

        _writer = null;
        _size = 0;
    }

    private static string LevelName(LogLevel level) => level switch
    {
        LogLevel.Trace => "trce",
        LogLevel.Debug => "dbug",
        LogLevel.Information => "info",
        LogLevel.Warning => "warn",
        LogLevel.Error => "fail",
        LogLevel.Critical => "crit",
        _ => "none",
    };

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        // Levels are filtered by the logging configuration before Log is called.
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var line = new StringBuilder()
                .Append(provider._time.GetLocalNow().ToString("yyyy-MM-dd HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture))
                .Append(' ').Append(LevelName(logLevel))
                .Append(' ').Append(category).Append(": ")
                .Append(formatter(state, exception));
            if (exception is not null)
                line.AppendLine().Append(exception);
            if (line.Length > MaxEntryChars)
                line.Length = MaxEntryChars;
            line.AppendLine();
            provider.Enqueue(line.ToString());
        }
    }
}
