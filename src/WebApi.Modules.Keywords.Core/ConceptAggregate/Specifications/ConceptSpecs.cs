namespace soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate.Specifications;

/// <summary>One concept with its labels, tracked (for changing it).</summary>
public sealed class ConceptByIdSpec : SingleResultSpecification<Concept>
{
    public ConceptByIdSpec(int id) => Query.Where(c => c.Id == id).Include(c => c.Labels);
}

/// <summary>One concept with its labels, untracked (for reading it).</summary>
public sealed class ConceptReadSpec : SingleResultSpecification<Concept>
{
    public ConceptReadSpec(int id) => Query.Where(c => c.Id == id).Include(c => c.Labels).AsNoTracking();
}

/// <summary>The concepts with these ids, with their labels, untracked.</summary>
public sealed class ConceptsByIdsSpec : Specification<Concept>
{
    public ConceptsByIdsSpec(IReadOnlyCollection<int> ids) =>
        Query.Where(c => ids.Contains(c.Id)).Include(c => c.Labels).AsNoTracking();
}

/// <summary>The concepts using a slug (at most one), untracked: to keep slugs unique.</summary>
public sealed class ConceptBySlugSpec : Specification<Concept>
{
    public ConceptBySlugSpec(string slug) => Query.Where(c => c.Slug == slug).AsNoTracking();
}

/// <summary>The slugs that start with a stem, to find a free one (stem, stem-2, stem-3...).</summary>
public sealed class SlugsLikeSpec : Specification<Concept, string>
{
    public SlugsLikeSpec(string stem)
    {
        Query.Where(c => c.Slug == stem || c.Slug.StartsWith(stem + "-")).AsNoTracking();
        Query.Select(c => c.Slug);
    }
}

/// <summary>
/// One page of concepts by name, untracked, with their labels. <paramref name="search"/> matches any label's search
/// key (<see cref="TextKey"/>: case, accents and Arabic forms folded), or the slug. <c>CountAsync</c> with the same
/// specification ignores the paging, so it gives the total.
/// </summary>
public sealed class ConceptsPageSpec : Specification<Concept>
{
    public ConceptsPageSpec(int page, int pageSize, string? search = null, string? type = null, bool includeRetired = false)
    {
        if (!includeRetired) Query.Where(c => c.Status == ConceptStatus.Active);
        if (!string.IsNullOrWhiteSpace(type)) Query.Where(c => c.Type == type);
        if (TextKey.For(search) is { Length: > 0 } key)
        {
            var slug = search!.Trim().ToLowerInvariant();
            Query.Where(c => c.Labels.Any(l => l.SearchKey.Contains(key)) || c.Slug.Contains(slug));
        }
        Query.OrderBy(c => c.Name).ThenBy(c => c.Id)
            .Include(c => c.Labels)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .AsNoTracking();
    }
}

/// <summary>
/// The active concepts with a label whose search key contains <paramref name="stem"/> (<see cref="MatchKey.Stem"/>),
/// with their labels, untracked, at most <paramref name="take"/>: the candidates a name is matched against.
/// </summary>
public sealed class ConceptsContainingSpec : Specification<Concept>
{
    public ConceptsContainingSpec(string stem, string? type, int take)
    {
        Query.Where(c => c.Status == ConceptStatus.Active && c.Labels.Any(l => l.SearchKey.Contains(stem)));
        if (!string.IsNullOrWhiteSpace(type)) Query.Where(c => c.Type == type);
        Query.OrderBy(c => c.Name).ThenBy(c => c.Id)
            .Include(c => c.Labels)
            .Take(take)
            .AsNoTracking();
    }
}
