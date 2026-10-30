using LeagueRemote.Connector.Tray;

namespace LeagueRemote.Connector.Tests;

public sealed class PrivateNetworkFirewallTests
{
    private const string Exe = @"C:\Apps\LeagueRemote.exe";
    private static readonly int[] Ports = [5080, 5443];

    private static FirewallRule Rule(string? ports = "5080,5443", int profiles = 2, string? program = null, int protocol = 6,
        bool enabled = true, bool inbound = true, bool allow = true) =>
        new(enabled, inbound, allow, protocol, ports, profiles, program);

    [Theory]
    [InlineData("*", 5080, true)]
    [InlineData(null, 5080, true)]
    [InlineData("5080,5443", 5443, true)]
    [InlineData("80, 5443", 5443, true)]
    [InlineData("5000-6000", 5443, true)]
    [InlineData("80,443", 5080, false)]
    [InlineData("6000-7000", 5443, false)]
    [InlineData("RPC", 5080, false)]
    public void Local_ports_cover_single_ports_lists_and_ranges(string? ports, int port, bool covered) =>
        Assert.Equal(covered, PrivateNetworkFirewall.Covers(ports, port));

    [Fact]
    public void A_port_rule_or_the_exe_rule_from_windows_prompt_on_the_private_profile_lets_phones_in()
    {
        Assert.True(PrivateNetworkFirewall.Allows([Rule()], Ports, Exe));
        Assert.True(PrivateNetworkFirewall.Allows([Rule(profiles: 0x7FFFFFFF)], Ports, Exe));
        Assert.True(PrivateNetworkFirewall.Allows([Rule("5080"), Rule("5443")], Ports, Exe));
        Assert.True(PrivateNetworkFirewall.Allows([Rule("*", program: @"c:\apps\leagueremote.exe")], Ports, Exe));
        Assert.True(PrivateNetworkFirewall.Allows([Rule("*", protocol: 256)], Ports, Exe));
    }

    [Fact]
    public void Rules_that_dont_open_both_ports_for_this_exe_on_private_networks_dont_count()
    {
        Assert.False(PrivateNetworkFirewall.Allows([], Ports, Exe));
        Assert.False(PrivateNetworkFirewall.Allows([Rule("5080")], Ports, Exe));
        Assert.False(PrivateNetworkFirewall.Allows([Rule(profiles: 4)], Ports, Exe));
        Assert.False(PrivateNetworkFirewall.Allows([Rule("*", program: @"C:\Old\LeagueRemote.exe")], Ports, Exe));
        Assert.False(PrivateNetworkFirewall.Allows([Rule(protocol: 17)], Ports, Exe));
        Assert.False(PrivateNetworkFirewall.Allows([Rule(enabled: false)], Ports, Exe));
        Assert.False(PrivateNetworkFirewall.Allows([Rule(inbound: false)], Ports, Exe));
        Assert.False(PrivateNetworkFirewall.Allows([Rule(allow: false)], Ports, Exe));
    }

    [Fact]
    public void The_elevated_command_adds_one_private_inbound_tcp_rule_for_the_ports()
    {
        var start = PrivateNetworkFirewall.AddRuleCommand([5443, 5080, 5443, 0]);

        Assert.Equal(Path.Combine(Environment.SystemDirectory, "netsh.exe"), start.FileName);
        Assert.Equal(
            "advfirewall firewall add rule name=\"League Remote\" dir=in action=allow protocol=TCP localport=5080,5443 profile=private",
            start.Arguments);
        Assert.Equal("runas", start.Verb);
        Assert.True(start.UseShellExecute);
    }

    /// <summary>
    /// Reads this PC's real firewall rules (read-only, no admin) to check the COM access: <c>set LEAGUEREMOTE_READ_FIREWALL=1</c>
    /// and run this test. Does nothing when the variable is not set.
    /// </summary>
    [Fact]
    public void Reads_the_real_rules_when_asked()
    {
        if (Environment.GetEnvironmentVariable("LEAGUEREMOTE_READ_FIREWALL") != "1")
            return;

        var rules = PrivateNetworkFirewall.ReadRules();
        Assert.NotNull(rules);
        Assert.NotEmpty(rules);
        Assert.Contains(rules, r => r.Inbound && r.Allow);
    }
}
