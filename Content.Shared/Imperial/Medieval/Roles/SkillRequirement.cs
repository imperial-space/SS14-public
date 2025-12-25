using System.Diagnostics.CodeAnalysis;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Preferences;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Roles;

[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class SkillRequirement : JobRequirement
{
    [DataField(required: true)]
    public string Skill = default!;

    [DataField(required: true)]
    public int RequiredLevel;

    public override bool Check(
        IEntityManager entManager,
        IPrototypeManager protoManager,
        HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        reason = null;

        if (profile is null)
            return true;

        if (string.IsNullOrWhiteSpace(Skill))
            return true;

        var current = 10;
        if (profile.Skills.TryGetValue(Skill, out var v))
            current = v;

        var skillName = Skill;
        if (protoManager.TryIndex<SkillPrototype>(Skill, out var proto))
            skillName = Loc.GetString(proto.Name);

        if (!Inverted)
        {
            if (current >= RequiredLevel)
                return true;

            reason = FormattedMessage.FromMarkupPermissive(
                Loc.GetString("role-requirement-skill-too-low", ("skill", skillName), ("level", RequiredLevel)));
            return false;
        }

        if (current <= RequiredLevel)
            return true;

        reason = FormattedMessage.FromMarkupPermissive(
            Loc.GetString("role-requirement-skill-too-high", ("skill", skillName), ("level", RequiredLevel)));
        return false;
    }
}
