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

/// <summary>
/// Turns names into concepts: for each of <paramref name="Terms"/> (keywords another module or an AI proposed), the
/// active concepts with a name like it, best first. A term matches any label in any language, hidden ones too, on its
/// <see cref="MatchKey"/> (case, accents and Arabic letter forms, ta marbuta and a leading ال ignored);
/// <paramref name="Language"/> picks the names shown and <paramref name="Type"/> keeps one schema.org type. Nothing is
/// created: the caller offers the matches in the pick-concepts dialog, where a person chooses.
/// </summary>
public sealed record MatchConceptsQuery(IReadOnlyList<string> Terms, string? Language = null, string? Type = null)
    : IQuery<Result<IReadOnlyList<TermMatchesDto>>>;

public sealed class MatchConceptsHandler(IReadRepository<Concept> concepts)
    : IQueryHandler<MatchConceptsQuery, Result<IReadOnlyList<TermMatchesDto>>>
{
    public async ValueTask<Result<IReadOnlyList<TermMatchesDto>>> Handle(MatchConceptsQuery query, CancellationToken ct)
    {
        var terms = (query.Terms ?? []).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct(StringComparer.Ordinal).ToList();
        if (terms.Count == 0)
            return Result<IReadOnlyList<TermMatchesDto>>.Invalid(new ValidationError(nameof(query.Terms), "Give at least one term."));
        if (terms.Count > ConceptMatching.MaxTerms)
            return Result<IReadOnlyList<TermMatchesDto>>.Invalid(new ValidationError(nameof(query.Terms), $"Match at most {ConceptMatching.MaxTerms} terms at a time."));
        if (terms.FirstOrDefault(t => t.Length > ConceptMatching.MaxTermLength) is { } tooLong)
            return Result<IReadOnlyList<TermMatchesDto>>.Invalid(new ValidationError(nameof(query.Terms), $"A term has at most {ConceptMatching.MaxTermLength} characters: '{tooLong[..20]}...'."));
        if (query.Type is { Length: > 0 } type && ConceptTypes.Find(type) is null)
            return Result<IReadOnlyList<TermMatchesDto>>.Invalid(new ValidationError(nameof(query.Type), $"'{type}' is not a concept type."));

        var results = new List<TermMatchesDto>(terms.Count);
        foreach (var term in terms)
        {
            var key = MatchKey.For(term);
            var stem = MatchKey.Stem(key);
            if (stem.Length == 0) { results.Add(new TermMatchesDto(term, [], false)); continue; }
            var candidates = await concepts.ListAsync(new ConceptsContainingSpec(stem, query.Type is { Length: > 0 } ? query.Type : null, ConceptMatching.Candidates), ct);
            results.Add(ConceptMatching.Rank(term, key, candidates, query.Language));
        }
        return results;
    }
}

/// <summary>How a name is scored against a concept's labels; pure, so the ranking is tested without a database.</summary>
public static class ConceptMatching
{
    /// <summary>The most terms one call matches.</summary>
    public const int MaxTerms = 20;

    /// <summary>The most matches returned per term.</summary>
    public const int MaxMatches = 5;

    public const int MaxTermLength = 200;

    /// <summary>How many concepts containing a term's stem are read and scored per term.</summary>
    public const int Candidates = 50;

    /// <summary>
    /// How alike two match keys are: 1 when equal; 0.8 when one starts with the other as whole words ("messi" and
    /// "messi lionel"); 0.7 when one holds the other as whole words; 0.5 when one holds the other at all (a part of
    /// a word, at least three letters); else 0.
    /// </summary>
    public static double Score(string termKey, string labelKey)
    {
        if (termKey.Length == 0 || labelKey.Length == 0) return 0;
        if (termKey == labelKey) return 1;
        var (shorter, longer) = termKey.Length <= labelKey.Length ? (termKey, labelKey) : (labelKey, termKey);
        if (longer.StartsWith(shorter + " ", StringComparison.Ordinal)) return 0.8;
        if ($" {longer} ".Contains($" {shorter} ", StringComparison.Ordinal)) return 0.7;
        if (shorter.Length >= 3 && longer.Contains(shorter, StringComparison.Ordinal)) return 0.5;
        return 0;
    }

    /// <summary>
    /// The best <see cref="MaxMatches"/> of <paramref name="candidates"/> for a term: each concept scores its best
    /// label (a hidden label, a misspelling, counts 0.9 for an equal key), ties go to the shorter name, then the id.
    /// <c>exact</c> is true when a shown name (preferred or alternate) equals the term.
    /// </summary>
    public static TermMatchesDto Rank(string term, string termKey, IEnumerable<Concept> candidates, string? language)
    {
        var scored = new List<(Concept Concept, double Score, bool Exact)>();
        foreach (var concept in candidates)
        {
            double best = 0;
            var exact = false;
            foreach (var label in concept.Labels)
            {
                var score = Score(termKey, MatchKey.FromTextKey(label.SearchKey));
                if (score == 1 && label.Kind == LabelKind.Hidden) score = 0.9;
                else if (score == 1) exact = true;
                best = Math.Max(best, score);
            }
            if (best > 0) scored.Add((concept, best, exact));
        }

        var top = scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Concept.NameIn(language).Length)
            .ThenBy(s => s.Concept.Id)
            .Take(MaxMatches)
            .ToList();
        return new TermMatchesDto(term,
            [.. top.Select(s => new ConceptMatchDto(s.Concept.Id, s.Concept.NameIn(language), s.Concept.Type, s.Concept.Slug, s.Score))],
            top.Any(s => s.Exact));
    }
}
