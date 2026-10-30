namespace LeagueRemote.Contracts;

/// <summary>A phone's bearer token for this PC's pairing. The phone keeps it to sign in again with Authenticate.</summary>
public sealed record PairingGrant(string PairingId, string Token);
