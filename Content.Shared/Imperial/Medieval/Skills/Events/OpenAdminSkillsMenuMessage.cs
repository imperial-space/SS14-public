using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Skills;

[Serializable, NetSerializable]
public sealed class OpenAdminSkillsMenuMessage : EntityEventArgs
{
    public readonly NetEntity Target;
    public readonly Dictionary<string, int> Levels;

    public OpenAdminSkillsMenuMessage(NetEntity target, Dictionary<string, int> levels)
    {
        Target = target;
        Levels = levels;
    }
}
