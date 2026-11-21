using soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate;
using soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate.Specifications;

namespace soft.square.net.WebApi.Modules.Keywords.UseCases.Concepts;

/// <summary>
/// Add a concept; the caller becomes its author. Without a slug, one is made from the first preferred label and
/// numbered when taken (messi, messi-2); a slug given and taken is a conflict.
/// </summary>
public sealed record CreateConceptCommand(ConceptValues Values) : ICommand<Result<ConceptDto>>;

public sealed class CreateConceptHandler(IRepository<Concept> concepts, IAccessScopes access, TimeProvider clock)
    : ICommandHandler<CreateConceptCommand, Result<ConceptDto>>
{
    public async ValueTask<Result<ConceptDto>> Handle(CreateConceptCommand command, CancellationToken ct)
    {
        if (access.UserId is not { Length: > 0 } me) return Result<ConceptDto>.Unauthorized("Sign in first.");

        var values = command.Values;
        var concept = Concept.Create(values, me, clock.GetUtcNow());
        if (values.Slug is { Length: > 0 })
        {
            if (await concepts.AnyAsync(new ConceptBySlugSpec(concept.Slug), ct))
                return Result<ConceptDto>.Conflict($"The slug '{concept.Slug}' is taken.");
        }
        else
        {
            var free = ConceptResults.FreeSlug(concept.Slug, await concepts.ListAsync(new SlugsLikeSpec(concept.Slug), ct));
            if (free != concept.Slug) concept = Concept.Create(values with { Slug = free }, me, clock.GetUtcNow());
        }

        var missing = await ConceptChecks.MissingAsync(concepts, concept.Related, ct);
        if (missing.Count > 0) return Result<ConceptDto>.Invalid(ConceptChecks.MissingError(missing));

        await concepts.AddAsync(concept, ct);
        return ConceptDto.From(concept, await ConceptLookup.RefsAsync(concepts, concept.Related, null, ct));
    }
}

/// <summary>Replace a concept's values. No slug keeps the current one. <paramref name="Version"/> is the one the caller read.</summary>
public sealed record EditConceptCommand(int Id, int Version, ConceptValues Values) : ICommand<Result<ConceptDto>>;

public sealed class EditConceptHandler(IRepository<Concept> concepts, TimeProvider clock) : ICommandHandler<EditConceptCommand, Result<ConceptDto>>
{
    public async ValueTask<Result<ConceptDto>> Handle(EditConceptCommand command, CancellationToken ct)
    {
        var concept = await concepts.FirstOrDefaultAsync(new ConceptByIdSpec(command.Id), ct);
        if (concept is null) return Result<ConceptDto>.NotFound(ConceptResults.NotFoundMessage(command.Id));
        if (concept.Version != command.Version) return Result<ConceptDto>.Conflict(ConceptResults.StaleMessage(command.Id, concept.Version, command.Version));

        var values = command.Values.Slug is { Length: > 0 } ? command.Values : command.Values with { Slug = concept.Slug };
        var slug = ConceptRules.CheckSlug(values.Slug!);
        if (slug != concept.Slug && await concepts.AnyAsync(new ConceptBySlugSpec(slug), ct))
            return Result<ConceptDto>.Conflict($"The slug '{slug}' is taken.");

        var missing = await ConceptChecks.MissingAsync(concepts, ConceptRules.CheckRelated(values.Related, concept.Id), ct);
        if (missing.Count > 0) return Result<ConceptDto>.Invalid(ConceptChecks.MissingError(missing));

        concept.Edit(values, clock.GetUtcNow());
        await concepts.UpdateAsync(concept, ct);
        return ConceptDto.From(concept, await ConceptLookup.RefsAsync(concepts, concept.Related, null, ct));
    }
}

/// <summary>Retire a concept (out of pickers and search; tags keep it), or restore a retired one.</summary>
public sealed record SetConceptStatusCommand(int Id, int Version, ConceptStatus Status) : ICommand<Result<ConceptDto>>;

public sealed class SetConceptStatusHandler(IRepository<Concept> concepts, TimeProvider clock) : ICommandHandler<SetConceptStatusCommand, Result<ConceptDto>>
{
    public async ValueTask<Result<ConceptDto>> Handle(SetConceptStatusCommand command, CancellationToken ct)
    {
        var concept = await concepts.FirstOrDefaultAsync(new ConceptByIdSpec(command.Id), ct);
        if (concept is null) return Result<ConceptDto>.NotFound(ConceptResults.NotFoundMessage(command.Id));
        if (concept.Version != command.Version) return Result<ConceptDto>.Conflict(ConceptResults.StaleMessage(command.Id, concept.Version, command.Version));

        if (command.Status == ConceptStatus.Retired) concept.Retire(clock.GetUtcNow());
        else concept.Restore(clock.GetUtcNow());
        await concepts.UpdateAsync(concept, ct);
        return ConceptDto.From(concept, await ConceptLookup.RefsAsync(concepts, concept.Related, null, ct));
    }
}

internal static class ConceptChecks
{
    /// <summary>The ids that are not concepts of this tenant.</summary>
    public static async Task<IReadOnlyList<int>> MissingAsync(IReadRepositoryBase<Concept> concepts, IReadOnlyList<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var found = (await concepts.ListAsync(new ConceptsByIdsSpec(ids), ct)).Select(c => c.Id).ToHashSet();
        return [.. ids.Where(id => !found.Contains(id))];
    }

    public static ValidationError MissingError(IReadOnlyList<int> missing) =>
        new("related", $"No such concepts: {string.Join(", ", missing)}.");
}
