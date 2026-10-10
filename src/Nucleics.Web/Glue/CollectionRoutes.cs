using Kaizen.BlogEngine;
using Kaizen.StaticSite;

namespace Nucleics.Web.Glue;

/// <summary>
/// The glue: adapts content collections (Kaizen.BlogEngine) to the exporter's contracts (Kaizen.StaticSite).
/// The two libraries know nothing about each other; only the site references both.
/// </summary>
public static class CollectionRoutes
{
    /// <summary>
    /// For every collection registered so far (call after AddContentCollection): one exported route per published page (with lastmod),
    /// plus an index gate for collections that have an index.
    /// </summary>
    public static IServiceCollection AddCollectionRoutes(this IServiceCollection services)
    {
        var all = services.Where(d => d.ServiceType == typeof(CollectionOptions)).Select(d => (CollectionOptions)d.ImplementationInstance!).ToList();
        foreach (var o in all)
        {
            var name = o.Name;
            services.AddSingleton<IStaticRouteSource>(sp => new CollectionRouteSource(sp.GetRequiredService<ContentCatalog>()[name]));
            if (o.Index)
                services.AddSingleton<IStaticPageGate>(sp => new CollectionIndexGate(sp.GetRequiredService<ContentCatalog>()[name]));
        }
        return services;
    }
}

/// <summary>Expands a collection's "{prefix}/{slug}" template to one route per published page; drafts are already hidden.</summary>
public sealed class CollectionRouteSource(IContentCollection collection) : IStaticRouteSource
{
    public string Template => collection.UrlTemplate();

    public async IAsyncEnumerable<IReadOnlyDictionary<string, string>> GetRouteValuesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var p in collection.GetAll())
            yield return new Dictionary<string, string> { ["slug"] = p.Slug };
        await Task.CompletedTask;
    }

    public DateTimeOffset? GetLastModified(IReadOnlyDictionary<string, string> routeValues) =>
        collection.GetBySlug(routeValues["slug"])?.LastModified();
}

/// <summary>
/// A collection index (e.g. /blog) exists only while the collection has at least one published page, so the site ships no empty index
/// and no dangling links to it.
/// </summary>
public sealed class CollectionIndexGate(IContentCollection collection) : IStaticPageGate
{
    public string Template => collection.IndexPath() ?? throw new InvalidOperationException($"Collection '{collection.Name}' has no index.");

    public bool ShouldExport(out string? reason)
    {
        reason = collection.GetAll().Count == 0 ? $"no published {collection.Name} pages" : null;
        return reason is null;
    }
}
