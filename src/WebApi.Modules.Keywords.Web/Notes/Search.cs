using soft.square.net.WebApi.Modules.Keywords.UseCases.Notes;
using soft.square.net.WebApi.Modules.Keywords.UseCases.Notes.Search;

namespace soft.square.net.WebApi.Modules.Keywords.Web.Notes;

/// <summary>GET /notes/search?text=&amp;page=&amp;pageSize=: notes whose title or body contains the text, ignoring case.</summary>
public sealed class Search : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder notes) =>
        notes.MapGet("/search", HandleAsync)
            .RequirePermission(KeywordsPermissions.Read)
            .WithName("Keywords_SearchNotes")
            .WithSummary("Notes whose title or body contains the text (at least 2 characters), ignoring case.");

    private static async Task<Results<Ok<NotePage>, ProblemHttpResult>> HandleAsync(string text, ISender sender, CancellationToken ct, int page = 1, int pageSize = 20) =>
        (await sender.Send(new SearchNotesQuery(text, page, pageSize), ct)).ToOkResult();
}
