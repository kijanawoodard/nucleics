using Kaizen.StaticSite;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaizen.StaticSite.Tests;

public class InventoryTests
{
    private static async Task<RouteInventory> BuildAsync(Action<StaticSiteOptions>? configure = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRazorComponents();
        builder.Services.AddSingleton<IStaticRouteSource, ItemSource>();
        builder.Services.AddSingleton<IStaticPageGate, GateOff>();
        builder.Services.AddSingleton<IStaticPageGate, GateOn>();
        builder.Services.AddSingleton<IStaticPageGate, GateForNothing>();
        var app = builder.Build();
        app.MapRazorComponents<FixtureRoot>(); // discovers [Route] components in the root component's assembly (this test assembly)
        var options = new StaticSiteOptions();
        configure?.Invoke(options);
        return await RouteInventoryBuilder.BuildAsync(app, options, CancellationToken.None);
    }

    [Fact]
    public async Task Public_page_is_exported()
    {
        var inv = await BuildAsync();
        Assert.Contains(inv.Routes, r => r.Path == "/public" && r.Origin == RouteOrigin.Direct);
    }

    [Fact]
    public async Task Authorize_pages_are_excluded_by_default()
    {
        var inv = await BuildAsync();
        Assert.DoesNotContain(inv.Routes, r => r.Path == "/secret");
        Assert.DoesNotContain(inv.Routes, r => r.Path == "/policy-gated");
        var ex = Assert.Single(inv.Excluded, e => e.Template == "/secret");
        Assert.Contains("[Authorize]", ex.Reason);
        Assert.Contains(inv.Excluded, e => e.Template == "/policy-gated");
    }

    [Fact]
    public async Task AllowAnonymous_overrides_Authorize()
    {
        var inv = await BuildAsync();
        Assert.Contains(inv.Routes, r => r.Path == "/anon-override");
    }

    [Fact]
    public async Task Authorize_pages_are_included_when_the_filter_is_off()
    {
        var inv = await BuildAsync(o => o.ExcludeAuthorizedPages = false);
        Assert.Contains(inv.Routes, r => r.Path == "/secret");
        Assert.Contains(inv.Routes, r => r.Path == "/policy-gated");
    }

    [Fact]
    public async Task ExcludeFromStaticExport_attribute_opts_out()
    {
        var inv = await BuildAsync();
        Assert.DoesNotContain(inv.Routes, r => r.Path == "/opted-out");
        Assert.Contains(inv.Excluded, e => e.Template == "/opted-out" && e.Reason.Contains("ExcludeFromStaticExport"));
    }

    [Fact]
    public async Task Parameterised_route_is_expanded_by_its_source_and_warns_without_one()
    {
        var inv = await BuildAsync();
        Assert.Contains(inv.Routes, r => r.Path == "/items/a" && r.Origin == RouteOrigin.Expanded);
        Assert.Contains(inv.Routes, r => r.Path == "/items/b%20c");
        Assert.DoesNotContain(inv.Routes, r => r.Template == "/unsourced/{x}");
        Assert.Contains(inv.Warnings, w => w.Contains("/unsourced/{x}") && w.Contains("no IStaticRouteSource"));
    }

    [Fact]
    public async Task Page_gate_can_switch_a_page_off_and_reports_why()
    {
        var inv = await BuildAsync();
        Assert.DoesNotContain(inv.Routes, r => r.Path == "/gated-off");
        var ex = Assert.Single(inv.Excluded, e => e.Template == "/gated-off");
        Assert.Contains("IStaticPageGate", ex.Reason);
        Assert.Contains("no content", ex.Reason);
        Assert.Contains(inv.Routes, r => r.Path == "/gated-on");
    }

    [Fact]
    public async Task Page_gate_matching_no_page_warns()
    {
        var inv = await BuildAsync();
        Assert.Contains(inv.Warnings, w => w.Contains("IStaticPageGate") && w.Contains("/typo"));
    }

    [Fact]
    public async Task Non_page_endpoints_are_not_pages()
    {
        var inv = await BuildAsync();
        Assert.All(inv.Routes, r => Assert.NotNull(r.Component)); // every exported page came from a component endpoint
    }
}

public class PassthroughLimitTests
{
    [Fact]
    public void Headers_over_100_rules_or_2000_char_lines_warn()
    {
        var text = string.Concat(Enumerable.Range(0, 101).Select(i => $"/p{i}\n  X: {i}\n")) + "/long\n  X: " + new string('x', 2000) + "\n";
        var w = PassthroughFiles.Validate("_headers", text).ToList();
        Assert.Contains(w, m => m.Contains("102 rules"));
        Assert.Contains(w, m => m.Contains("limit 2000"));
    }

    [Fact]
    public void Sane_headers_do_not_warn() =>
        Assert.Empty(PassthroughFiles.Validate("_headers", "/*\n  X-Frame-Options: DENY\n"));

    [Fact]
    public void Redirects_limits_and_ordering_warn()
    {
        var text = "/a/* /b/:splat 301\n" + string.Concat(Enumerable.Range(0, 2001).Select(i => $"/o{i} /n{i} 301\n"))
                   + string.Concat(Enumerable.Range(0, 100).Select(i => $"/d{i}/:id /x/:id 302\n"));
        var w = PassthroughFiles.Validate("_redirects", text).ToList();
        Assert.Contains(w, m => m.Contains("2001 static") && m.Contains("exceeds"));
        Assert.Contains(w, m => m.Contains("101 dynamic"));
        Assert.Contains(w, m => m.Contains("after a dynamic one"));
    }
}
