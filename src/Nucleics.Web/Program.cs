using Kaizen.StaticSite;
using Nucleics.Web.Components;

var builder = WebApplication.CreateBuilder(args);
builder.AddStaticSite(args, o => o.SiteUrl = "https://nucleics.org");
builder.Services.AddRazorComponents();

var app = builder.Build();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>();

return await app.RunStaticSiteAsync(args);
