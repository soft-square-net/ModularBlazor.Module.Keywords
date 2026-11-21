using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate;

/// <summary>The rules a concept's values follow; each check returns the cleaned value or throws <see cref="DomainException"/>.</summary>
public static partial class ConceptRules
{
    public const int SlugMaxLength = 120;
    public const int LabelMaxLength = 200;
    public const int LanguageMaxLength = 16;
    public const int DescriptionMaxLength = 1000;
    public const int UrlMaxLength = 500;
    public const int MaxLabels = 50;
    public const int MaxSameAs = 20;
    public const int MaxRelated = 50;

    [GeneratedRegex(@"^[\p{Ll}\p{Lo}\p{Lm}\p{Nd}]+(-[\p{Ll}\p{Lo}\p{Lm}\p{Nd}]+)*$")]
    private static partial Regex SlugPattern();

    [GeneratedRegex("^[a-z]{2,3}(-[a-z0-9]{2,8})*$")]
    private static partial Regex LanguagePattern();

    public static string CheckSlug(string slug)
    {
        var value = slug.Trim().ToLowerInvariant();
        if (value.Length is 0 or > SlugMaxLength || !SlugPattern().IsMatch(value))
            throw new DomainException($"A slug is lower-case letters (any script) and digits joined by single hyphens, at most {SlugMaxLength} characters ('{slug}').");
        return value;
    }

    /// <summary>
    /// A slug from a name: its letters (any script, lower case, accents and Arabic diacritics dropped) and digits,
    /// words joined by hyphens: "Café de Flore" gives cafe-de-flore, "القاهرة" stays القاهرة. Nothing usable gives "concept".
    /// </summary>
    public static string Slugify(string name)
    {
        var slug = new StringBuilder();
        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            var lower = char.ToLowerInvariant(c);
            if (char.IsLetterOrDigit(lower) && !char.IsUpper(lower)) slug.Append(lower);
            else if (slug.Length > 0 && slug[^1] != '-') slug.Append('-');
        }
        var text = slug.ToString().Trim('-');
        if (text.Length > SlugMaxLength) text = text[..SlugMaxLength].TrimEnd('-');
        return text.Length == 0 ? "concept" : text;
    }

    public static string CheckLanguage(string language)
    {
        var value = language.Trim().ToLowerInvariant();
        if (value.Length > LanguageMaxLength || !LanguagePattern().IsMatch(value))
            throw new DomainException($"'{language}' is not a language tag (en, ar, en-gb).");
        return value;
    }

    /// <summary>
    /// Labels, cleaned: trimmed and NFC, no blanks, no repeats (same language, same search key), at most one preferred
    /// label per language, and at least one preferred label in all. Preferred labels come first, in the order given.
    /// </summary>
    public static IReadOnlyList<LabelValue> CheckLabels(IReadOnlyList<LabelValue>? labels)
    {
        var cleaned = new List<LabelValue>();
        var seen = new HashSet<(string, string)>();
        foreach (var label in labels ?? [])
        {
            var text = (label.Text ?? "").Trim().Normalize(NormalizationForm.FormC);
            if (text.Length == 0) continue;
            if (text.Length > LabelMaxLength) throw new DomainException($"A label is at most {LabelMaxLength} characters ('{text[..40]}…').");
            if (!Enum.IsDefined(label.Kind)) throw new DomainException($"'{label.Kind}' is not a label kind.");
            var language = CheckLanguage(label.Language ?? "");
            if (!seen.Add((language, TextKey.For(text)))) throw new DomainException($"The {language} label '{text}' is there twice.");
            cleaned.Add(new LabelValue(language, text, label.Kind));
        }

        if (cleaned.Count > MaxLabels) throw new DomainException($"A concept has at most {MaxLabels} labels.");
        if (!cleaned.Any(l => l.Kind == LabelKind.Preferred)) throw new DomainException("A concept needs a preferred label (its name) in at least one language.");
        var twice = cleaned.Where(l => l.Kind == LabelKind.Preferred).GroupBy(l => l.Language).FirstOrDefault(g => g.Count() > 1);
        if (twice is not null) throw new DomainException($"One preferred label per language: {twice.Key} has {twice.Count()}. Make the others alternate labels.");

        return [.. cleaned.Where(l => l.Kind == LabelKind.Preferred), .. cleaned.Where(l => l.Kind != LabelKind.Preferred)];
    }

    public static string? CheckDescription(string? description)
    {
        var value = description?.Trim();
        if (string.IsNullOrEmpty(value)) return null;
        if (value.Length > DescriptionMaxLength) throw new DomainException($"A description is at most {DescriptionMaxLength} characters.");
        return value;
    }

    /// <summary>Absolute http(s) URLs (Wikidata, Wikipedia, an official site), without repeats.</summary>
    public static IReadOnlyList<string> CheckSameAs(IReadOnlyList<string>? urls)
    {
        var cleaned = new List<string>();
        foreach (var raw in urls ?? [])
        {
            var url = (raw ?? "").Trim();
            if (url.Length == 0) continue;
            if (url.Length > UrlMaxLength || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                throw new DomainException($"'{url}' is not an http(s) address.");
            if (!cleaned.Contains(url, StringComparer.OrdinalIgnoreCase)) cleaned.Add(url);
        }
        if (cleaned.Count > MaxSameAs) throw new DomainException($"A concept has at most {MaxSameAs} sameAs links.");
        return cleaned;
    }

    /// <summary>Related concept ids: positive, not the concept itself, without repeats. Whether they exist is the handler's check.</summary>
    public static IReadOnlyList<int> CheckRelated(IReadOnlyList<int>? related, int self)
    {
        var cleaned = (related ?? []).Distinct().ToList();
        if (cleaned.Any(id => id <= 0)) throw new DomainException("A related concept id is a positive number.");
        if (self != 0 && cleaned.Contains(self)) throw new DomainException("A concept is not related to itself.");
        if (cleaned.Count > MaxRelated) throw new DomainException($"A concept has at most {MaxRelated} related concepts.");
        return cleaned;
    }

    /// <summary>The preferred label in the language, else in its base language (ar-eg → ar); null without one.</summary>
    public static string? PreferredIn(IEnumerable<ConceptLabel> labels, string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return null;
        var wanted = language.Trim().ToLowerInvariant();
        var preferred = labels.Where(l => l.Kind == LabelKind.Preferred).ToList();
        return preferred.FirstOrDefault(l => l.Language == wanted)?.Text
            ?? preferred.FirstOrDefault(l => l.Language == wanted.Split('-')[0])?.Text
            ?? preferred.FirstOrDefault(l => l.Language.Split('-')[0] == wanted.Split('-')[0])?.Text;
    }
}
