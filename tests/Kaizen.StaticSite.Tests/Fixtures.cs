using Kaizen.StaticSite;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Kaizen.StaticSite.Tests;

// Fixture components: each [Route] becomes a Razor-component page endpoint carrying its attributes as endpoint metadata.
public abstract class FixturePage : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, GetType().Name);
}

[Route("/public")] public sealed class PublicPage : FixturePage { }
[Authorize] [Route("/secret")] public sealed class SecretPage : FixturePage { }
[Authorize(Policy = "Admins")] [Route("/policy-gated")] public sealed class PolicyGatedPage : FixturePage { }
[Authorize] [AllowAnonymous] [Route("/anon-override")] public sealed class AnonOverridePage : FixturePage { }
[ExcludeFromStaticExport("tests")] [Route("/opted-out")] public sealed class OptedOutPage : FixturePage { }
[Route("/items/{id}")] public sealed class ItemPage : FixturePage { [Parameter] public string Id { get; set; } = ""; }
[Route("/unsourced/{x}")] public sealed class UnsourcedPage : FixturePage { [Parameter] public string X { get; set; } = ""; }

public sealed class FixtureRoot : ComponentBase { }

public sealed class ItemSource : IStaticRouteSource
{
    public string Template => "/items/{id}";
    public async IAsyncEnumerable<IReadOnlyDictionary<string, string>> GetRouteValuesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new Dictionary<string, string> { ["id"] = "a" };
        yield return new Dictionary<string, string> { ["id"] = "b c" };
        await Task.CompletedTask;
    }
}
