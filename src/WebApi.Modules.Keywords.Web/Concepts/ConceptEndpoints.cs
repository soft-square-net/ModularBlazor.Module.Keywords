using Microsoft.AspNetCore.Mvc;
using soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate;
using soft.square.net.WebApi.Modules.Keywords.UseCases.Concepts;

namespace soft.square.net.WebApi.Modules.Keywords.Web.Concepts;

/// <summary>GET /concepts?page=&amp;pageSize=&amp;search=&amp;type=&amp;language=&amp;includeRetired=: one page of concepts by name.</summary>
public sealed class List : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder concepts) =>
        concepts.MapGet("/", HandleAsync)
            .RequirePermission(KeywordsPermissions.Read)
            .WithName("Keywords_ListConcepts")
            .WithSummary("One page of concepts by name; search matches any label (case, accents and Arabic letter forms ignored) or the slug.");

    private static async Task<Results<Ok<ConceptPage>, ProblemHttpResult>> HandleAsync(ISender sender, CancellationToken ct,
        int page = 1, int pageSize = 20, string? search = null, string? type = null, string? language = null, bool includeRetired = false) =>
        (await sender.Send(new ListConceptsQuery(page, pageSize, search, type, language, includeRetired), ct)).ToOkResult();
}

/// <summary>GET /concepts/lookup?ids=1&amp;ids=2&amp;language=: the concepts with these ids, in that order.</summary>
public sealed class Lookup : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder concepts) =>
        concepts.MapGet("/lookup", HandleAsync)
            .RequirePermission(KeywordsPermissions.Read)
            .WithName("Keywords_LookupConcepts")
            .WithSummary("The concepts with these ids, in the order asked (retired ones too); unknown ids are left out.");

    private static async Task<Results<Ok<IReadOnlyList<ConceptSummaryDto>>, ProblemHttpResult>> HandleAsync([FromQuery] int[] ids, ISender sender,
        CancellationToken ct, string? language = null) =>
        (await sender.Send(new LookupConceptsQuery(ids, language), ct)).ToOkResult();
}

/// <summary>POST /concepts/match: for each name, the concepts like it (a name an AI or another module proposed).</summary>
public sealed class Match : IEndpoint
{
    /// <summary>The request body: up to 20 names, the language to name the matches in, optionally one schema.org type.</summary>
    public sealed record ConceptMatchRequest(IReadOnlyList<string> Terms, string? Language = null, string? Type = null);

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder concepts) =>
        concepts.MapPost("/match", HandleAsync)
            .RequirePermission(KeywordsPermissions.Read)
            .WithName("Keywords_MatchConcepts")
            .WithSummary("For each term (at most 20), up to 5 active concepts with a name like it in any language, best first; exact when a shown name is the term. Case, accents and Arabic letter forms, ta marbuta and a leading al- are ignored.");

    private static async Task<Results<Ok<IReadOnlyList<TermMatchesDto>>, ProblemHttpResult>> HandleAsync(ConceptMatchRequest body, ISender sender, CancellationToken ct) =>
        (await sender.Send(new MatchConceptsQuery(body.Terms ?? [], body.Language, body.Type), ct)).ToOkResult();
}

/// <summary>GET /concepts/types: the schema.org types a concept can have.</summary>
public sealed class Types : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder concepts) =>
        concepts.MapGet("/types", HandleAsync)
            .RequirePermission(KeywordsPermissions.Read)
            .WithName("Keywords_ListConceptTypes");

    private static async Task<Results<Ok<IReadOnlyList<ConceptType>>, ProblemHttpResult>> HandleAsync(ISender sender, CancellationToken ct) =>
        (await sender.Send(new ListConceptTypesQuery(), ct)).ToOkResult();
}

/// <summary>GET /concepts/{id}?language=: a concept with its labels, links and related concepts.</summary>
public sealed class Get : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder concepts) =>
        concepts.MapGet("/{id:int}", HandleAsync)
            .RequirePermission(KeywordsPermissions.Read)
            .WithName("Keywords_GetConcept");

    private static async Task<Results<Ok<ConceptDto>, ProblemHttpResult>> HandleAsync(int id, ISender sender, CancellationToken ct, string? language = null) =>
        (await sender.Send(new GetConceptQuery(id, language), ct)).ToOkResult();
}

/// <summary>GET /concepts/{id}/jsonld?language=: the concept as schema.org JSON-LD.</summary>
public sealed class JsonLd : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder concepts) =>
        concepts.MapGet("/{id:int}/jsonld", HandleAsync)
            .RequirePermission(KeywordsPermissions.Read)
            .WithName("Keywords_GetConceptJsonLd")
            .WithSummary("The concept as schema.org JSON-LD (type, name in the language, alternateName, description, sameAs).");

    private static async Task<Results<Ok<ConceptJsonLdDto>, ProblemHttpResult>> HandleAsync(int id, ISender sender, CancellationToken ct, string? language = null) =>
        (await sender.Send(new GetConceptJsonLdQuery(id, language), ct)).ToOkResult();
}

/// <summary>POST /concepts: add a concept; 201 with it.</summary>
public sealed class Create : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder concepts) =>
        concepts.MapPost("/", HandleAsync)
            .RequirePermission(KeywordsPermissions.Manage)
            .WithName("Keywords_CreateConcept");

    private static async Task<Results<Created<ConceptDto>, ProblemHttpResult>> HandleAsync(ConceptValues values, ISender sender, CancellationToken ct) =>
        (await sender.Send(new CreateConceptCommand(values), ct)).ToCreatedResult(concept => $"concepts/{concept.Id}");
}

/// <summary>PUT /concepts/{id}: replace a concept's values; the body carries the version the caller read.</summary>
public sealed class Edit : IEndpoint
{
    /// <summary>The request body.</summary>
    public sealed record ConceptEdit(int Version, ConceptValues Values);

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder concepts) =>
        concepts.MapPut("/{id:int}", HandleAsync)
            .RequirePermission(KeywordsPermissions.Manage)
            .WithName("Keywords_EditConcept");

    private static async Task<Results<Ok<ConceptDto>, ProblemHttpResult>> HandleAsync(int id, ConceptEdit edit, ISender sender, CancellationToken ct) =>
        (await sender.Send(new EditConceptCommand(id, edit.Version, edit.Values), ct)).ToOkResult();
}

/// <summary>The body of retire and restore.</summary>
public sealed record VersionRead(int Version);

/// <summary>POST /concepts/{id}/retire: take a concept out of pickers and search; content keeps it.</summary>
public sealed class Retire : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder concepts) =>
        concepts.MapPost("/{id:int}/retire", HandleAsync)
            .RequirePermission(KeywordsPermissions.Manage)
            .WithName("Keywords_RetireConcept");

    private static async Task<Results<Ok<ConceptDto>, ProblemHttpResult>> HandleAsync(int id, VersionRead body, ISender sender, CancellationToken ct) =>
        (await sender.Send(new SetConceptStatusCommand(id, body.Version, ConceptStatus.Retired), ct)).ToOkResult();
}

/// <summary>POST /concepts/{id}/restore: make a retired concept active again.</summary>
public sealed class Restore : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder concepts) =>
        concepts.MapPost("/{id:int}/restore", HandleAsync)
            .RequirePermission(KeywordsPermissions.Manage)
            .WithName("Keywords_RestoreConcept");

    private static async Task<Results<Ok<ConceptDto>, ProblemHttpResult>> HandleAsync(int id, VersionRead body, ISender sender, CancellationToken ct) =>
        (await sender.Send(new SetConceptStatusCommand(id, body.Version, ConceptStatus.Active), ct)).ToOkResult();
}
