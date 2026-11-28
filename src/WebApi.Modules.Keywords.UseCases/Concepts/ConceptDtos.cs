using soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate;

namespace soft.square.net.WebApi.Modules.Keywords.UseCases.Concepts;

/// <summary>A concept with everything an editor changes. <see cref="Version"/> goes back with an edit.</summary>
public sealed record ConceptDto(
    int Id,
    string Slug,
    string Type,
    string Name,
    string? Description,
    ConceptStatus Status,
    IReadOnlyList<LabelValue> Labels,
    IReadOnlyList<string> SameAs,
    IReadOnlyList<ConceptRefDto> Related,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int Version)
{
    public static ConceptDto From(Concept concept, IReadOnlyList<ConceptRefDto> related) =>
        new(concept.Id, concept.Slug, concept.Type, concept.Name, concept.Description, concept.Status,
            [.. concept.Labels.Select(l => new LabelValue(l.Language, l.Text, l.Kind))],
            concept.SameAs, related, concept.CreatedBy, concept.CreatedAt, concept.UpdatedAt, concept.Version);
}

/// <summary>
/// A concept as a list, a picker or another module shows it: its name in the asked language (else its first
/// preferred one), the languages it has a name in and its other visible names.
/// </summary>
public sealed record ConceptSummaryDto(
    int Id,
    string Slug,
    string Type,
    string Name,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> AlternateNames,
    string? Description,
    ConceptStatus Status,
    int Version)
{
    public static ConceptSummaryDto From(Concept concept, string? language)
    {
        var name = concept.NameIn(language);
        return new(concept.Id, concept.Slug, concept.Type, name,
            [.. concept.Labels.Where(l => l.Kind == LabelKind.Preferred).Select(l => l.Language).Distinct()],
            [.. concept.Labels.Where(l => l.Kind != LabelKind.Hidden && l.Text != name).Select(l => l.Text).Distinct().Take(8)],
            concept.Description, concept.Status, concept.Version);
    }
}

/// <summary>A concept named from elsewhere (a related concept, a tag): enough to show and link it.</summary>
public sealed record ConceptRefDto(int Id, string Slug, string Type, string Name, ConceptStatus Status)
{
    public static ConceptRefDto From(Concept concept, string? language) =>
        new(concept.Id, concept.Slug, concept.Type, concept.NameIn(language), concept.Status);
}

/// <summary>One page of concepts and the total across all pages.</summary>
public sealed record ConceptPage(IReadOnlyList<ConceptSummaryDto> Items, int Page, int PageSize, int Total);

/// <summary>Small helpers the concept use cases share.</summary>
public static class ConceptResults
{
    /// <summary>Checks a requested page against the module's settings.</summary>
    public static Result CheckPage(int page, int pageSize, int maxPageSize) =>
        page < 1 ? Result.Invalid(new ValidationError(nameof(page), "The page starts at 1."))
        : pageSize < 1 || pageSize > maxPageSize ? Result.Invalid(new ValidationError(nameof(pageSize), $"The page size is 1 to {maxPageSize}."))
        : Result.Success();

    public static string NotFoundMessage(int id) => $"Concept {id} was not found.";

    public static string StaleMessage(int id, int current, int read) =>
        $"Concept {id} is at version {current}, not {read}; reload it and try again.";

    /// <summary>The first of stem, stem-2, stem-3... not in <paramref name="taken"/>.</summary>
    public static string FreeSlug(string stem, IEnumerable<string> taken)
    {
        var used = taken.ToHashSet(StringComparer.Ordinal);
        if (!used.Contains(stem)) return stem;
        for (var i = 2; ; i++)
        {
            var suffix = $"-{i}";
            var slug = (stem.Length + suffix.Length > ConceptRules.SlugMaxLength ? stem[..(ConceptRules.SlugMaxLength - suffix.Length)] : stem) + suffix;
            if (!used.Contains(slug)) return slug;
        }
    }
}

/// <summary>The concepts like one term, best first; <see cref="Exact"/> when a shown name of one of them is the term.</summary>
public sealed record TermMatchesDto(string Term, IReadOnlyList<ConceptMatchDto> Matches, bool Exact);

/// <summary>A concept like a term: enough to offer it and keep its id. <see cref="Score"/> runs from 0 to 1.</summary>
public sealed record ConceptMatchDto(int Id, string Name, string Type, string Slug, double Score);
