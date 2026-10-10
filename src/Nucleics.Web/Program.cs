using Kaizen.BlogEngine;
using Kaizen.Seo;
using Kaizen.StaticSite;
using Nucleics.Web.Components;
using Nucleics.Web.Glue;

const string SiteUrl = "https://nucleics.org";

var builder = WebApplication.CreateBuilder(args);

// The site is the only place the three libraries meet.
builder.AddStaticSite(args, o => o.SiteUrl = SiteUrl);                       // Kaizen.StaticSite
builder.Services.AddContentCollection("pages", builder.Configuration["Pages:ContentPath"] ?? "content/pages", urlPrefix: "");   // Kaizen.BlogEngine: /{slug}/
builder.Services.AddContentCollection("blog", builder.Configuration["Blog:ContentPath"] ?? "content/posts", urlPrefix: "/blog",   //                   /blog/{slug}/
    c => { c.Index = true; c.Tags = true; });
builder.Services.AddSeo(o =>                                                 // Kaizen.Seo
{
    o.SiteUrl = SiteUrl;
    o.SiteName = "Nucleics";
    o.Organization = new OrganizationInfo("Nucleics", Logo: "/assets/favicon-64.png",
        Description: "Advocacy for nuclear abundance: 100 MWh of electricity per person per year.");
});
builder.Services.AddCollectionRoutes();                                      // glue: a route (+lastmod) per published page; index gate for /blog

builder.Services.AddRazorComponents();

var app = builder.Build();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.MapStaticAssets();
app.MapRazorComponents<App>();

return await app.RunStaticSiteAsync(args);
