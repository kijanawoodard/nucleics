using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kaizen.StaticSite;

/// <summary>Spike 1: dumps every endpoint and whether it carries <see cref="ComponentTypeMetadata"/>.</summary>
public static class EndpointReport
{
    public static void Print(WebApplication app, TextWriter @out)
    {
        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(ds => ds.Endpoints).ToList();
        @out.WriteLine($"# all endpoints: {endpoints.Count}");
        foreach (var e in endpoints.OrderBy(e => Describe(e)))
        {
            var cm = e.Metadata.GetMetadata<ComponentTypeMetadata>();
            var kind = e is RouteEndpoint ? "RouteEndpoint" : e.GetType().Name;
            @out.WriteLine($"{(cm is null ? "-" : "C")} {kind,-13} {Describe(e)}  [{e.DisplayName}]");
        }

        var pages = endpoints.OfType<RouteEndpoint>().Where(e => e.Metadata.GetMetadata<ComponentTypeMetadata>() is not null).ToList();
        @out.WriteLine();
        @out.WriteLine($"# filtered (ComponentTypeMetadata present): {pages.Count} of {endpoints.Count}");
        foreach (var p in pages.OrderBy(p => p.RoutePattern.RawText))
        {
            var t = p.Metadata.GetMetadata<ComponentTypeMetadata>()!.Type;
            @out.WriteLine($"  {p.RoutePattern.RawText,-24} -> {t.FullName}");
        }
        var without = endpoints.Where(e => e.Metadata.GetMetadata<ComponentTypeMetadata>() is null).ToList();
        @out.WriteLine($"# without ComponentTypeMetadata: {without.Count} (e.g. static assets, framework endpoints)");
        foreach (var g in without.GroupBy(e => (e as RouteEndpoint) is null ? "non-route" : "route").OrderBy(g => g.Key))
            @out.WriteLine($"  {g.Key}: {g.Count()}");

        // Evidence for classification: which metadata types do the non-page endpoints carry?
        @out.WriteLine("# metadata types on endpoints WITHOUT ComponentTypeMetadata (count of endpoints carrying each):");
        foreach (var grp in without.SelectMany(e => e.Metadata.Select(m => m.GetType().FullName ?? m.GetType().Name).Distinct())
                     .GroupBy(n => n).OrderByDescending(g => g.Count()))
            @out.WriteLine($"  {grp.Count(),3}  {grp.Key}");
        @out.WriteLine("# metadata types on page endpoints:");
        foreach (var grp in pages.SelectMany(e => e.Metadata.Select(m => m.GetType().FullName ?? m.GetType().Name).Distinct())
                     .GroupBy(n => n).OrderByDescending(g => g.Count()))
            @out.WriteLine($"  {grp.Count(),3}  {grp.Key}");
    }

    private static string Describe(Endpoint e) => e is RouteEndpoint re ? re.RoutePattern.RawText ?? "(null)" : "(non-route)";
}
