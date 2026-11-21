namespace soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate;

/// <summary>
/// A concept as schema.org JSON-LD, the way a page embeds what it is <c>about</c> or <c>mentions</c>: its type, its
/// name in the page's language, the other names as <c>alternateName</c>, its description and its <c>sameAs</c> links.
/// Hidden labels never appear. The consumer adds an <c>@id</c> when it has a public URL for the concept.
/// </summary>
public static class ConceptJsonLd
{
    public static Dictionary<string, object?> For(Concept concept, string? language, bool withContext = true)
    {
        var name = concept.NameIn(language);
        var others = concept.Labels
            .Where(l => l.Kind != LabelKind.Hidden && l.Text != name)
            .Select(l => l.Text)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var json = new Dictionary<string, object?>();
        if (withContext) json["@context"] = "https://schema.org";
        json["@type"] = concept.Type;
        json["name"] = name;
        if (others.Count > 0) json["alternateName"] = others;
        if (concept.Description is { Length: > 0 } description) json["description"] = description;
        if (concept.SameAs.Count > 0) json["sameAs"] = concept.SameAs;
        if (concept.Type == ConceptTypes.DefinedTerm) json["termCode"] = concept.Slug;
        else json["identifier"] = concept.Slug;
        return json;
    }
}
