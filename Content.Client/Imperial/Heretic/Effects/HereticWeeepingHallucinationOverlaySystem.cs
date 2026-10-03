using Content.Shared.Humanoid;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Shared.StatusEffectNew;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Heretic.Effects;

/// <summary>
/// Пока на локальном игроке висит галлюцинация Плачущих, остальные гуманоиды выглядят еретиками.
/// </summary>
public sealed class HereticWeeepingHallucinationOverlaySystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

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

        Activate(ent.Comp);
    }

    private void OnRemoved(Entity<HereticWeeepingHallucinationStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (_player.LocalEntity != args.Target)
            return;

        Deactivate();
    }

    private void OnPlayerAttached(Entity<HereticWeeepingHallucinationStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerAttachedEvent> args)
    {
        Activate(ent.Comp);
    }

    private void OnPlayerDetached(Entity<HereticWeeepingHallucinationStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerDetachedEvent> args)
    {
        Deactivate();
    }

    private void OnHumanoidAdded(Entity<HumanoidProfileComponent> ent, ref ComponentStartup args)
    {
        if (_player.LocalEntity is not { } player || ent.Owner == player)
            return;

        if (!_statusEffects.TryEffectsWithComp<HereticWeeepingHallucinationStatusEffectComponent>(player, out var effects))
            return;

        foreach (var effect in effects)
        {
            AddLayers(ent, effect.Comp1);
            return;
        }
    }

    private void Activate(HereticWeeepingHallucinationStatusEffectComponent hallucination)
    {
        var query = EntityQueryEnumerator<HumanoidProfileComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (uid == _player.LocalEntity)
                continue;

            AddLayers(uid, hallucination);
        }
    }

    private void Deactivate()
    {
        var query = EntityQueryEnumerator<HereticWeepingHallucinationVisualsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
        {
            Entity<SpriteComponent?> ent = (uid, sprite);
            RemoveLayerIfExists(ent, HereticWeepingHallucinationLayers.Armor);
            RemoveLayerIfExists(ent, HereticWeepingHallucinationLayers.Hood);
            RemoveLayerIfExists(ent, HereticWeepingHallucinationLayers.Blade);
            RemCompDeferred<HereticWeepingHallucinationVisualsComponent>(uid);
        }
    }

    private void RemoveLayerIfExists(Entity<SpriteComponent?> ent, HereticWeepingHallucinationLayers key)
    {
        if (_sprite.LayerMapTryGet(ent, key, out var layer, false))
            _sprite.RemoveLayer(ent, layer);
    }

    private void AddLayers(EntityUid uid, HereticWeeepingHallucinationStatusEffectComponent hallucination)
    {
        if (HasComp<HereticWeepingHallucinationVisualsComponent>(uid) || !TryComp<SpriteComponent>(uid, out var sprite))
            return;

        AddComp<HereticWeepingHallucinationVisualsComponent>(uid);
        Entity<SpriteComponent?> ent = (uid, sprite);

        // Случайный вариант: обычная роба еретика или снаряжение пути Луны.
        if (_random.Prob(0.5f))
        {
            AddLayerIfMissing(ent, HereticWeepingHallucinationLayers.Armor, hallucination.DefaultArmorSprite);
            AddLayerIfMissing(ent, HereticWeepingHallucinationLayers.Hood, hallucination.DefaultHoodSprite);
            return;
        }

        AddLayerIfMissing(ent, HereticWeepingHallucinationLayers.Armor, hallucination.MoonArmorSprite);
        AddLayerIfMissing(ent, HereticWeepingHallucinationLayers.Hood, hallucination.MoonHoodSprite);
        if (_random.Prob(hallucination.MoonBladeChance))
            AddLayerIfMissing(ent, HereticWeepingHallucinationLayers.Blade, hallucination.MoonBladeSprite);
    }

    private void AddLayerIfMissing(Entity<SpriteComponent?> ent, HereticWeepingHallucinationLayers key, SpriteSpecifier spec)
    {
        if (_sprite.LayerMapTryGet(ent, key, out _, false))
            return;

        var layer = _sprite.AddLayer(ent, spec);
        _sprite.LayerMapSet(ent, key, layer);
    }
}
