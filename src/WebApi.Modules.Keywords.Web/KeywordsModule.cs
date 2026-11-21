using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using soft.square.net.WebApi.Contracts;
using soft.square.net.WebApi.Modules.Keywords.Web.Configurations;

namespace soft.square.net.WebApi.Modules.Keywords.Web;

/// <summary>
/// The module: its manifest (id, name, version, route prefix, default settings, permissions) and its services.
/// This is the composition root (the Ardalis template's Program.cs): the runtime creates it, reads the manifest and
/// calls <see cref="ConfigureServices"/> on the module's own container; the endpoints are in
/// <see cref="KeywordsRoutes"/>, mapped under <c>/api/keywords</c>. Tenant data, schema "keywords".
/// </summary>
public sealed class KeywordsModule : IRuntimeModule
{
    public ModuleManifest Manifest { get; } = new("keywords", "Keywords", "0.1.0", "keywords")
    {
        Description = "Keywords: the concepts content is about (people, organizations, places, topics), with names per language, aliases and sameAs links, as schema.org JSON-LD.",
        DefaultSettings = new Dictionary<string, string?> { ["Keywords:MaxPageSize"] = "100" },
        Permissions = KeywordsPermissions.All
    };

    public void ConfigureServices(IServiceCollection services, IConfiguration settings) => services.AddServiceConfigs(settings);
}
