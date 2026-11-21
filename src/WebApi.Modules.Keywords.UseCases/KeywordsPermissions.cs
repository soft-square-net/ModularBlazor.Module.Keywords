namespace soft.square.net.WebApi.Modules.Keywords.UseCases;

/// <summary>
/// The module's permissions, named <c>&lt;module&gt;.&lt;action&gt;</c>. Declare every permission the module uses (the
/// manifest lists <see cref="All"/>); activation fails on an undeclared one. Admins grant them per tenant.
/// </summary>
public static class KeywordsPermissions
{
    public const string Read = "keywords.read";
    public const string Manage = "keywords.manage";

    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new(Read, "Search and read concepts, and their JSON-LD."),
        new(Manage, "Add, change, retire and restore concepts.")
    ];
}
