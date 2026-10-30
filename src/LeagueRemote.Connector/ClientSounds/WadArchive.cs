using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.Hashing;
using System.Text;

namespace LeagueRemote.Connector.ClientSounds;

/// <summary>
/// Minimal read-only reader for Riot's WAD archives (version 3.x, as used by the League client's <c>Plugins/*/assets.wad</c>).
/// Layout: <c>"RW"</c>, major, minor, 256-byte signature, 8-byte checksum, uint32 entry count, then 32-byte entries
/// (uint64 xxHash64 of the lower-case path, uint32 offset, uint32 stored size, uint32 size, byte type (low nibble:
/// 0 raw, 1 gzip, 2 link, 3 zstd, 4 chunked zstd), byte, uint16, uint64 checksum). Only raw and gzip entries can be read
/// (the client stores its .ogg sounds raw); others return null.
/// </summary>
internal static class WadArchive
{
    private const int HeaderSize = 4 + 256 + 8 + 4;
    private const int EntrySize = 32;
    private const int MaxEntries = 1_000_000;
    private const int MaxEntryBytes = 32 * 1024 * 1024;

    /// <summary>Where an entry sits in the archive and how it is stored.</summary>
    public readonly record struct Entry(uint Offset, uint StoredSize, uint Size, int Type);

    public static ulong HashPath(string path) => XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(path.ToLowerInvariant()));

    /// <summary>
    /// The archive's table of contents by path hash. A plugin WAD lists thousands of entries, so callers that read several
    /// sounds from one archive read it once.
    /// </summary>
    /// <exception cref="InvalidDataException">Not a WAD v3 file.</exception>
    public static IReadOnlyDictionary<ulong, Entry> ReadToc(string wadPath)
    {
        using var file = Open(wadPath);

        Span<byte> header = stackalloc byte[HeaderSize];
        file.ReadExactly(header);
        if (header[0] != (byte)'R' || header[1] != (byte)'W' || header[2] != 3)
            throw new InvalidDataException($"{wadPath} is not a WAD v3 archive.");
        var count = BinaryPrimitives.ReadUInt32LittleEndian(header[^4..]);
        if (count > MaxEntries)
            throw new InvalidDataException($"{wadPath}: implausible entry count {count}.");

        var toc = new byte[count * EntrySize];
        file.ReadExactly(toc);
        var entries = new Dictionary<ulong, Entry>((int)count);
        for (var i = 0; i < count; i++)
        {
            var entry = toc.AsSpan(i * EntrySize, EntrySize);
            entries.TryAdd(
                BinaryPrimitives.ReadUInt64LittleEndian(entry),
                new Entry(
                    BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]),
                    BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]),
                    BinaryPrimitives.ReadUInt32LittleEndian(entry[16..]),
                    entry[20] & 0x0F));
        }

        return entries;
    }

    /// <summary>The entry's bytes, or null when the archive has no such entry or it is stored in an unsupported way.</summary>
    /// <exception cref="InvalidDataException">Not a WAD v3 file.</exception>
    public static byte[]? TryRead(string wadPath, string entryPath) => TryRead(wadPath, ReadToc(wadPath), entryPath);

    /// <summary>As <see cref="TryRead(string, string)"/>, with a table of contents from <see cref="ReadToc"/>.</summary>
    public static byte[]? TryRead(string wadPath, IReadOnlyDictionary<ulong, Entry> toc, string entryPath)
    {
        if (!toc.TryGetValue(HashPath(entryPath), out var entry))
            return null;

        using var file = Open(wadPath);
        if (entry.StoredSize > MaxEntryBytes || entry.Size > MaxEntryBytes || entry.Offset + (long)entry.StoredSize > file.Length)
            return null;

        var stored = new byte[entry.StoredSize];
        file.Position = entry.Offset;
        file.ReadExactly(stored);
        return entry.Type switch
        {
            0 => stored,
            1 => Gunzip(stored, (int)entry.Size),
            _ => null,
        };
    }

    private static FileStream Open(string wadPath) =>
        new(wadPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static byte[] Gunzip(byte[] data, int size)
    {
        using var input = new GZipStream(new MemoryStream(data), CompressionMode.Decompress);
        var output = new byte[size];
        input.ReadExactly(output);
        return output;
    }
}
