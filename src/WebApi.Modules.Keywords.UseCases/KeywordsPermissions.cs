namespace soft.square.net.WebApi.Modules.Keywords.UseCases;

/// <summary>
/// The module's permissions, named <c>&lt;module&gt;.&lt;action&gt;</c>. Declare every permission the module uses (the
/// manifest lists <see cref="All"/>); activation fails on an undeclared one. Admins grant them per tenant.
/// </summary>
public static class KeywordsPermissions
{
    public const string Read = "keywords.read";
    public const string Write = "keywords.write";

    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new(Read, "Read and search notes."),
        new(Write, "Create, edit and delete notes.")
    ];
}
