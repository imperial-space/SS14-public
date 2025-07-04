using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Skills;

[Serializable, NetSerializable]
public sealed class SetSkillLevelMessage : EntityEventArgs
{
    public readonly NetEntity Target;
    public readonly string Skill;
    public readonly int Level;

    public SetSkillLevelMessage(NetEntity target, string skill, int level)
    {
        Target = target;
        Skill = skill;
        Level = level;
    }
}
