using Kaizen.StaticSite;
using Nucleics.Web.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents();

var app = builder.Build();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>();

if (args.Contains("routes")) { await app.StartAsync(); EndpointReport.Print(app, Console.Out); await app.StopAsync(); return; }
app.Run();
