namespace soft.square.net.WebApi.Modules.Keywords.UseCases;

/// <summary>Module settings (section <c>Keywords</c>; defaults in the manifest, changed on the Host Modules page).</summary>
public sealed class KeywordsOptions
{
    /// <summary>The largest page a list or search returns.</summary>
    public int MaxPageSize { get; set; } = 100;
}
