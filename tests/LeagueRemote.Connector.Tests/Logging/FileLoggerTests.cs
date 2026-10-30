using LeagueRemote.Connector.Logging;
using LeagueRemote.Connector.Pairing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace LeagueRemote.Connector.Tests;

public sealed class FileLoggerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "league-remote-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 30, 14, 3, 12, 345, TimeSpan.Zero));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private string Logs => Path.Combine(_dir, "logs");

    private FileLoggerProvider Provider(long maxFileBytes = FileLoggerProvider.DefaultMaxFileBytes, int maxFiles = 3, string? directory = null) =>
        new(directory ?? Logs, _time, maxFileBytes, maxFiles);

    [Fact]
    public void Entries_reach_the_file_with_time_level_and_category()
    {
        var provider = Provider();
        var logger = provider.CreateLogger("Test.Category");
        logger.LogInformation("Connected after {Count} tries", 3);
        logger.LogWarning(new InvalidOperationException("boom"), "Something failed");
        provider.Dispose();

        var text = File.ReadAllText(provider.CurrentPath);
        Assert.Contains("2026-10-30 14:03:12.345 info Test.Category: Connected after 3 tries", text);
        Assert.Contains("2026-10-30 14:03:12.345 warn Test.Category: Something failed", text);
        Assert.Contains("System.InvalidOperationException: boom", text);
    }

    [Fact]
    public void A_full_file_rolls_over_and_only_the_newest_files_are_kept()
    {
        var provider = Provider(maxFileBytes: 1024, maxFiles: 3);
        var logger = provider.CreateLogger("T");
        for (var i = 0; i < 200; i++)
            logger.LogInformation("Entry {Number:D4} {Padding}", i, new string('x', 60));
        provider.Dispose();

        Assert.Equal(
            ["connector.1.log", "connector.2.log", "connector.log"],
            Directory.GetFiles(Logs).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        var oldestFirst = new[] { provider.ArchivePath(2), provider.ArchivePath(1), provider.CurrentPath };
        Assert.All(oldestFirst, path => Assert.InRange(new FileInfo(path).Length, 1, 1024));

        // Entries stay in order across the files, the newest is last, the oldest are gone.
        var numbers = oldestFirst.SelectMany(File.ReadAllLines)
            .Select(line => int.Parse(line.Split("Entry ")[1][..4], System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
        Assert.Equal(199, numbers[^1]);
        Assert.Equal(Enumerable.Range(numbers[0], numbers.Count), numbers);
        Assert.True(numbers[0] > 0);
    }

    [Fact]
    public void A_restart_appends_to_the_current_file()
    {
        var first = Provider();
        first.CreateLogger("T").LogInformation("first run");
        first.Dispose();

        var second = Provider();
        second.CreateLogger("T").LogInformation("second run");
        second.Dispose();

        var lines = File.ReadAllLines(second.CurrentPath);
        Assert.Equal(2, lines.Length);
        Assert.EndsWith("first run", lines[0]);
        Assert.EndsWith("second run", lines[1]);
    }

    [Fact]
    public void A_huge_entry_is_cut()
    {
        var provider = Provider();
        provider.CreateLogger("T").LogError("{Text}", new string('y', 100_000));
        provider.Dispose();

        Assert.InRange(new FileInfo(provider.CurrentPath).Length, 1, FileLoggerProvider.MaxEntryChars + 2);
    }

    [Fact]
    public void An_unwritable_folder_never_throws()
    {
        // A file where the folder should be: the folder can't be created.
        Directory.CreateDirectory(_dir);
        var blocked = Path.Combine(_dir, "not-a-folder");
        File.WriteAllText(blocked, "");

        var provider = Provider(directory: blocked);
        var logger = provider.CreateLogger("T");
        for (var i = 0; i < 100; i++)
            logger.LogInformation("lost {Number}", i);
        provider.Dispose();
        provider.Dispose();
        logger.LogInformation("after dispose");

        Assert.Equal("", File.ReadAllText(blocked));
    }

    [Fact]
    public void The_pairing_code_stays_out_of_the_file()
    {
        var provider = Provider();
        using (var factory = LoggerFactory.Create(b => b.AddFileLog(provider)))
        {
            factory.CreateLogger(PairingStatusService.CodeCategory).LogInformation("Pairing code {Code}", "123456");
            factory.CreateLogger<PairingStatusService>().LogInformation("Phones open {AppUrl}", "https://192.168.1.2:5443/");
        }

        provider.Dispose();

        var text = File.ReadAllText(provider.CurrentPath);
        Assert.DoesNotContain("123456", text);
        Assert.Contains("Phones open https://192.168.1.2:5443/", text);
    }
}
