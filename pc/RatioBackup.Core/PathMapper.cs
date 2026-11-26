using System.Text;

namespace RatioBackup.Core;

/// <summary>Übersetzt Handy-Pfade (mit '/') in sichere relative Windows-Pfade.</summary>
public static class PathMapper
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private const string Invalid = "<>:\"\\|?*%";

    public static bool IsSafe(string phonePath)
    {
        if (string.IsNullOrEmpty(phonePath) || phonePath.StartsWith('/') || phonePath.Contains('\\'))
            return false;
        foreach (var part in phonePath.Split('/'))
        {
            if (part.Length == 0 || part == "." || part == "..") return false;
            if (part.Any(c => c < 0x20)) return false;
            if (part.Length >= 2 && part[1] == ':' && char.IsLetter(part[0]) && part == phonePath.Split('/')[0]) return false;
        }
        return true;
    }

    public static string ToWindowsRelative(string phonePath)
    {
        if (!IsSafe(phonePath)) throw new ArgumentException($"Unzulässiger Pfad: {phonePath}", nameof(phonePath));
        return string.Join('\\', phonePath.Split('/').Select(EscapeSegment));
    }

    private static string EscapeSegment(string segment)
    {
        var sb = new StringBuilder(segment.Length);
        foreach (var c in segment)
            sb.Append(Invalid.Contains(c) ? Hex(c) : c.ToString());

        var result = sb.ToString();
        var stem = result.Split('.')[0];
        if (Reserved.Contains(stem))
            result = Hex(result[0]) + result[1..];

        var last = result[^1];
        if (last == '.' || last == ' ')
            result = result[..^1] + Hex(last);
        return result;
    }

    private static string Hex(char c) => "%" + ((int)c).ToString("X2");
}
