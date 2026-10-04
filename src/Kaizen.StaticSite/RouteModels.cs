namespace Kaizen.StaticSite;

public enum RouteOrigin { Direct, Expanded, Extra }

/// <summary>A concrete URL path that will be exported.</summary>
public sealed record StaticRoute(string Path, string Template, RouteOrigin Origin, Type? Component);

/// <summary>A page endpoint that was discovered but deliberately not exported.</summary>
public sealed record ExcludedRoute(string Template, string Reason, Type? Component);

/// <summary>A static asset endpoint (fingerprinted or plain) that gets materialised into the output.</summary>
public sealed record AssetRoute(string Route, string? AssetPath);

public sealed class RouteInventory
{
    public List<StaticRoute> Routes { get; } = new();
    public List<ExcludedRoute> Excluded { get; } = new();
    public List<AssetRoute> Assets { get; } = new();
    public List<string> Warnings { get; } = new();

    public IReadOnlySet<string> RoutePaths =>
        new HashSet<string>(Routes.Select(r => r.Path), StringComparer.OrdinalIgnoreCase);
}
