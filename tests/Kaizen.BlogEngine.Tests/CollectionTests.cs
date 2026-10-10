using Microsoft.Extensions.DependencyInjection;
using Kaizen.BlogEngine;
using Xunit;

namespace Kaizen.BlogEngine.Tests;

public sealed class CollectionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kaizen-posts-" + Guid.NewGuid().ToString("N"));
    public CollectionTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private IContentCollection Service(string? prefix = null)
    {
        var o = new CollectionOptions { ContentPath = _dir };
        if (prefix is not null) o.UrlPrefix = prefix;
        return new MarkdownCollection(o, _dir);
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
    public void Page_layout_and_updated_are_exposed_and_empty_prefix_gives_top_level_url()
    {
        Write("why.md", "---\ntitle: Why\ndate: 2026-10-01\nupdated: 2026-10-10\nlayout: page\n---\nBody\n");
        var p = Assert.Single(Service("").GetAll());
        Assert.Equal("/why/", p.Url);
        Assert.Equal("page", p.Layout);
        Assert.Equal(new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero), p.Updated);
    }

    [Fact]
    public void Zero_published_posts_is_fine_missing_folder_empty_folder_or_only_drafts()
    {
        var missing = new MarkdownCollection(new CollectionOptions { ContentPath = Path.Combine(_dir, "nope") }, _dir);
        Assert.Empty(missing.GetAll());
        Assert.Empty(Service().GetAll()); // empty folder
        Write("wip.md", "---\ntitle: WIP\ndate: 2026-10-02\ndraft: true\n---\nx");
        var svc = Service();
        Assert.Empty(svc.GetAll());
        Assert.Empty(svc.GetTags());
        Assert.Null(svc.GetBySlug("wip"));
    }

    [Fact]
    public void Drafts_are_hidden_unless_included_and_posts_sort_newest_first()
    {
        Write("old.md", "---\ntitle: Old\ndate: 2026-01-01\n---\nx");
        Write("new.md", "---\ntitle: New\ndate: 2026-05-01\n---\nx");
        Write("wip.md", "---\ntitle: WIP\ndate: 2026-09-01\ndraft: true\n---\nx");
        Assert.Equal(new[] { "new", "old" }, Service().GetAll().Select(p => p.Slug));
        var withDrafts = new MarkdownCollection(new CollectionOptions { ContentPath = _dir, IncludeDrafts = true }, _dir);
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
    [InlineData("no front matter at all", "missing or empty YAML front matter")]
    [InlineData("---\ndate: 2026-01-01\n---\nx", "'title' is required")]
    [InlineData("---\ntitle: T\n---\nx", "'date' is required")]
    [InlineData("---\ntitle: T\ndate: 2026-01-01\nslug: Bad Slug!\n---\nx", "lowercase-kebab")]
    [InlineData("---\ntitle: T\ndate: 2026-01-01\nnested:\n  a: 1\n---\nx", "invalid front matter")]
    [InlineData("---\n---\nx", "missing or empty YAML front matter")]                                  // empty front matter
    [InlineData("---\ntitle: T\ndate: not-a-date\n---\nx", "'date' is required")]
    [InlineData("---\ntitle: T\ndate: 2026-01-01\nupdated: soon\n---\nx", "'updated' is not a date")]
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
    [Fact]
    public void Collection_name_is_stamped_on_pages_and_template_and_index_follow_the_prefix()
    {
        Write("p.md", "---\ntitle: P\ndate: 2026-01-01\n---\nx");
        var blog = new MarkdownCollection(new CollectionOptions { Name = "blog", ContentPath = _dir, UrlPrefix = "/blog", Index = true }, _dir);
        var pages = new MarkdownCollection(new CollectionOptions { Name = "pages", ContentPath = _dir, UrlPrefix = "" }, _dir);
        Assert.Equal("blog", Assert.Single(blog.GetAll()).Collection);
        Assert.Equal("/blog/{slug}", blog.UrlTemplate());
        Assert.Equal("/blog", blog.IndexPath());
        Assert.Equal("/{slug}", pages.UrlTemplate());
        Assert.Null(pages.IndexPath());
        Assert.Equal("/p/", pages.GetBySlug("p")!.Url);
    }

    [Fact]
    public void LastModified_prefers_updated_and_falls_back_to_date()
    {
        Write("a.md", "---\ntitle: A\ndate: 2026-10-01\nupdated: 2026-10-10\n---\nx");
        Write("b.md", "---\ntitle: B\ndate: 2026-09-01\n---\nx");
        var c = Service("");
        Assert.Equal(new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero), c.GetBySlug("a")!.LastModified());
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), c.GetBySlug("b")!.LastModified());
    }

    [Fact]
    public void Catalog_registers_collections_by_name_and_hides_drafts_per_collection()
    {
        var pagesDir = Path.Combine(_dir, "pages"); var postsDir = Path.Combine(_dir, "posts");
        Directory.CreateDirectory(pagesDir); Directory.CreateDirectory(postsDir);
        File.WriteAllText(Path.Combine(pagesDir, "why.md"), "---\ntitle: Why\ndate: 2026-10-01\nlayout: page\n---\nx");
        File.WriteAllText(Path.Combine(postsDir, "wip.md"), "---\ntitle: WIP\ndate: 2026-10-01\ndraft: true\n---\nx");
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Hosting.IHostEnvironment>(new FakeEnv { ContentRootPath = _dir });
        services.AddContentCollection("pages", "pages", urlPrefix: "");
        services.AddContentCollection("blog", "posts", "/blog", c => c.Index = true);
        var catalog = services.BuildServiceProvider().GetRequiredService<ContentCatalog>();
        Assert.Equal(new[] { "pages", "blog" }, catalog.Collections.Select(c => c.Name));
        Assert.Equal("/why/", Assert.Single(catalog["pages"].GetAll()).Url);
        Assert.Empty(catalog["blog"].GetAll());
        Assert.True(catalog["blog"].Options.Index);
        Assert.Throws<KeyNotFoundException>(() => catalog["nope"]);
    }

    private sealed class FakeEnv : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

public sealed class PostServiceErrorLocationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kaizen-posts-err-" + Guid.NewGuid().ToString("N"));
    public PostServiceErrorLocationTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private IContentCollection Service() => new MarkdownCollection(new CollectionOptions { ContentPath = _dir }, _dir);

    [Fact]
    public void Malformed_yaml_names_the_file_and_the_line_in_the_file()
    {
        var path = Path.Combine(_dir, "broken.md");
        File.WriteAllText(path, "---\ntitle: T\ndate: 2026-01-01\ndratf: true\n---\nbody");   // fence on line 1, bad key on line 4
        var ex = Assert.Throws<InvalidOperationException>(() => Service().GetAll());
        Assert.Contains(path + ", line 4", ex.Message);
        Assert.Contains("dratf", ex.Message);
    }

    [Fact]
    public void Valid_shapes_end_to_end_dates_tags_multiline_and_draft()
    {
        File.WriteAllText(Path.Combine(_dir, "full.md"),
            "---\ntitle: \"Quoted: title\"\ndate: 2026-10-01T23:30:00Z\nupdated: 2026-10-02\ndescription: >\n  folded over\n  two lines\nauthor: A\ntags:\n  - one\n  - two\ndraft: false\n---\n\nBody **text**");
        File.WriteAllText(Path.Combine(_dir, "wip.md"), "---\ntitle: WIP\ndate: 2026-10-03\ntags: [x]\ndraft: true\n---\nz");
        var s = Service();
        var p = Assert.Single(s.GetAll());
        Assert.Equal("Quoted: title", p.Title);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 23, 30, 0, TimeSpan.Zero), p.Date);
        Assert.Equal(new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero), p.Updated);
        Assert.Equal("folded over two lines", p.Description);
        Assert.Equal(new[] { "one", "two" }, p.Tags);
        Assert.Contains("<strong>text</strong>", p.Html);
    }

}
