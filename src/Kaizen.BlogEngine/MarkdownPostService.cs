using System.Globalization;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;

namespace Kaizen.BlogEngine;

public sealed class MarkdownPostService : IPostService
{
    private readonly MarkdownContentOptions _options;
    private readonly string _folder;
    private readonly Lazy<IReadOnlyList<Post>> _posts;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseYamlFrontMatter().Build();

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
        var doc = Markdown.Parse(File.ReadAllText(file), Pipeline);
        var block = doc.Descendants<YamlFrontMatterBlock>().FirstOrDefault();   // Markdig finds the --- ... --- block
        if (block is null) throw new InvalidOperationException($"{file}: missing YAML front matter (--- ... ---).");
        Dictionary<string, object> fm;
        try { fm = FrontMatterParser.Parse(block.Lines.Lines.Take(block.Lines.Count).Select(l => l.ToString()), file); }
        catch (FormatException e) { throw new InvalidOperationException(e.Message, e); }

        var title = FrontMatterParser.GetString(fm, "title");
        if (string.IsNullOrWhiteSpace(title)) throw new InvalidOperationException($"{file}: front matter 'title' is required.");
        var dateText = FrontMatterParser.GetString(fm, "date");
        if (!TryDate(dateText, out var date)) throw new InvalidOperationException($"{file}: front matter 'date' is required (yyyy-MM-dd).");
        var updatedText = FrontMatterParser.GetString(fm, "updated");
        TryDate(updatedText, out var updated);
        var slugText = FrontMatterParser.GetString(fm, "slug");
        var slug = (string.IsNullOrWhiteSpace(slugText) ? Path.GetFileNameWithoutExtension(file) : slugText).Trim().ToLowerInvariant();
        if (!Regex.IsMatch(slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$")) throw new InvalidOperationException($"{file}: slug '{slug}' must be lowercase-kebab.");
        bool draft;
        try { draft = FrontMatterParser.GetBool(fm, "draft"); }
        catch (FormatException e) { throw new InvalidOperationException($"{file}: {e.Message}", e); }

        return new Post(slug, title, date, updatedText is null ? null : updated, FrontMatterParser.GetString(fm, "description"),
            FrontMatterParser.GetString(fm, "author"), FrontMatterParser.GetList(fm, "tags") ?? new List<string>(), draft,
            Markdown.ToHtml(doc, Pipeline), file)
        { Url = $"{_options.UrlPrefix.TrimEnd('/')}/{slug}/" };
    }

    private static bool TryDate(string? s, out DateTimeOffset d)
    {
        d = default;
        return s is not null && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out d);
    }
}
