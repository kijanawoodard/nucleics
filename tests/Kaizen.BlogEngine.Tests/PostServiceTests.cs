using Kaizen.BlogEngine;
using Xunit;

namespace Kaizen.BlogEngine.Tests;

public sealed class PostServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kaizen-posts-" + Guid.NewGuid().ToString("N"));
    public PostServiceTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private IPostService Service(string? prefix = null)
    {
        var o = new MarkdownContentOptions { ContentPath = _dir };
        if (prefix is not null) o.UrlPrefix = prefix;
        return new MarkdownPostService(o, _dir);
    }
    private void Write(string name, string text) => File.WriteAllText(Path.Combine(_dir, name), text);

    [Fact]
    public void Renders_markdown_body_without_the_front_matter_block()
    {
        Write("hello.md", "---\ntitle: Hello\ndate: 2026-10-01\ndescription: d\ntags: [a, b]\n---\n\n# Heading\n\nSome **bold** text.\n");
        var p = Assert.Single(Service().GetAll());
        Assert.Equal("hello", p.Slug);
        Assert.Equal("/blog/hello/", p.Url);
        Assert.Equal("Hello", p.Title);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), p.Date);
        Assert.Contains("<strong>bold</strong>", p.Html);
        Assert.Contains("<h1 id=\"heading\">Heading</h1>", p.Html);
        Assert.DoesNotContain("title:", p.Html);
        Assert.DoesNotContain("---", p.Html);
        Assert.Equal(new[] { "a", "b" }, p.Tags);
    }

    [Fact]
    public void Drafts_are_hidden_unless_included_and_posts_sort_newest_first()
    {
        Write("old.md", "---\ntitle: Old\ndate: 2026-01-01\n---\nx");
        Write("new.md", "---\ntitle: New\ndate: 2026-05-01\n---\nx");
        Write("wip.md", "---\ntitle: WIP\ndate: 2026-09-01\ndraft: true\n---\nx");
        Assert.Equal(new[] { "new", "old" }, Service().GetAll().Select(p => p.Slug));
        var withDrafts = new MarkdownPostService(new MarkdownContentOptions { ContentPath = _dir, IncludeDrafts = true }, _dir);
        Assert.Equal(new[] { "wip", "new", "old" }, withDrafts.GetAll().Select(p => p.Slug));
    }

    [Fact]
    public void Slug_override_tags_and_lookup()
    {
        Write("file-name.md", "---\ntitle: T\ndate: 2026-01-01\nslug: Custom-Slug\ntags:\n  - x\n  - y\n---\nbody");
        var s = Service("/notes");
        Assert.NotNull(s.GetBySlug("custom-slug"));
        Assert.Null(s.GetBySlug("file-name"));
        Assert.Equal("/notes/custom-slug/", s.GetBySlug("custom-slug")!.Url);
        Assert.Single(s.GetByTag("X"));
        Assert.Equal(new[] { "x", "y" }, s.GetTags());
    }

    [Theory]
    [InlineData("no front matter at all", "missing YAML front matter")]
    [InlineData("---\ndate: 2026-01-01\n---\nx", "'title' is required")]
    [InlineData("---\ntitle: T\n---\nx", "'date' is required")]
    [InlineData("---\ntitle: T\ndate: 2026-01-01\nslug: Bad Slug!\n---\nx", "lowercase-kebab")]
    [InlineData("---\ntitle: T\ndate: 2026-01-01\nnested:\n  a: 1\n---\nx", "unsupported YAML")]
    public void Invalid_posts_fail_loudly(string text, string expectedMessage)
    {
        Write("bad.md", text);
        var ex = Assert.Throws<InvalidOperationException>(() => Service().GetAll());
        Assert.Contains(expectedMessage, ex.Message);
    }

    [Fact]
    public void Duplicate_slugs_fail()
    {
        Write("a.md", "---\ntitle: A\ndate: 2026-01-01\nslug: same\n---\nx");
        Write("b.md", "---\ntitle: B\ndate: 2026-01-01\nslug: same\n---\nx");
        Assert.Throws<InvalidOperationException>(() => Service().GetAll());
    }
}
