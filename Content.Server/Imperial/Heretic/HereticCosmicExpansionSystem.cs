using Content.Server.Imperial.Heretic.Components;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticCosmicExpansionSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem  _lookup        = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedAudioSystem   _audio         = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticComponent, HereticCosmicExpansionActionEvent>(OnCosmicExpansion);
    }

    private void OnCosmicExpansion(EntityUid uid, HereticComponent comp, HereticCosmicExpansionActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        var xform = Transform(uid);
        var parentUid = xform.ParentUid;
        var center = new Vector2(MathF.Floor(xform.LocalPosition.X) + 0.5f, MathF.Floor(xform.LocalPosition.Y) + 0.5f);

        for (var dx = -2; dx <= 2; dx++)
        {
            for (var dy = -2; dy <= 2; dy++)
            {
                var pos = center + new Vector2(dx, dy);
                SpawnCarpet(comp.PassiveLevel, parentUid, pos);
            }
        }

        var coords = xform.Coordinates;
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 7f))
        {
            if (ent.Owner == uid) continue;
            if (HasComp<HereticComponent>(ent.Owner)) continue;
            _statusEffects.TrySetStatusEffectDuration(ent.Owner, "StarMarkStatusEffect", TimeSpan.FromSeconds(30));
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_magic_cosmic_expansion.ogg"), uid);
    }

    private void SpawnCarpet(int passiveLevel, EntityUid parent, Vector2 pos)
    {
        var carpet = Spawn("HereticCosmicCarpet", new EntityCoordinates(parent, pos));
        if (passiveLevel > 0 && TryComp<HereticCosmicFieldComponent>(carpet, out var field))
            field.PassiveLevel = passiveLevel;
    }
}
