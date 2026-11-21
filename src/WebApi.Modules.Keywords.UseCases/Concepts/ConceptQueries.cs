using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate;
using soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate.Specifications;

namespace soft.square.net.WebApi.Modules.Keywords.UseCases.Concepts;

/// <summary>
/// One page of the tenant's concepts by name. <paramref name="Search"/> matches any label (case, accents and Arabic
/// letter forms ignored) or the slug; <paramref name="Type"/> keeps one schema.org type; names come in
/// <paramref name="Language"/> when the concept has one in it.
/// </summary>
public sealed record ListConceptsQuery(int Page = 1, int PageSize = 20, string? Search = null, string? Type = null,
    string? Language = null, bool IncludeRetired = false) : IQuery<Result<ConceptPage>>;

public sealed class ListConceptsHandler(IReadRepository<Concept> concepts, IOptions<KeywordsOptions> options)
    : IQueryHandler<ListConceptsQuery, Result<ConceptPage>>
{
    public async ValueTask<Result<ConceptPage>> Handle(ListConceptsQuery query, CancellationToken ct)
    {
        var paging = ConceptResults.CheckPage(query.Page, query.PageSize, options.Value.MaxPageSize);
        if (!paging.IsSuccess) return paging.As<ConceptPage>();
        if (query.Type is { Length: > 0 } type && ConceptTypes.Find(type) is null)
            return Result<ConceptPage>.Invalid(new ValidationError(nameof(query.Type), $"'{type}' is not a concept type."));

        var spec = new ConceptsPageSpec(query.Page, query.PageSize, query.Search, query.Type, query.IncludeRetired);
        var items = await concepts.ListAsync(spec, ct);
        var total = await concepts.CountAsync(spec, ct); // counts without the paging
        return new ConceptPage([.. items.Select(c => ConceptSummaryDto.From(c, query.Language))], query.Page, query.PageSize, total);
    }
}

/// <summary>A concept with its labels, links and related concepts (named in <paramref name="Language"/>).</summary>
public sealed record GetConceptQuery(int Id, string? Language = null) : IQuery<Result<ConceptDto>>;

public sealed class GetConceptHandler(IReadRepository<Concept> concepts) : IQueryHandler<GetConceptQuery, Result<ConceptDto>>
{
    public async ValueTask<Result<ConceptDto>> Handle(GetConceptQuery query, CancellationToken ct)
    {
        var concept = await concepts.FirstOrDefaultAsync(new ConceptReadSpec(query.Id), ct);
        if (concept is null) return Result<ConceptDto>.NotFound(ConceptResults.NotFoundMessage(query.Id));
        return ConceptDto.From(concept, await ConceptLookup.RefsAsync(concepts, concept.Related, query.Language, ct));
    }
}

/// <summary>
/// The concepts with these ids, in the order asked, retired ones included (content may still have them); ids that do
/// not exist are left out. What pickers and other modules call to show the names of the ids they hold.
/// </summary>
public sealed record LookupConceptsQuery(IReadOnlyList<int> Ids, string? Language = null) : IQuery<Result<IReadOnlyList<ConceptSummaryDto>>>;

public sealed class LookupConceptsHandler(IReadRepository<Concept> concepts, IOptions<KeywordsOptions> options)
    : IQueryHandler<LookupConceptsQuery, Result<IReadOnlyList<ConceptSummaryDto>>>
{
    public async ValueTask<Result<IReadOnlyList<ConceptSummaryDto>>> Handle(LookupConceptsQuery query, CancellationToken ct)
    {
        var ids = query.Ids.Distinct().ToList();
        if (ids.Count > options.Value.MaxPageSize)
            return Result<IReadOnlyList<ConceptSummaryDto>>.Invalid(new ValidationError(nameof(query.Ids), $"Look up at most {options.Value.MaxPageSize} ids at a time."));
        if (ids.Count == 0) return Result<IReadOnlyList<ConceptSummaryDto>>.Success([]);

        var found = (await concepts.ListAsync(new ConceptsByIdsSpec(ids), ct)).ToDictionary(c => c.Id);
        return ids.Where(found.ContainsKey).Select(id => ConceptSummaryDto.From(found[id], query.Language)).ToList();
    }
}

/// <summary>A concept as schema.org JSON-LD, its name in <paramref name="Language"/>: what a page embeds as about/mentions.</summary>
public sealed record GetConceptJsonLdQuery(int Id, string? Language = null) : IQuery<Result<ConceptJsonLdDto>>;

/// <summary>A concept's JSON-LD object (<c>@context</c>, <c>@type</c>, <c>name</c>...), ready to embed.</summary>
public sealed record ConceptJsonLdDto(int Id, string Name, Dictionary<string, object?> JsonLd);

public sealed class GetConceptJsonLdHandler(IReadRepository<Concept> concepts) : IQueryHandler<GetConceptJsonLdQuery, Result<ConceptJsonLdDto>>
{
    public async ValueTask<Result<ConceptJsonLdDto>> Handle(GetConceptJsonLdQuery query, CancellationToken ct)
    {
        var concept = await concepts.FirstOrDefaultAsync(new ConceptReadSpec(query.Id), ct);
        return concept is null
            ? Result<ConceptJsonLdDto>.NotFound(ConceptResults.NotFoundMessage(query.Id))
            : new ConceptJsonLdDto(concept.Id, concept.NameIn(query.Language), ConceptJsonLd.For(concept, query.Language));
    }
}

/// <summary>The schema.org types a concept can have.</summary>
public sealed record ListConceptTypesQuery : IQuery<Result<IReadOnlyList<ConceptType>>>;

public sealed class ListConceptTypesHandler : IQueryHandler<ListConceptTypesQuery, Result<IReadOnlyList<ConceptType>>>
{
    public ValueTask<Result<IReadOnlyList<ConceptType>>> Handle(ListConceptTypesQuery query, CancellationToken ct) =>
        ValueTask.FromResult(Result<IReadOnlyList<ConceptType>>.Success(ConceptTypes.All));
}

/// <summary>Reads the concepts a concept points to.</summary>
internal static class ConceptLookup
{
    public static async Task<IReadOnlyList<ConceptRefDto>> RefsAsync(IReadRepositoryBase<Concept> concepts, IReadOnlyList<int> ids, string? language, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var found = (await concepts.ListAsync(new ConceptsByIdsSpec(ids), ct)).ToDictionary(c => c.Id);
        return [.. ids.Where(found.ContainsKey).Select(id => ConceptRefDto.From(found[id], language))];
    }
}
