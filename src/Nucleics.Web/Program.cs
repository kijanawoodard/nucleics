using Kaizen.BlogEngine;
using Kaizen.Seo;
using Kaizen.StaticSite;
using Nucleics.Web.Components;
using Nucleics.Web.Glue;

const string SiteUrl = "https://nucleics.org";

var builder = WebApplication.CreateBuilder(args);

// The site is the only place the three libraries meet.
builder.AddStaticSite(args, o => o.SiteUrl = SiteUrl);                       // Kaizen.StaticSite
builder.Services.AddMarkdownContent(builder.Configuration["Blog:ContentPath"] ?? "content/posts", urlPrefix: "/blog");    // Kaizen.BlogEngine
builder.Services.AddSeo(o =>                                                 // Kaizen.Seo
{
    o.SiteUrl = SiteUrl;
    o.SiteName = "Nucleics";
    o.Organization = new OrganizationInfo("Nucleics", Logo: "/assets/favicon-64.png",
        Description: "Advocacy for nuclear abundance: 100 MWh of electricity per person per year.");
});
builder.Services.AddSingleton<IStaticRouteSource, BlogRouteSource>();        // glue: /blog/{slug} per published post
builder.Services.AddSingleton<IStaticPageGate, BlogIndexGate>();             // glue: /blog only if there is a post

builder.Services.AddRazorComponents();

var app = builder.Build();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.MapStaticAssets();
app.MapRazorComponents<App>();

return await app.RunStaticSiteAsync(args);
