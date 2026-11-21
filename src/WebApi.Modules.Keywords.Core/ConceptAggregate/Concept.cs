using System.Text.Json;
using System.Text.Json.Serialization;

namespace soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate;

/// <summary>
/// A concept: one thing or idea content can be about (a person, a company, a place, a topic), with a stable id that
/// tags point to. Its names are <see cref="ConceptLabel"/>s, per language: one preferred label per language, any
/// number of alternate ones (synonyms, abbreviations, transliterations) and hidden ones (misspellings found only by
/// search). <see cref="SameAs"/> links it to the same thing elsewhere (Wikidata, Wikipedia, an official site), which is
/// what search engines and AI use to know which "Jordan" it is. Concepts are retired, never deleted: content keeps
/// pointing at them.
/// </summary>
public sealed class Concept : AggregateRoot<int>, ITenantOwned
{
    private readonly List<ConceptLabel> _labels = [];
    private List<string>? _sameAs;
    private List<int>? _related;

    private Concept() { } // EF Core

    /// <summary>Set by the platform's TenantDbContext when the concept is saved.</summary>
    public string TenantId { get; set; } = "";

    /// <summary>URL-safe name, unique per tenant (<c>lionel-messi</c>); can change, unlike <see cref="EntityBase{TId}.Id"/>.</summary>
    public string Slug { get; private set; } = "";

    /// <summary>The schema.org type (<see cref="ConceptTypes"/>).</summary>
    public string Type { get; private set; } = ConceptTypes.Thing;

    /// <summary>The first preferred label, kept in a column for lists and ordering.</summary>
    public string Name { get; private set; } = "";

    public string? Description { get; private set; }

    public ConceptStatus Status { get; private set; } = ConceptStatus.Active;

    /// <summary>The <see cref="SameAs"/> URLs as stored (JSON).</summary>
    public string SameAsJson { get; private set; } = "[]";

    /// <summary>The <see cref="Related"/> ids as stored (JSON).</summary>
    public string RelatedJson { get; private set; } = "[]";

    public string CreatedBy { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Bumped by every change; a concurrency token, so a stale edit fails instead of overwriting.</summary>
    public int Version { get; private set; }

    public IReadOnlyList<ConceptLabel> Labels => _labels;

    public IReadOnlyList<string> SameAs => _sameAs ??= JsonSerializer.Deserialize<List<string>>(SameAsJson) ?? [];

    /// <summary>Other concepts this one is associated with (an event and its city); symmetric in meaning, stored here.</summary>
    public IReadOnlyList<int> Related => _related ??= JsonSerializer.Deserialize<List<int>>(RelatedJson) ?? [];

    public static Concept Create(ConceptValues values, string createdBy, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(createdBy)) throw new DomainException("A concept needs its author.");
        var concept = new Concept { CreatedBy = createdBy, CreatedAt = now };
        concept.Set(values, now);
        return concept;
    }

    public void Edit(ConceptValues values, DateTimeOffset now)
    {
        Set(values, now);
        Version++;
    }

    /// <summary>Takes the concept out of pickers and search; content that has it keeps it.</summary>
    public void Retire(DateTimeOffset now)
    {
        if (Status == ConceptStatus.Retired) throw new DomainException($"Concept {Id} is retired already.");
        Status = ConceptStatus.Retired;
        UpdatedAt = now;
        Version++;
    }

    public void Restore(DateTimeOffset now)
    {
        if (Status == ConceptStatus.Active) throw new DomainException($"Concept {Id} is active already.");
        Status = ConceptStatus.Active;
        UpdatedAt = now;
        Version++;
    }

    /// <summary>The preferred label in <paramref name="language"/> (or its base language), else the first preferred one.</summary>
    public string NameIn(string? language) => ConceptRules.PreferredIn(_labels, language) ?? Name;

    private void Set(ConceptValues values, DateTimeOffset now)
    {
        var labels = ConceptRules.CheckLabels(values.Labels);
        var sameAs = ConceptRules.CheckSameAs(values.SameAs);
        var related = ConceptRules.CheckRelated(values.Related, Id);

        Type = ConceptTypes.Check(values.Type);
        Slug = ConceptRules.CheckSlug(values.Slug is { Length: > 0 } slug ? slug : ConceptRules.Slugify(labels[0].Text));
        Description = ConceptRules.CheckDescription(values.Description);
        Name = labels.First(l => l.Kind == LabelKind.Preferred).Text;

        _labels.Clear();
        _labels.AddRange(labels.Select(l => new ConceptLabel(l.Language, l.Text, l.Kind)));
        _sameAs = [.. sameAs];
        SameAsJson = JsonSerializer.Serialize(_sameAs);
        _related = [.. related];
        RelatedJson = JsonSerializer.Serialize(_related);
        UpdatedAt = now;
    }
}

/// <summary>One name of a concept, in one language. <see cref="SearchKey"/> is what search compares.</summary>
public sealed class ConceptLabel
{
    private ConceptLabel() { } // EF Core

    public ConceptLabel(string language, string text, LabelKind kind)
    {
        Language = language;
        Text = text;
        Kind = kind;
        SearchKey = TextKey.For(text);
    }

    public int Id { get; private set; }

    /// <summary>A BCP 47 language tag, lower case (<c>en</c>, <c>ar</c>, <c>en-gb</c>).</summary>
    public string Language { get; private set; } = "";

    /// <summary>As the editor typed it (NFC), shown as is.</summary>
    public string Text { get; private set; } = "";

    public LabelKind Kind { get; private set; }

    /// <summary><see cref="Text"/> folded for matching (<see cref="TextKey"/>); never shown.</summary>
    public string SearchKey { get; private set; } = "";
}

[JsonConverter(typeof(JsonStringEnumConverter<LabelKind>))]
public enum LabelKind
{
    /// <summary>The name to show; one per language.</summary>
    Preferred,

    /// <summary>Another name: synonym, abbreviation, spelling variant, transliteration, former name.</summary>
    Alternate,

    /// <summary>Found by search but never shown (common misspellings).</summary>
    Hidden
}

[JsonConverter(typeof(JsonStringEnumConverter<ConceptStatus>))]
public enum ConceptStatus
{
    Active,
    Retired
}

/// <summary>What an editor sets on a concept.</summary>
public sealed record ConceptValues(
    string Type,
    IReadOnlyList<LabelValue> Labels,
    string? Slug = null,
    string? Description = null,
    IReadOnlyList<string>? SameAs = null,
    IReadOnlyList<int>? Related = null);

public sealed record LabelValue(string Language, string Text, LabelKind Kind = LabelKind.Preferred);
