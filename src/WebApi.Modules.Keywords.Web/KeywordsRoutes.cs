using Carter;
using Concepts = soft.square.net.WebApi.Modules.Keywords.Web.Concepts;

namespace soft.square.net.WebApi.Modules.Keywords.Web;

/// <summary>
/// The module's route table, and its only Carter module (the runtime maps the Carter modules of the entry assembly,
/// under <c>/api/keywords</c>). Every endpoint is listed here once, in the order of the OpenAPI document. A new
/// endpoint: add its file under the resource folder, then one line here.
/// </summary>
public sealed class KeywordsRoutes : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Problem details for domain rules and lost races, on the module's own group (module code, unloads with it).
        var concepts = app.MapGroup("/concepts").WithTags("Concepts").AddEndpointFilter<KeywordsProblemsFilter>();

        Concepts.List.Map(concepts);
        Concepts.Lookup.Map(concepts);
        Concepts.Match.Map(concepts);
        Concepts.Types.Map(concepts);
        Concepts.Get.Map(concepts);
        Concepts.JsonLd.Map(concepts);
        Concepts.Create.Map(concepts);
        Concepts.Edit.Map(concepts);
        Concepts.Retire.Map(concepts);
        Concepts.Restore.Map(concepts);
    }
}
