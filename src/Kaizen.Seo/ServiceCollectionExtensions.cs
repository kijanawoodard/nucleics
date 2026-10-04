using Microsoft.Extensions.DependencyInjection;

namespace Kaizen.Seo;

public static class SeoServiceCollectionExtensions
{
    public static IServiceCollection AddSeo(this IServiceCollection services, Action<SeoOptions> configure)
    {
        var o = new SeoOptions();
        configure(o);
        o.SiteUrl = o.SiteUrl.TrimEnd('/');
        return services.AddSingleton(o);
    }
}
