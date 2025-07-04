using Content.Server.Hands.Systems;
using Content.Server.Imperial.DeviceLinking;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Medieval.Skills;

public sealed partial class SkillsSystem
{
    private void InitializeStrength()
    {
        SubscribeLocalEvent<SkillsComponent, CheckSignalSwitchActivationTimeEvent>(OnCheckSignalSwitchActivationTime);
        SubscribeLocalEvent<SkillsComponent, MeleeAttackEvent>(OnMeleeAttack);
    }

    private void OnCheckSignalSwitchActivationTime(EntityUid uid, SkillsComponent comp, ref CheckSignalSwitchActivationTimeEvent args)
    {
        var (proto, level) = GetSkill(uid, StrengthId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);
        args.Modifier += (level > 10 ? proto.Modifiers["PositiveGateInteractionTimeModifier"] : proto.Modifiers["NegativeGateInteractionTimeModifier"]) * diff;
    }

    private void OnMeleeAttack(EntityUid uid, SkillsComponent comp, MeleeAttackEvent args)
    {
        var (_, level) = GetSkill(uid, StrengthId);

        if (level > 1)
            return;

        if (!_random.Prob(0.3f))
            return;

        var coords = Transform(uid).Coordinates;
        _hands.ThrowHeldItem(uid, new EntityCoordinates(coords.EntityId, _random.NextFloat(coords.X - 2, coords.X + 2), _random.NextFloat(coords.Y - 2, coords.Y + 2)));
    }

    private void StrengthLevelSet(EntityUid uid, int level, int oldLevel)
    {
        if (level < 16)
            return;

        var comp = EnsureComp<MeleeThrowOnHitComponent>(uid);
        comp.Distance = 0.25f;
        Dirty(uid, comp);
    }
}
