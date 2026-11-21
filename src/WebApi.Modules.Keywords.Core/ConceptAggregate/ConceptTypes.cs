namespace soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate;

/// <summary>One schema.org type a concept can have, with its parent (for "is a" checks and grouping).</summary>
public sealed record ConceptType(string Name, string? Parent, string Label, string Description);

/// <summary>
/// The schema.org types a concept can be: the things content is about or mentions. A short list on purpose, so
/// editors choose quickly and the JSON-LD stays valid; <see cref="Thing"/> when nothing fits, <see cref="DefinedTerm"/>
/// for topics and subjects (Sports, Climate change).
/// </summary>
public static class ConceptTypes
{
    public const string Thing = "Thing";
    public const string DefinedTerm = "DefinedTerm";

    public static IReadOnlyList<ConceptType> All { get; } =
    [
        new(Thing, null, "Thing", "Anything that fits no other type."),
        new(DefinedTerm, Thing, "Topic", "A subject or idea: Sports, Climate change, Artificial intelligence."),
        new("Person", Thing, "Person", "A person, alive, dead or fictional."),
        new("Organization", Thing, "Organization", "A company, club, party, NGO or other group."),
        new("Corporation", "Organization", "Company", "A business corporation."),
        new("GovernmentOrganization", "Organization", "Government body", "A ministry, agency or other public body."),
        new("EducationalOrganization", "Organization", "School or university", "A school, college or university."),
        new("SportsTeam", "Organization", "Sports team", "A club or national team."),
        new("NewsMediaOrganization", "Organization", "News organization", "A newspaper, channel or news agency."),
        new("LocalBusiness", "Organization", "Local business", "A shop, restaurant or other business with a place."),
        new("Place", Thing, "Place", "A place that fits no narrower type."),
        new("Country", "Place", "Country", "A country."),
        new("AdministrativeArea", "Place", "Region", "A state, province, governorate or district."),
        new("City", "Place", "City", "A city or town."),
        new("LandmarksOrHistoricalBuildings", "Place", "Landmark", "A monument, building or site of note."),
        new("Event", Thing, "Event", "Something that happens at a time and place."),
        new("SportsEvent", "Event", "Sports event", "A match, race or tournament."),
        new("Product", Thing, "Product", "A product or model."),
        new("Brand", Thing, "Brand", "A brand name."),
        new("CreativeWork", Thing, "Creative work", "A work that fits no narrower type."),
        new("Book", "CreativeWork", "Book", "A book."),
        new("Movie", "CreativeWork", "Film", "A film."),
        new("TVSeries", "CreativeWork", "TV series", "A television series."),
        new("MusicRecording", "CreativeWork", "Song", "A song or other recording."),
        new("SoftwareApplication", "CreativeWork", "Software", "An application, game or service."),
        new("Taxon", Thing, "Species", "A living thing as a biological group: a species, a breed."),
        new("MedicalCondition", Thing, "Medical condition", "A disease, disorder or injury.")
    ];

    private static readonly Dictionary<string, ConceptType> ByName = All.ToDictionary(t => t.Name, StringComparer.Ordinal);

    public static ConceptType? Find(string? name) => name is not null && ByName.TryGetValue(name, out var type) ? type : null;

    /// <summary>The type, or a broken rule if it is not one of <see cref="All"/>.</summary>
    public static string Check(string? name) =>
        Find(name?.Trim())?.Name ?? throw new DomainException($"'{name}' is not a concept type; use one of {string.Join(", ", All.Select(t => t.Name))}.");

    /// <summary>Whether <paramref name="name"/> is <paramref name="ancestor"/> or one of its narrower types.</summary>
    public static bool IsA(string name, string ancestor)
    {
        for (var type = Find(name); type is not null; type = Find(type.Parent))
            if (type.Name == ancestor) return true;
        return false;
    }
}
