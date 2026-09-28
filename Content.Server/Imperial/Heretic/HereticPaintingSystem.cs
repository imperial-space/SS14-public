using Content.Server.Popups;
using Content.Server.Traits.Assorted;
using Content.Shared.Body.Components;
using Content.Shared.Traits.Assorted;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Weapons.Melee;
using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticPaintingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ParacusiaSystem _paracusia = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly MovementModStatusSystem _movementMod = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    private static readonly string[] Organs =
    {
        "OrganHumanHeart",
        "OrganHumanLungs",
        "OrganHumanStomach",
        "OrganHumanLiver",
        "OrganHumanKidneys",
    };

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HereticPaintingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextUpdate)
                continue;
            comp.NextUpdate = now + comp.UpdateRate;

            var range = comp.VisibilityRange;
            foreach (var target in _lookup.GetEntitiesInRange(uid, range, LookupFlags.Dynamic))
            {
                if (!HasComp<ActorComponent>(target))
                    continue;
                if (!HasComp<MobStateComponent>(target))
                    continue;
                if (!_interaction.InRangeUnobstructed(uid, target, range + 0.5f))
                    continue;

                if (comp.PlayerCooldowns.TryGetValue(target, out var nextActivation) && now < nextActivation)
                    continue;
                comp.PlayerCooldowns[target] = now + comp.Cooldown;

                if (HasComp<HereticComponent>(target))
                    ApplyHereticEffect(uid, comp, target);
                else
                    ApplyNonHereticEffect(uid, comp, target);
            }
        }
    }

    private void ApplyHereticEffect(EntityUid uid, HereticPaintingComponent comp, EntityUid examiner)
    {
        var coords = _xform.GetMapCoordinates(examiner);

        switch (comp.PaintingType)
        {
            case HereticPaintingType.Weeping:
            case HereticPaintingType.Rust:
                _movementMod.TryAddMovementSpeedModDuration(examiner, "HereticPaintingSpeedBoostStatusEffect", TimeSpan.FromSeconds(30), 1.1f);
                _popup.PopupEntity(Loc.GetString("heretic-painting-heretic-speed-effect"), examiner, examiner, PopupType.Medium);
                break;

            case HereticPaintingType.Desire:
                var organ = _random.Pick(Organs);
                Spawn(organ, coords);
                _popup.PopupEntity(Loc.GetString("heretic-painting-heretic-desire-effect"), examiner, examiner, PopupType.Medium);
                break;

            case HereticPaintingType.Vines:
                Spawn("FoodPoppy", coords);
                _popup.PopupEntity(Loc.GetString("heretic-painting-heretic-vines-effect"), examiner, examiner, PopupType.Medium);
                break;

            case HereticPaintingType.Beauty:
                if (TryComp<BloodstreamComponent>(examiner, out var bloodstream))
                {
                    if (bloodstream.MetabolitesSolution != null)
                        _solution.RemoveAllSolution(bloodstream.MetabolitesSolution.Value);
                    if (bloodstream.TemporarySolution != null)
                        _solution.RemoveAllSolution(bloodstream.TemporarySolution.Value);
                }
                _popup.PopupEntity(Loc.GetString("heretic-painting-heretic-beauty-effect"), examiner, examiner, PopupType.Medium);
                break;
        }
    }

    private void ApplyNonHereticEffect(EntityUid uid, HereticPaintingComponent comp, EntityUid examiner)
    {
        var coords = _xform.GetMapCoordinates(examiner);

        switch (comp.PaintingType)
        {
            case HereticPaintingType.Weeping:
                _statusEffects.TryAddStatusEffectDuration(examiner, "HereticWeeepingHallucinationStatusEffect", TimeSpan.FromSeconds(30));
                if (!EnsureComp<ParacusiaComponent>(examiner, out var paracusia))
                {
                    _paracusia.SetSounds(examiner, new SoundCollectionSpecifier("Paracusia"), paracusia);
                    _paracusia.SetTime(examiner, 5f, 30f, paracusia);
                    _paracusia.SetDistance(examiner, 7f);
                }
                _popup.PopupEntity(Loc.GetString("heretic-painting-weeping-effect"), examiner, examiner, PopupType.LargeCaution);
                break;

            case HereticPaintingType.Desire:
                if (TryComp<HungerComponent>(examiner, out var hunger))
                    _hunger.ModifyHunger(examiner, -100, hunger);
                _popup.PopupEntity(Loc.GetString("heretic-painting-desire-effect"), examiner, examiner, PopupType.MediumCaution);
                break;

            case HereticPaintingType.Vines:
                Spawn("Kudzu", coords);
                _popup.PopupEntity(Loc.GetString("heretic-painting-vines-effect"), examiner, examiner, PopupType.MediumCaution);
                break;

            case HereticPaintingType.Beauty:
                if (_melee.TryGetWeapon(examiner, out var weaponUid, out var melee))
                {
                    var dmg = _melee.GetDamage(weaponUid, examiner, melee);
                    _damage.TryChangeDamage(examiner, dmg, ignoreResistances: false, origin: weaponUid);
                }
                _popup.PopupEntity(Loc.GetString("heretic-painting-beauty-effect"), examiner, examiner, PopupType.LargeCaution);
                break;

            case HereticPaintingType.Rust:
                var baseMapCoords = _xform.GetMapCoordinates(examiner);
                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        var tilePos = new MapCoordinates(baseMapCoords.Position + new Vector2(dx, dy), baseMapCoords.MapId);
                        if (_lookup.GetEntitiesInRange<HereticRustOverlayComponent>(tilePos, 0.4f, LookupFlags.Static).Count == 0)
                            Spawn("HereticRustOverlay", tilePos);
                    }
                }
                _popup.PopupEntity(Loc.GetString("heretic-painting-rust-effect"), examiner, examiner, PopupType.LargeCaution);
                break;
        }
    }
}
