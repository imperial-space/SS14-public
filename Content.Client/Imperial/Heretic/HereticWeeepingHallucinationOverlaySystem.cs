using Content.Shared.Humanoid;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.StatusEffectNew;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticWeeepingHallucinationOverlaySystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SpriteSystem   _sprite = default!;
    [Dependency] private readonly IRobustRandom  _random = default!;

    private bool _active;

    // Per-entity variant index: 0 = default heretic gear, 1 = moon path gear
    private readonly Dictionary<EntityUid, int> _entityVariants = new();
    private readonly HashSet<EntityUid>          _affected       = new();

    // Variant 0: default heretic uniform
    private static readonly SpriteSpecifier.Rsi DefaultArmorSpec = new(
        new ResPath("Imperial/Other/Heretic/Clothes/HereticRobe.rsi"),
        "equipped-OUTERCLOTHING");

    private static readonly SpriteSpecifier.Rsi DefaultHoodSpec = new(
        new ResPath("Imperial/Other/Heretic/Clothes/HereticHelmet.rsi"),
        "equipped-HELMET");

    // Variant 1: moon path armor
    private static readonly SpriteSpecifier.Rsi MoonArmorSpec = new(
        new ResPath("Imperial/heretic/heretic_robes.rsi"),
        "moon_armor_worn");

    private static readonly SpriteSpecifier.Rsi MoonHoodSpec = new(
        new ResPath("Imperial/heretic/heretic_hoods.rsi"),
        "moon_armor_worn");

    // Moon blade overlay (right hand), shown with variant 1
    private static readonly SpriteSpecifier.Rsi MoonBladeSpec = new(
        new ResPath("Imperial/heretic/blade_moon_inhand.rsi"),
        "moon_blade-inhand-right");

    private enum HallucinationKey { Armor, Hood, Blade }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticWeeepingHallucinationStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<HereticWeeepingHallucinationStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<HereticWeeepingHallucinationStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerAttachedEvent>>(OnPlayerAttached);
        SubscribeLocalEvent<HereticWeeepingHallucinationStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerDetachedEvent>>(OnPlayerDetached);
        SubscribeLocalEvent<HumanoidProfileComponent, ComponentStartup>(OnHumanoidAdded);
    }

    private void OnApplied(Entity<HereticWeeepingHallucinationStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;
        Activate();
    }

    private void OnRemoved(Entity<HereticWeeepingHallucinationStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;
        Deactivate();
    }

    private void OnPlayerAttached(Entity<HereticWeeepingHallucinationStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerAttachedEvent> args)
    {
        Activate();
    }

    private void OnPlayerDetached(Entity<HereticWeeepingHallucinationStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerDetachedEvent> args)
    {
        Deactivate();
    }

    private void OnHumanoidAdded(EntityUid uid, HumanoidProfileComponent comp, ComponentStartup args)
    {
        if (!_active || uid == _player.LocalEntity)
            return;
        AddLayers(uid);
    }

    private void Activate()
    {
        _active = true;
        var query = EntityQueryEnumerator<HumanoidProfileComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (uid == _player.LocalEntity)
                continue;
            AddLayers(uid);
        }
    }

    private void Deactivate()
    {
        _active = false;
        foreach (var uid in _affected)
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite))
                continue;
            Entity<SpriteComponent?> ent = new(uid, sprite);
            RemoveLayerIfExists(ent, HallucinationKey.Armor);
            RemoveLayerIfExists(ent, HallucinationKey.Hood);
            RemoveLayerIfExists(ent, HallucinationKey.Blade);
        }
        _affected.Clear();
        _entityVariants.Clear();
    }

    private void RemoveLayerIfExists(Entity<SpriteComponent?> ent, HallucinationKey key)
    {
        if (_sprite.LayerMapTryGet(ent, key, out var layer, false))
            _sprite.RemoveLayer(ent, layer);
    }

    private void AddLayers(EntityUid uid)
    {
        if (_affected.Contains(uid))
            return;
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        // Assign a random outfit variant for this entity (0 = default, 1 = moon)
        var variant = _random.Next(0, 2);
        _entityVariants[uid] = variant;

        Entity<SpriteComponent?> ent = new(uid, sprite);

        if (variant == 0)
        {
            // Default heretic uniform
            AddLayerIfMissing(ent, HallucinationKey.Armor, DefaultArmorSpec);
            AddLayerIfMissing(ent, HallucinationKey.Hood, DefaultHoodSpec);
        }
        else
        {
            // Moon path gear
            AddLayerIfMissing(ent, HallucinationKey.Armor, MoonArmorSpec);
            AddLayerIfMissing(ent, HallucinationKey.Hood, MoonHoodSpec);
            // 50% chance to also show the moon blade in right hand
            if (_random.Prob(0.5f))
                AddLayerIfMissing(ent, HallucinationKey.Blade, MoonBladeSpec);
        }

        _affected.Add(uid);
    }

    private void AddLayerIfMissing(Entity<SpriteComponent?> ent, HallucinationKey key, SpriteSpecifier spec)
    {
        if (_sprite.LayerMapTryGet(ent, key, out _, false))
            return;
        var layer = _sprite.AddLayer(ent, spec);
        _sprite.LayerMapSet(ent, key, layer);
    }
}
