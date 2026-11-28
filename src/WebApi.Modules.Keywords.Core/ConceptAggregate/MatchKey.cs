namespace soft.square.net.WebApi.Modules.Keywords.Core.ConceptAggregate;

/// <summary>
/// The looser key that matching a name to a concept compares (<c>POST /concepts/match</c>): the
/// <see cref="TextKey"/> (case, accents, Arabic diacritics, tatweel, alef forms and alef maqsura folded), then, per
/// word, ta marbuta (ة) written as ha (ه) and a leading definite article (ال) dropped when at least two letters stay.
/// So <c>القاهرة</c>, <c>قاهره</c> and <c>الْقَاهِرَه</c> give one key. Search keeps ta marbuta and ha apart
/// (<see cref="TextKey"/>); matching a name someone else wrote (an AI's keyword) accepts the few merged words, since
/// a person still picks the concept.
/// </summary>
public static class MatchKey
{
    public static string For(string? text) => FromTextKey(TextKey.For(text));

    /// <summary>The match key of a text that is already a <see cref="TextKey"/> (a label's stored search key).</summary>
    public static string FromTextKey(string textKey)
    {
        if (textKey.Length == 0) return "";
        return string.Join(' ', textKey.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Word));
    }

    /// <summary>
    /// The part of a match key that the stored search keys of every matching label contain: its longest word, with a
    /// trailing ha dropped (the label may have ta marbuta there). Empty when nothing is left to look for.
    /// </summary>
    public static string Stem(string matchKey)
    {
        var word = matchKey.Split(' ', StringSplitOptions.RemoveEmptyEntries).OrderByDescending(w => w.Length).FirstOrDefault() ?? "";
        if (word.Length > 2 && word[^1] == 'ه') word = word[..^1];
        return word;
    }

    private static string Word(string word)
    {
        if (word.Length >= 4 && word.StartsWith("ال", StringComparison.Ordinal)) word = word[2..];
        return word.Replace('ة', 'ه');
    }
}
