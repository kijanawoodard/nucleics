using System.Text;

namespace Kaizen.StaticSite;

/// <summary>
/// Copies host config files (Cloudflare Pages <c>_headers</c>, <c>_redirects</c>) byte-for-byte into the output root and
/// WARNS (never fails) when they exceed Cloudflare Pages limits:
///   _headers   : max 100 rules, each line max 2,000 chars.
///   _redirects : max 2,000 static + 100 dynamic (splat '*' or :placeholder) rules, each declaration max 1,000 chars;
///                static rules should come before dynamic ones.
/// Limits per https://developers.cloudflare.com/pages/configuration/headers/ and .../redirects/ (read 2026-10-04).
/// </summary>
public static class PassthroughFiles
{
    public const int HeadersMaxRules = 100, HeadersMaxLine = 2000;
    public const int RedirectsMaxStatic = 2000, RedirectsMaxDynamic = 100, RedirectsMaxLine = 1000;

    /// <returns>Names of files copied.</returns>
    public static List<string> Copy(StaticSiteOptions o, string outRoot, TextWriter log, TextWriter warn)
    {
        var copied = new List<string>();
        if (o.PassthroughDirectory is null) return copied;
        foreach (var name in o.PassthroughFiles)
        {
            var src = Path.Combine(o.PassthroughDirectory, name);
            if (!File.Exists(src)) continue;
            File.Copy(src, Path.Combine(outRoot, name), overwrite: true); // as-is: bytes untouched
            copied.Add(name);
            log.WriteLine($"  copied {name} (as-is)");
            foreach (var w in Validate(name, File.ReadAllText(src, Encoding.UTF8)))
                warn.WriteLine($"WARN {name}: {w}");
        }
        return copied;
    }

    public static IEnumerable<string> Validate(string fileName, string text)
    {
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        if (fileName == "_headers")
        {
            var rules = lines.Count(l => l.Length > 0 && !char.IsWhiteSpace(l[0]) && l[0] != '#'); // URL lines start a rule
            if (rules > HeadersMaxRules) yield return $"{rules} rules exceeds the Cloudflare Pages limit of {HeadersMaxRules}.";
            for (var i = 0; i < lines.Count; i++)
                if (lines[i].Length > HeadersMaxLine) yield return $"line {i + 1} is {lines[i].Length} chars (limit {HeadersMaxLine}).";
        }
        else if (fileName == "_redirects")
        {
            int stat = 0, dyn = 0, lastDynamic = -1, staticAfterDynamic = 0, firstMisordered = 0;
            for (var i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                if (l.Length > RedirectsMaxLine) yield return $"line {i + 1} is {l.Length} chars (limit {RedirectsMaxLine}).";
                var t = l.Trim();
                if (t.Length == 0 || t[0] == '#') continue;
                var source = t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
                var isDynamic = source.Contains('*') || System.Text.RegularExpressions.Regex.IsMatch(source, @":[A-Za-z]");
                if (isDynamic) { dyn++; lastDynamic = i; }
                else
                {
                    stat++;
                    if (lastDynamic >= 0 && staticAfterDynamic++ == 0) firstMisordered = i + 1;
                }
            }
            if (staticAfterDynamic > 0) yield return $"{staticAfterDynamic} static redirect(s) appear after a dynamic one, first at line {firstMisordered} (static rules should come first).";
            if (stat > RedirectsMaxStatic) yield return $"{stat} static redirects exceeds the limit of {RedirectsMaxStatic}.";
            if (dyn > RedirectsMaxDynamic) yield return $"{dyn} dynamic redirects exceeds the limit of {RedirectsMaxDynamic}.";
        }
    }
}
