using System;
using System.Collections.Generic;

namespace RBN.Modlib.Persistence;

public static class JsonSettingsUtils
{
    public static string ReadNestedRaw(string json, string key, char open, char close)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(key)) return string.Empty;
        var keyToken = $"\"{key}\"";
        var keyIndex = json.IndexOf(keyToken, StringComparison.OrdinalIgnoreCase);
        if (keyIndex < 0) return string.Empty;
        var colon = json.IndexOf(':', keyIndex + keyToken.Length);
        if (colon < 0) return string.Empty;
        var start = json.IndexOf(open, colon + 1);
        if (start < 0) return string.Empty;

        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var i = start; i < json.Length; i++)
        {
            var c = json[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }
                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == open) depth++;
            else if (c == close)
            {
                depth--;
                if (depth == 0)
                {
                    if (i <= start + 1) return string.Empty;
                    return json.Substring(start + 1, i - start - 1);
                }
            }
        }

        return string.Empty;
    }

    public static List<string> SplitTopLevelObjects(string rawArray)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(rawArray)) return result;

        var start = -1;
        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var i = 0; i < rawArray.Length; i++)
        {
            var c = rawArray[i];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '{')
            {
                if (depth == 0) start = i;
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0 && start >= 0)
                {
                    result.Add(rawArray.Substring(start, i - start + 1));
                    start = -1;
                }
            }
        }

        return result;
    }

    public static string ReadString(string json, string key, string fallback)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            json,
            $"\"{System.Text.RegularExpressions.Regex.Escape(key)}\"\\s*:\\s*\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : fallback;
    }

    public static float ReadFloat(string json, string key, float fallback)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            json,
            $"\"{System.Text.RegularExpressions.Regex.Escape(key)}\"\\s*:\\s*([-0-9\\.Ee]+)");
        if (m.Success &&
            float.TryParse(
                m.Groups[1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value))
        {
            return value;
        }

        return fallback;
    }

    public static int ReadInt(string json, string key, int fallback)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            json,
            $"\"{System.Text.RegularExpressions.Regex.Escape(key)}\"\\s*:\\s*([-0-9]+)");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var value))
        {
            return value;
        }

        return fallback;
    }

    public static bool ReadBool(string json, string key, bool fallback)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            json,
            $"\"{System.Text.RegularExpressions.Regex.Escape(key)}\"\\s*:\\s*(true|false)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success && bool.TryParse(m.Groups[1].Value, out var value))
        {
            return value;
        }

        return fallback;
    }

    public static string ReadArrayRaw(string json, string key)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            json,
            $"\"{System.Text.RegularExpressions.Regex.Escape(key)}\"\\s*:\\s*\\[(.*?)\\]",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        return m.Success ? m.Groups[1].Value : string.Empty;
    }

    public static string[] ParseStringArray(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
        var matches = System.Text.RegularExpressions.Regex.Matches(raw, "\"((?:\\\\.|[^\"])*)\"");
        if (matches.Count == 0) return Array.Empty<string>();

        var values = new List<string>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            var value = matches[i].Groups[1].Value
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
            values.Add(value);
        }

        return values.ToArray();
    }

    public static int[] ParseIntArray(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<int>();
        var matches = System.Text.RegularExpressions.Regex.Matches(raw, "[-0-9]+");
        var values = new List<int>(matches.Count);
        for (var i = 0; i < matches.Count; i++)
        {
            if (int.TryParse(matches[i].Value, out var value))
            {
                values.Add(value);
            }
        }

        return values.ToArray();
    }

    public static string EscapeJson(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value!.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public static string BuildStringArray(string[]? values)
    {
        if (values == null || values.Length == 0) return "[]";
        var lines = new List<string> { "[" };
        for (var i = 0; i < values.Length; i++)
        {
            var comma = i < values.Length - 1 ? "," : string.Empty;
            lines.Add($"    \"{EscapeJson(values[i])}\"{comma}");
        }

        lines.Add("  ]");
        return string.Join(Environment.NewLine, lines);
    }

    public static string BuildIntArray(int[]? values)
    {
        if (values == null || values.Length == 0) return "[]";
        var lines = new List<string> { "[" };
        for (var i = 0; i < values.Length; i++)
        {
            var comma = i < values.Length - 1 ? "," : string.Empty;
            lines.Add($"    {values[i]}{comma}");
        }

        lines.Add("  ]");
        return string.Join(Environment.NewLine, lines);
    }
}
