using soft.square.net.WebApi.Modules.Keywords.Client;

namespace soft.square.net.Module.Keywords;

/// <summary>Small display helpers the pages and the picker share.</summary>
public static class KeywordsUi
{
    /// <summary>Languages offered first in the label rows; any BCP 47 tag can be typed.</summary>
    public static IReadOnlyList<(string Tag, string Name)> CommonLanguages { get; } =
    [
        ("en", "English"), ("ar", "العربية"), ("fr", "Français"), ("es", "Español"), ("de", "Deutsch"), ("tr", "Türkçe"), ("ur", "اردو")
    ];

    /// <summary>The editor's label for a schema.org type ("Company" for Corporation), or the type itself.</summary>
    public static string TypeLabel(string type, IEnumerable<ConceptType> types) =>
        types.FirstOrDefault(t => t.Name == type)?.Label ?? type;

    /// <summary>Non-breaking spaces that show a type's depth in a select (Place › City).</summary>
    public static string Indent(ConceptType type, IReadOnlyList<ConceptType> types)
    {
        var depth = 0;
        for (var parent = type.Parent; parent is not null; parent = types.FirstOrDefault(t => t.Name == parent)?.Parent) depth++;
        return new string(' ', 3 * Math.Max(0, depth - 1));
    }

    /// <summary>Whether a language is written right to left (for the label inputs' dir).</summary>
    public static bool IsRtl(string? language) =>
        (language ?? "").Split('-')[0].ToLowerInvariant() is "ar" or "he" or "fa" or "ur" or "ps" or "yi" or "ckb" or "dv";
}
