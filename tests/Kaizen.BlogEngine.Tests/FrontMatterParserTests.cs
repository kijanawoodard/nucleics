using Kaizen.BlogEngine;
using Xunit;

namespace Kaizen.BlogEngine.Tests;

public class FrontMatterParserTests
{
    private static Dictionary<string, object> P(string text) => FrontMatterParser.Parse(text.Split('\n'));

    [Fact]
    public void Parses_the_sample_post_shape()
    {
        var d = P("title: Why nuclear? Start with energy density\ndate: 2026-10-01\nupdated: 2026-10-03\n" +
                  "description: A handful of uranium pellets powers a household for years. Density is the whole argument in one number.\n" +
                  "author: Kijana Woodard\ntags: [nuclear, energy-density, abundance]\ndraft: false");
        Assert.Equal("Why nuclear? Start with energy density", FrontMatterParser.GetString(d, "title"));
        Assert.Equal("2026-10-01", FrontMatterParser.GetString(d, "date"));
        Assert.StartsWith("A handful of uranium pellets", FrontMatterParser.GetString(d, "description"));
        Assert.Equal(new[] { "nuclear", "energy-density", "abundance" }, FrontMatterParser.GetList(d, "tags"));
        Assert.False(FrontMatterParser.GetBool(d, "draft"));
    }

    [Fact] public void Draft_true_is_read() => Assert.True(FrontMatterParser.GetBool(P("draft: true"), "draft"));
    [Fact] public void Missing_draft_is_false() => Assert.False(FrontMatterParser.GetBool(P("title: x"), "draft"));

    [Fact]
    public void Quoted_scalars_keep_colons_hashes_and_quotes()
    {
        var d = P("title: \"Fission: a primer # not a comment\"\ndescription: 'It''s quiet'\nauthor: K # trailing comment");
        Assert.Equal("Fission: a primer # not a comment", FrontMatterParser.GetString(d, "title"));
        Assert.Equal("It's quiet", FrontMatterParser.GetString(d, "description"));
        Assert.Equal("K", FrontMatterParser.GetString(d, "author"));
    }

    [Fact]
    public void Block_and_inline_lists_and_quoted_items()
    {
        var d = P("tags:\n  - a\n  - \"b, c\"\nmore: [x, \"y, z\", 'w']\nempty: []");
        Assert.Equal(new[] { "a", "b, c" }, FrontMatterParser.GetList(d, "tags"));
        Assert.Equal(new[] { "x", "y, z", "w" }, FrontMatterParser.GetList(d, "more"));
        Assert.Empty(FrontMatterParser.GetList(d, "empty")!);
    }

    [Fact] public void Comments_and_blank_lines_are_ignored() =>
        Assert.Equal("v", FrontMatterParser.GetString(P("# c\n\nk: v\n"), "k"));

    [Fact] public void Keys_are_case_insensitive() => Assert.Equal("v", FrontMatterParser.GetString(P("Title: v"), "title"));
    [Fact] public void Colon_without_space_stays_in_the_value() => Assert.Equal("http://x.y", FrontMatterParser.GetString(P("u: http://x.y"), "u"));

    [Theory]
    [InlineData("a:\n  b: 1")]            // nested map
    [InlineData("a: |\n  text")]          // block scalar
    [InlineData("a: {b: 1}")]             // flow map
    [InlineData("a: &x 1")]               // anchor
    [InlineData("just text")]             // not key: value
    [InlineData("a: 1\na: 2")]            // duplicate key
    [InlineData("a: \"open")]             // unterminated quote
    [InlineData("a: [x, y")]              // unterminated list
    public void Unsupported_yaml_is_rejected_loudly(string text) => Assert.Throws<FormatException>(() => P(text));

    [Fact] public void Bad_bool_is_rejected() => Assert.Throws<FormatException>(() => FrontMatterParser.GetBool(P("draft: maybe"), "draft"));
}
