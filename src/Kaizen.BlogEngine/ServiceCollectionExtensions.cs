using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Kaizen.BlogEngine;

public static class BlogServiceCollectionExtensions
{
    /// <summary>
    /// Registers a markdown <see cref="IContentCollection"/> (folder + url prefix + rules) and the <see cref="ContentCatalog"/> that lists them.
    /// Example: AddContentCollection("pages", "content/pages", urlPrefix: "") and AddContentCollection("blog", "content/posts", "/blog", c => c.Index = true).
    /// </summary>
    public static IServiceCollection AddContentCollection(this IServiceCollection services, string name, string contentPath,
        string urlPrefix, Action<CollectionOptions>? configure = null)
    {
        var o = new CollectionOptions { Name = name, ContentPath = contentPath, UrlPrefix = urlPrefix };
        configure?.Invoke(o);
        services.AddSingleton(o);   // also readable at registration time (ServiceDescriptor.ImplementationInstance) by site glue
        services.AddSingleton<IContentCollection>(sp => new MarkdownCollection(o, sp.GetRequiredService<IHostEnvironment>().ContentRootPath));
        services.TryAddSingleton<ContentCatalog>();
        return services;
    }
}
