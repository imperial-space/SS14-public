using System;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Ships;
using Content.Shared.Imperial.Medieval.Skills;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared.Imperial.Medieval.Ships.Anchor;

public sealed class MedievalAnchorSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSkillsSystem _skills = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MedievalAnchorComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnActivate(EntityUid uid, MedievalAnchorComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        Use(args.User, args.Target, component);
    }

    private void Use(EntityUid playerEntity, EntityUid target, MedievalAnchorComponent component)
    {
        if (!_skills.HasSkill(playerEntity, SharedSkillsSystem.StrengthId))
            return;

        var time = component.BaseUseTime - _skills.GetSkillLevel(playerEntity, "Strength") * component.StrengthUseTimeModifier;
        time = Math.Max(1.0f, time);

        if (!component.Enabled)
            time = time / 10;

        var doAfter = new DoAfterArgs(EntityManager,
            playerEntity,
            time,
            new UseAnchorEvent(),
            target,
            target: target)
        {
            MovementThreshold = 0.5f,
            BreakOnMove = true,
            CancelDuplicate = true,
            DistanceThreshold = 2,
            BreakOnDamage = true,
            RequireCanInteract = false,
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            NeedHand = true,
        };

        if (component.User is not null)
            return;

        if (_doAfter.TryStartDoAfter(doAfter) && _net.IsServer)
        {
            _audio.PlayPvs(MedievalShipSounds.AnchorUse, target);
            component.User = playerEntity;
            Dirty(target, component);
        }
        else
            component.User = null;
    }
}
