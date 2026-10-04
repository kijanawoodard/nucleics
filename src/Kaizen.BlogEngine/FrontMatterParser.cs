using System.Globalization;

namespace Kaizen.BlogEngine;

/// <summary>
/// Deliberately tiny parser for the FLAT subset of YAML that post front matter uses:
///   key: scalar            (plain, 'single' or "double" quoted; # comments after whitespace are ignored)
///   key: [a, b, "c d"]     (inline list)
///   key:                   (block list on the following lines)
///     - a
///     - b
///   key: true|false        (read with <see cref="GetBool"/>)
/// Anything else (nested maps, multi-line scalars | and >, anchors, tags, flow maps) is rejected with a clear error
/// rather than silently misread. Markdig's YamlFrontMatter extension finds the block; this class reads its lines.
/// </summary>
public static class FrontMatterParser
{
    public static Dictionary<string, object> Parse(IEnumerable<string> lines, string source = "front matter")
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        string? openList = null;
        var n = 0;
        foreach (var raw in lines)
        {
            n++;
            var line = raw.TrimEnd('\r', '\n');
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

            if (char.IsWhiteSpace(line[0]) || trimmed.StartsWith("- ") || trimmed == "-")
            {
                // only valid as an item of a block list opened by a bare "key:"
                if (openList is null || !(trimmed.StartsWith("- ") || trimmed == "-"))
                    throw new FormatException($"{source}, line {n}: unsupported YAML (indentation / nesting): '{trimmed}'. Only flat key: value pairs and simple lists are supported.");
                ((List<string>)result[openList]).Add(Scalar(trimmed.Length > 1 ? trimmed[2..] : "", source, n));
                continue;
            }

            openList = null;
            var colon = FindKeyColon(line);
            if (colon <= 0) throw new FormatException($"{source}, line {n}: expected 'key: value', got '{trimmed}'.");
            var key = line[..colon].Trim();
            var value = StripComment(line[(colon + 1)..]).Trim();
            if (result.ContainsKey(key)) throw new FormatException($"{source}, line {n}: duplicate key '{key}'.");

            if (value.Length == 0) { result[key] = new List<string>(); openList = key; }   // "key:" opens a block list; stays empty if no items follow
            else if (value[0] == '[') result[key] = InlineList(value, source, n);
            else if (value[0] is '{' or '|' or '>' or '&' or '*' or '!') throw new FormatException($"{source}, line {n}: unsupported YAML value '{value}'.");
            else result[key] = Scalar(value, source, n);
        }
        return result;
    }

    public static string? GetString(Dictionary<string, object> d, string key) =>
        d.TryGetValue(key, out var v) ? v switch { string s => s, List<string> l when l.Count == 0 => null, _ => throw new FormatException($"'{key}' must be a single value.") } : null;

    public static List<string>? GetList(Dictionary<string, object> d, string key) =>
        d.TryGetValue(key, out var v) ? v switch { List<string> l => l, string s => new List<string> { s }, _ => null } : null;

    public static bool GetBool(Dictionary<string, object> d, string key)
    {
        var s = GetString(d, key);
        if (s is null) return false;
        return s.ToLowerInvariant() switch
        {
            "true" => true,
            "false" => false,
            _ => throw new FormatException($"'{key}' must be true or false, got '{s}'."),
        };
    }

    private static int FindKeyColon(string line)
    {
        for (var i = 0; i < line.Length; i++)
            if (line[i] == ':' && (i + 1 == line.Length || char.IsWhiteSpace(line[i + 1]))) return i;
        return -1;
    }

    private static string StripComment(string s)
    {
        char? quote = null;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (quote is null && (c == '"' || c == '\'') && s[..i].Trim().Length == 0) quote = c;
            else if (quote == c) quote = null;
            else if (quote is null && c == '#' && (i == 0 || char.IsWhiteSpace(s[i - 1]))) return s[..i];
        }
        return s;
    }

    private static string Scalar(string s, string source, int line)
    {
        s = StripComment(s).Trim();
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
            return s[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n").Replace("\\t", "\t");
        if (s.Length >= 2 && s[0] == '\'' && s[^1] == '\'') return s[1..^1].Replace("''", "'");
        if (s.Length > 0 && (s[0] == '"' || s[0] == '\'')) throw new FormatException($"{source}, line {line}: unterminated quote in '{s}'.");
        return s;
    }

    private static List<string> InlineList(string value, string source, int line)
    {
        if (value[^1] != ']') throw new FormatException($"{source}, line {line}: inline list must end with ']' on the same line: '{value}'.");
        var inner = value[1..^1];
        var items = new List<string>();
        var cur = new System.Text.StringBuilder();
        char? quote = null;
        foreach (var c in inner)
        {
            if (quote is null && (c == '"' || c == '\'')) { quote = c; cur.Append(c); }
            else if (quote == c) { quote = null; cur.Append(c); }
            else if (quote is null && c == ',') { Flush(); }
            else cur.Append(c);
        }
        Flush();
        return items;

        void Flush()
        {
            var t = cur.ToString().Trim(); cur.Clear();
            if (t.Length > 0) items.Add(Scalar(t, source, line));
        }
    }
}
