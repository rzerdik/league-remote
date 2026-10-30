using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using LeagueRemote.Connector.ClientSounds;

namespace LeagueRemote.Connector.Tests;

/// <summary>Writes WAD v3 archives in the client's layout, so no Riot file is ever needed (or committed) for tests.</summary>
internal static class FakeWad
{
    private static readonly byte[] OggBytes = [.. "OggS"u8, 0, 2, .. Encoding.ASCII.GetBytes("fake ogg payload")];

    /// <summary>A minimal Ogg file (a fresh copy each time).</summary>
    public static byte[] Ogg => [.. OggBytes];

    /// <param name="path">Where to write the archive.</param>
    /// <param name="entries">(path, data, gzip)</param>
    public static void Write(string path, params (string Path, byte[] Data, bool Gzip)[] entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = File.Create(path);
        var header = new byte[4 + 256 + 8 + 4];
        header[0] = (byte)'R';
        header[1] = (byte)'W';
        header[2] = 3;
        header[3] = 4;
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(^4), (uint)entries.Length);
        file.Write(header);

        var offset = header.Length + (entries.Length * 32);
        var blobs = new List<byte[]>();
        foreach (var (entryPath, data, gzip) in entries)
        {
            var stored = gzip ? Gzip(data) : data;
            var entry = new byte[32];
            BinaryPrimitives.WriteUInt64LittleEndian(entry, WadArchive.HashPath(entryPath));
            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(8), (uint)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(12), (uint)stored.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(16), (uint)data.Length);
            entry[20] = (byte)(gzip ? 1 : 0);
            file.Write(entry);
            blobs.Add(stored);
            offset += stored.Length;
        }

        foreach (var blob in blobs)
            file.Write(blob);
    }

    private static byte[] Gzip(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Fastest))
            gz.Write(data);
        return ms.ToArray();
    }
}
