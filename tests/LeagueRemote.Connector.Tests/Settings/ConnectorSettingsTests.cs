using LeagueRemote.Connector.Lcu;
using LeagueRemote.Connector.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace LeagueRemote.Connector.Tests;

/// <summary>Configuration order with the app's own host builder: code defaults, settings.json, then the command line.</summary>
public sealed class ConnectorSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "league-remote-tests", Guid.NewGuid().ToString("N"));

    public ConnectorSettingsTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string SettingsFile => Path.Combine(_dir, "data", ConnectorSettings.FileName);

    /// <summary>The app's builder with an appsettings.json in its content root that must be ignored.</summary>
    private WebApplicationBuilder Builder(params string[] args)
    {
        File.WriteAllText(Path.Combine(_dir, "appsettings.json"), """{ "Server": { "HttpPort": 1111, "HttpsPort": 1112 } }""");
        return WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = _dir,
            EnvironmentName = Environments.Production,
        });
    }

    private void WriteSettings(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        File.WriteAllText(SettingsFile, json);
    }

    [Fact]
    public void Settings_file_overrides_the_defaults_and_the_command_line_overrides_the_file()
    {
        WriteSettings("""
            {
              "Server": { "HttpPort": 6080, "HttpsPort": 6443 },
              "Logging": { "LogLevel": { "Microsoft.AspNetCore": "Debug" } }
            }
            """);
        var builder = Builder("--Server:HttpsPort=7443");

        Assert.Null(ConnectorSettings.Apply(builder.Configuration, SettingsFile));

        var server = builder.Configuration.GetSection(ServerOptions.Section).Get<ServerOptions>()!;
        Assert.Equal(6080, server.HttpPort);
        Assert.Equal(7443, server.HttpsPort);
        Assert.Equal("Debug", builder.Configuration["Logging:LogLevel:Microsoft.AspNetCore"]);
        Assert.Equal("Warning", builder.Configuration["Logging:LogLevel:System.Net.Http.HttpClient"]);
    }

    [Fact]
    public void Without_a_settings_file_the_built_in_defaults_apply_and_appsettings_is_ignored()
    {
        var builder = Builder();

        Assert.Null(ConnectorSettings.Apply(builder.Configuration, SettingsFile));

        var server = new ServerOptions();
        builder.Configuration.GetSection(ServerOptions.Section).Bind(server);
        Assert.Equal(5080, server.HttpPort);
        Assert.Equal(5443, server.HttpsPort);
        Assert.Equal("Warning", builder.Configuration["Logging:LogLevel:Microsoft.AspNetCore"]);
        Assert.Equal("Information", builder.Configuration["Logging:LogLevel:Microsoft.Hosting.Lifetime"]);
    }

    [Fact]
    public void A_broken_settings_file_is_skipped_with_a_message()
    {
        WriteSettings("""{ "Server": { "HttpPort": 6080 """);
        var builder = Builder();

        var problem = ConnectorSettings.Apply(builder.Configuration, SettingsFile);

        Assert.StartsWith("settings.json has an error", problem);
        Assert.Null(builder.Configuration["Server:HttpPort"]);
        Assert.Equal("Warning", builder.Configuration["Logging:LogLevel:Microsoft.AspNetCore"]);
    }

    [Fact]
    public void The_written_file_holds_the_defaults_and_is_never_overwritten()
    {
        ConnectorSettings.CreateIfMissing(SettingsFile);

        var config = new ConfigurationBuilder().AddJsonFile(SettingsFile).Build();
        Assert.Equal(5080, config.GetValue<int>("Server:HttpPort"));
        Assert.Equal(5443, config.GetValue<int>("Server:HttpsPort"));
        Assert.Equal(new LcuOptions().InstallPath, config["Lcu:InstallPath"]);
        Assert.False(config.GetValue<bool>("Connector:Headless"));

        WriteSettings("""{ "Server": { "HttpPort": 6080 } }""");
        ConnectorSettings.CreateIfMissing(SettingsFile);
        Assert.Contains("6080", File.ReadAllText(SettingsFile));
    }
}
