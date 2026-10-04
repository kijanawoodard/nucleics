using Kaizen.BlogEngine;
using Xunit;

namespace Kaizen.BlogEngine.Tests;

public class FrontMatterParserTests
{
    private static FrontMatter P(string text, string source = "post.md", int fenceLine = 1) => FrontMatterParser.Parse(text, source, fenceLine);

    [Fact]
    public void Parses_the_sample_post_shape()
    {
        var d = P("title: Why nuclear? Start with energy density\ndate: 2026-10-01\nupdated: 2026-10-03\n" +
                  "description: A handful of uranium pellets powers a household for years. Density is the whole argument in one number.\n" +
                  "author: Kijana Woodard\ntags: [nuclear, energy-density, abundance]\ndraft: false");
        Assert.Equal("Why nuclear? Start with energy density", d.Title);
        Assert.Equal("2026-10-01", d.Date);
        Assert.Equal("2026-10-03", d.Updated);
        Assert.StartsWith("A handful of uranium pellets", d.Description);
        Assert.Equal("Kijana Woodard", d.Author);
        Assert.Equal(new[] { "nuclear", "energy-density", "abundance" }, d.Tags);
        Assert.False(d.Draft);
    }

    [Fact] public void Draft_true_is_read() => Assert.True(P("draft: true").Draft);
    [Fact] public void Missing_draft_is_false() => Assert.False(P("title: x").Draft);
    [Fact] public void Bad_bool_is_rejected_with_the_file_and_line() =>
        AssertError("title: x\ndraft: maybe", "post.md, line 3", "maybe");

    [Fact]
    public void Quoted_scalars_keep_colons_hashes_and_quotes()
    {
        var d = P("title: \"Fission: a primer # not a comment\"\ndescription: 'It''s quiet'\nauthor: K # trailing comment");
        Assert.Equal("Fission: a primer # not a comment", d.Title);
        Assert.Equal("It's quiet", d.Description);
        Assert.Equal("K", d.Author);
    }

    [Fact]
    public void Double_quoted_escapes_are_honoured() => Assert.Equal("a\"b\\c\nd", P("title: \"a\\\"b\\\\c\\nd\"").Title);

    [Fact]
    public void Tags_inline_and_dash_list_and_quoted_items()
    {
        Assert.Equal(new[] { "a", "b, c" }, P("tags:\n  - a\n  - \"b, c\"").Tags);
        Assert.Equal(new[] { "x", "y, z", "w" }, P("tags: [x, \"y, z\", 'w']").Tags);
        Assert.Equal(new[] { "a", "b" }, P("tags:\n- a\n- b").Tags);   // unindented dash list is valid YAML
        Assert.Empty(P("tags: []").Tags);
        Assert.Empty(P("title: x").Tags);
    }

    [Theory]
    [InlineData("date: 2026-10-01", "2026-10-01")]
    [InlineData("date: \"2026-10-01\"", "2026-10-01")]
    [InlineData("date: 2026-10-01T09:30:00Z", "2026-10-01T09:30:00Z")]
    [InlineData("date: 2026-10-01 09:30:00", "2026-10-01 09:30:00")]
    [InlineData("date: 2026-10-01T09:30:00-05:00", "2026-10-01T09:30:00-05:00")]
    public void Dates_are_kept_as_text_so_nothing_shifts_time_zones(string yaml, string expected) => Assert.Equal(expected, P(yaml).Date);

    [Fact]
    public void Multi_line_literal_and_folded_values()
    {
        var d = P("description: |\n  line one\n  line two\nauthor: >\n  folded\n  text\ntitle: after");
        Assert.Equal("line one\nline two\n", d.Description);
        Assert.Equal("folded text\n", d.Author);
        Assert.Equal("after", d.Title);
    }

    [Fact] public void Comments_and_blank_lines_are_ignored() => Assert.Equal("v", P("# c\n\ntitle: v\n").Title);
    [Fact] public void Colon_without_space_stays_in_the_value() => Assert.Equal("http://x.y", P("title: http://x.y").Title);
    [Fact] public void Empty_front_matter_is_an_empty_model() { Assert.Null(P("").Title); Assert.Null(P("  \n \n").Title); Assert.Null(P("# only a comment").Title); }

    [Fact]
    public void Unknown_keys_are_an_error_naming_file_and_line_never_ignored()
    {
        var ex = AssertError("title: x\ndratf: true", "post.md, line 3", "dratf");
        Assert.IsAssignableFrom<YamlDotNet.Core.YamlException>(ex.InnerException);
    }

    [Fact] public void Keys_are_case_sensitive_camelCase() => AssertError("Title: v", "post.md, line 2", "Title");

    [Fact] public void Line_numbers_are_file_lines_using_the_fence_line() =>
        AssertError("title: ok\nbad: 1", "x/y.md, line 13", "bad", source: "x/y.md", fenceLine: 11);

    [Theory]
    [InlineData("a: |\n  text")]          // unknown key (and multi-line) - still an error
    [InlineData("title: [x, y")]          // unterminated flow sequence
    [InlineData("title: \"open")]         // unterminated quote
    [InlineData("title: x\ntitle: y")]    // duplicate key
    [InlineData("title: x\n  bad: indent")] // bad indentation
    [InlineData("just text")]             // not key: value
    [InlineData("tags: single")]          // scalar where a list is required
    [InlineData("tags:\n  nested: map")]  // map where a list is required
    [InlineData("title: {a: 1}")]         // map where text is required
    [InlineData("draft: [1]")]            // list where a bool is required
    public void Malformed_or_wrong_typed_yaml_is_rejected_loudly(string text)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => P(text, "docs/p.md"));
        Assert.StartsWith("docs/p.md", ex.Message);
        Assert.Contains("invalid front matter", ex.Message);
    }

    private static InvalidOperationException AssertError(string yaml, string where, string mention, string source = "post.md", int fenceLine = 1)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => P(yaml, source, fenceLine));
        Assert.Contains(where, ex.Message);
        Assert.Contains(mention, ex.Message);
        return ex;
    }
}
