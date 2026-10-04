using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaizen.StaticSite;

public static class StaticSiteExtensions
{
    /// <summary>Commands understood by <see cref="RunStaticSiteAsync"/>.</summary>
    public static bool IsStaticCommand(string[] args) =>
        args.Any(a => a is "export" or "--static-export" or "check" or "routes");

    /// <summary>Registers options. Quiet logging + static web assets when running an export/check command.</summary>
    public static WebApplicationBuilder AddStaticSite(this WebApplicationBuilder builder, string[] args, Action<StaticSiteOptions>? configure = null)
    {
        var options = new StaticSiteOptions();
        configure?.Invoke(options);
        var i = Array.IndexOf(args, "--output");
        if (i >= 0 && i + 1 < args.Length) options.OutputPath = args[i + 1];
        if (!Path.IsPathRooted(options.OutputPath))
            options.OutputPath = Path.Combine(FindRepoRoot(builder.Environment.ContentRootPath), options.OutputPath);
        if (args.Contains("--include-auth")) options.ExcludeAuthorizedPages = false; // demo/diagnostics: export runs anonymous, expect failures
        if (args.Contains("--keep-framework")) options.KeepFramework = true;
        options.PassthroughDirectory ??= FindRepoRoot(builder.Environment.ContentRootPath);
        builder.Services.AddSingleton(options);
        if (IsStaticCommand(args))
        {
            // Deterministic output: a launch profile (Development) must not change what gets exported. `--environment X` still wins.
            if (!args.Contains("--environment") && !args.Any(a => a.StartsWith("--environment=")))
                builder.Environment.EnvironmentName = Environments.Production;
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
            builder.WebHost.UseStaticWebAssets(); // harmless when manifest is already wired; needed outside Development
        }
        return builder;
    }

    /// <summary>`dotnet run --project` sets cwd to the project folder, so a relative OutputPath is anchored at the repo/solution root.</summary>
    private static string FindRepoRoot(string start)
    {
        for (var d = new DirectoryInfo(start); d is not null; d = d.Parent)
            if (d.EnumerateFiles("*.sln").Any() || d.EnumerateFiles("*.slnx").Any() || Directory.Exists(Path.Combine(d.FullName, ".git")))
                return d.FullName;
        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Replaces app.Run(): `export` / `--static-export` writes the site, `check` validates it, `routes` prints the endpoint report;
    /// anything else runs the normal server untouched.
    /// </summary>
    public static async Task<int> RunStaticSiteAsync(this WebApplication app, string[] args)
    {
        var options = app.Services.GetRequiredService<StaticSiteOptions>();
        try { return await RunCoreAsync(app, args, options); }
        catch (Exception ex) when (IsStaticCommand(args) && ex is not OperationCanceledException)
        {
            // Any unexpected failure while exporting/checking must be a deterministic non-zero exit (CI / Cloudflare build fails).
            Console.Error.WriteLine($"[kaizen] FAILED with an unhandled exception: {ex}");
            return 1;
        }
    }

    private static async Task<int> RunCoreAsync(WebApplication app, string[] args, StaticSiteOptions options)
    {
        if (args.Contains("routes"))
        {
            app.Urls.Clear(); app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();
            EndpointReport.Print(app, Console.Out);
            await app.StopAsync();
            return 0;
        }
        if (args.Contains("check"))
            return await new StaticSiteExporter(app, options, Console.Out).RunAsync(StaticSiteMode.Check);
        if (args.Contains("export") || args.Contains("--static-export"))
            return await new StaticSiteExporter(app, options, Console.Out).RunAsync(StaticSiteMode.Export);

        await app.RunAsync();
        return 0;
    }
}
