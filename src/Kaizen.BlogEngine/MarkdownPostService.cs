using System.Globalization;
using System.Text.RegularExpressions;
using Markdig;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Kaizen.BlogEngine;

public sealed partial class MarkdownPostService : IPostService
{
    private readonly MarkdownContentOptions _options;
    private readonly string _folder;
    private readonly Lazy<IReadOnlyList<Post>> _posts;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build();

    [GeneratedRegex(@"\A\uFEFF?---[ \t]*\r?\n(?<yaml>.*?)\r?\n---[ \t]*(?:\r?\n|\z)(?<body>.*)\z", RegexOptions.Singleline)]
    private static partial Regex FrontMatterRegex();

    public MarkdownPostService(MarkdownContentOptions options, string contentRoot)
    {
        _options = options;
        _folder = Path.IsPathRooted(options.ContentPath) ? options.ContentPath : Path.Combine(contentRoot, options.ContentPath);
        _posts = new Lazy<IReadOnlyList<Post>>(Load);
    }

    public string UrlPrefix => _options.UrlPrefix;
    public IReadOnlyList<Post> GetAll() => _posts.Value;
    public Post? GetBySlug(string slug) => _posts.Value.FirstOrDefault(p => string.Equals(p.Slug, slug, StringComparison.Ordinal));
    public IReadOnlyList<Post> GetByTag(string tag) =>
        _posts.Value.Where(p => p.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList();
    public IReadOnlyList<string> GetTags() =>
        _posts.Value.SelectMany(p => p.Tags).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();

    private IReadOnlyList<Post> Load()
    {
        if (!Directory.Exists(_folder)) return Array.Empty<Post>();
        var list = new List<Post>();
        foreach (var file in Directory.EnumerateFiles(_folder, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var post = Parse(file);
            if (post.Draft && !_options.IncludeDrafts) continue;
            if (list.Any(p => p.Slug == post.Slug)) throw new InvalidOperationException($"Duplicate post slug '{post.Slug}' ({file}).");
            list.Add(post);
        }
        return list.OrderByDescending(p => p.Date).ThenBy(p => p.Slug, StringComparer.Ordinal).ToList();
    }

    private Post Parse(string file)
    {
        var text = File.ReadAllText(file);
        var m = FrontMatterRegex().Match(text);
        if (!m.Success) throw new InvalidOperationException($"{file}: missing YAML front matter (--- ... ---).");
        var fm = Yaml.Deserialize<FrontMatter>(m.Groups["yaml"].Value) ?? new FrontMatter();
        if (string.IsNullOrWhiteSpace(fm.Title)) throw new InvalidOperationException($"{file}: front matter 'title' is required.");
        if (!TryDate(fm.Date, out var date)) throw new InvalidOperationException($"{file}: front matter 'date' is required (yyyy-MM-dd).");
        TryDate(fm.Updated, out var updated);
        var slug = (string.IsNullOrWhiteSpace(fm.Slug) ? Path.GetFileNameWithoutExtension(file) : fm.Slug!).Trim().ToLowerInvariant();
        if (!Regex.IsMatch(slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$")) throw new InvalidOperationException($"{file}: slug '{slug}' must be lowercase-kebab.");
        return new Post(slug, fm.Title!, date, fm.Updated is null ? null : updated, fm.Description, fm.Author,
            fm.Tags ?? new List<string>(), fm.Draft, Markdown.ToHtml(m.Groups["body"].Value, Pipeline), file)
        { Url = $"{_options.UrlPrefix.TrimEnd('/')}/{slug}/" };
    }

    private static bool TryDate(string? s, out DateTimeOffset d)
    {
        d = default;
        return s is not null && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out d);
    }

    private sealed class FrontMatter
    {
        public string? Title { get; set; }
        public string? Date { get; set; }
        public string? Updated { get; set; }
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string? Slug { get; set; }
        public List<string>? Tags { get; set; }
        public bool Draft { get; set; }
    }
}
