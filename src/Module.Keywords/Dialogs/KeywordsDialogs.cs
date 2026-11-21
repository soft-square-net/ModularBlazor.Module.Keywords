namespace soft.square.net.Module.Keywords;

/// <summary>
/// The dialogs this module provides to every module (IModuleDialogs), and what they answer. Other modules do not
/// reference this assembly: they use the id and the JSON shape, which is the contract.
/// </summary>
public static class KeywordsDialogs
{
    /// <summary>
    /// Choose concepts. Parameters <c>{ multiple?: bool, selected?: [ids], type?: "Person", language?: "ar",
    /// exclude?: [ids] }</c>; result <c>{ ids: [...], concepts: [{ id, name, type, slug }] }</c>, ids as strings, in the
    /// order chosen. <c>type</c> offers only that schema.org type; <c>language</c> names them in that language.
    /// </summary>
    public const string PickConcepts = "Module.Keywords/pick-concepts";

    /// <summary>What <see cref="PickConcepts"/> answers.</summary>
    public sealed record Picked(List<string> Ids, List<PickedConcept> Concepts);

    /// <summary>A chosen concept, named, so the caller can show it (or keep a snapshot) without a lookup.</summary>
    public sealed record PickedConcept(string Id, string Name, string Type, string Slug);
}
