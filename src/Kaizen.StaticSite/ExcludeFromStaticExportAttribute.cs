namespace Kaizen.StaticSite;

/// <summary>Opt a routable component out of the static export and the sitemap.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ExcludeFromStaticExportAttribute : Attribute
{
    public ExcludeFromStaticExportAttribute(string? reason = null) => Reason = reason;
    public string? Reason { get; }
}
