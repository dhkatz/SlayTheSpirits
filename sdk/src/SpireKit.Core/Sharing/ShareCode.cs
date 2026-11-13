using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text;

namespace SpireKit.Sharing;

/// <summary>
/// Compact, copy-pasteable text codes for sharing data between players (Discord, chat):
/// <c>PREFIX:base64url(deflate(utf8(payload)))</c>.
/// </summary>
public static class ShareCode
{
    /// <summary>Encodes <paramref name="payload"/> behind <paramref name="prefix"/> (letters/digits, no ':').</summary>
    public static string Encode(string prefix, string payload)
    {
        ValidatePrefix(prefix);
        ArgumentNullException.ThrowIfNull(payload);
        using var buffer = new MemoryStream();
        using (var deflate = new DeflateStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(Encoding.UTF8.GetBytes(payload));
        }

        return $"{prefix}:{ToBase64Url(buffer.ToArray())}";
    }

    /// <summary>Decodes a code produced by <see cref="Encode"/>. Whitespace around the code is ignored.</summary>
    public static bool TryDecode(string prefix, string? code, [NotNullWhen(true)] out string? payload)
    {
        ValidatePrefix(prefix);
        payload = null;
        var trimmed = code?.Trim();
        if (trimmed is null || !trimmed.StartsWith(prefix + ":", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var compressed = FromBase64Url(trimmed[(prefix.Length + 1)..]);
            using var input = new DeflateStream(new MemoryStream(compressed), CompressionMode.Decompress);
            using var output = new MemoryStream();
            input.CopyTo(output);
            payload = Encoding.UTF8.GetString(output.ToArray());
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException)
        {
            return false;
        }
    }

    private static void ValidatePrefix(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        if (!prefix.All(char.IsAsciiLetterOrDigit))
        {
            throw new ArgumentException("Prefix must be ASCII letters/digits.", nameof(prefix));
        }
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        s = (s.Length % 4) switch
        {
            2 => s + "==",
            3 => s + "=",
            _ => s,
        };
        return Convert.FromBase64String(s);
    }
}
