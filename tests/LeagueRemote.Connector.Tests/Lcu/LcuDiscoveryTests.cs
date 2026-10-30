using LeagueRemote.Connector.Lcu;

namespace LeagueRemote.Connector.Tests;

/// <summary>Finding the client's port and password: the lockfile and the client process's command line.</summary>
public sealed class LcuDiscoveryTests
{
    [Fact]
    public void Lockfile_valid_is_parsed()
    {
        var credentials = LockfileParser.Parse("LeagueClient:12345:54321:s3cr3t-Token_x:https\r\n");

        Assert.Equal(new LcuCredentials(54321, "s3cr3t-Token_x", "https", 12345), credentials);
        Assert.Equal(new Uri("https://127.0.0.1:54321/"), credentials!.BaseUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("LeagueClient:12345:54321:pw")]          // missing protocol
    [InlineData("LeagueClient:abc:54321:pw:https")]      // non-numeric pid
    [InlineData("LeagueClient:12345:99999:pw:https")]    // port out of range
    [InlineData("LeagueClient:12345:54321::https")]      // empty password
    public void Lockfile_malformed_returns_null(string content) =>
        Assert.Null(LockfileParser.Parse(content));

    [Fact]
    public void Process_args_are_parsed_from_quoted_command_line()
    {
        const string commandLine =
            "\"C:/Riot Games/League of Legends/LeagueClientUx.exe\" \"--riotclient-auth-token=other\" " +
            "\"--remoting-auth-token=AbC-123_xyz\" \"--app-port=61234\" \"--install-directory=C:/Riot Games/League of Legends\"";

        var credentials = ProcessArgsParser.Parse(commandLine);

        Assert.NotNull(credentials);
        Assert.Equal(61234, credentials.Port);
        Assert.Equal("AbC-123_xyz", credentials.Password);
    }

    [Fact]
    public void Process_args_without_port_return_null() =>
        Assert.Null(ProcessArgsParser.Parse("LeagueClientUx.exe --remoting-auth-token=abc"));
}
