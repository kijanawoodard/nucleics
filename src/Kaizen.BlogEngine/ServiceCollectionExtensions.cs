using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaizen.BlogEngine;

public static class BlogServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IPostService"/> over a folder of markdown files with YAML front matter.</summary>
    public static IServiceCollection AddMarkdownContent(this IServiceCollection services, string contentPath = "content/posts",
        string urlPrefix = "/blog", Action<MarkdownContentOptions>? configure = null)
    {
        var o = new MarkdownContentOptions { ContentPath = contentPath, UrlPrefix = urlPrefix };
        configure?.Invoke(o);
        return services.AddSingleton<IPostService>(sp => new MarkdownPostService(o, sp.GetRequiredService<IHostEnvironment>().ContentRootPath));
    }
}
