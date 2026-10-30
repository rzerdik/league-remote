using System.Text;

namespace LeagueRemote.Connector.Lcu;

/// <summary>Connection details of a running League client (from the lockfile or the LeagueClientUx command line).</summary>
internal sealed record LcuCredentials(int Port, string Password, string Protocol, int Pid)
{
    /// <summary>The LCU only ever listens on loopback; we never use any other host.</summary>
    public const string Host = "127.0.0.1";

    public Uri BaseUri => new($"https://{Host}:{Port}/");

    public Uri WebSocketUri => new($"wss://{Host}:{Port}/");

    /// <summary>Value for <c>Authorization: Basic ...</c> (user is always "riot").</summary>
    public string BasicAuthParameter => Convert.ToBase64String(Encoding.ASCII.GetBytes($"riot:{Password}"));

    // Keep the password out of logs.
    public override string ToString() => $"LcuCredentials {{ Port = {Port}, Pid = {Pid} }}";
}
