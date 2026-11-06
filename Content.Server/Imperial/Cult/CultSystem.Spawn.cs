using Content.Server.Ghost.Roles.Components;

namespace Content.Server.Imperial.Cult;

public sealed partial class CultSystem
{
    /// <summary>Конструкты и тени принадлежат фракции культа.</summary>
    public void AddConstructFaction(EntityUid uid)
    {
        _faction.AddFaction(uid, CultFaction);
    }

    /// <summary>Опрос призраков (poll_ghosts_for_target): тело становится гост-ролью.</summary>
    public void MakeGhostRole(EntityUid uid, string name)
    {
        var role = EnsureComp<GhostRoleComponent>(uid);
        role.RoleName = name;
        role.RoleDescription = Loc.GetString("cult-ghost-role-desc");
        role.RoleRules = Loc.GetString("cult-ghost-role-rules");
        EnsureComp<GhostTakeoverAvailableComponent>(uid);
    }
}
