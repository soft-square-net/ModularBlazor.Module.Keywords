using soft.square.net.WebApi.Modules.Keywords.UseCases.Notes.Delete;

namespace soft.square.net.WebApi.Modules.Keywords.Web.Notes;

/// <summary>DELETE /notes/{id}?version=: delete a note at the version the caller read; 204.</summary>
public sealed class Delete : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder notes) =>
        notes.MapDelete("/{id:int}", HandleAsync)
            .RequirePermission(KeywordsPermissions.Write)
            .WithName("Keywords_DeleteNote");

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(int id, int version, ISender sender, CancellationToken ct) =>
        (await sender.Send(new DeleteNoteCommand(id, version), ct)).ToNoContentResult();
}
