using System.Linq;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.StatusEffectNew;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Heretic.Paths.Moon;

/// <summary>
/// delusion/preset/moon: пока на локальном игроке висит бред, все остальные люди
/// выглядят для него лунными массами. Другие игроки ничего не замечают.
/// </summary>
public sealed class HereticMoonDelusionSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly SpriteSpecifier.Rsi MoonMassSpec = new(
        new ResPath("Imperial/heretic/moon_mass.rsi"),
        "moon_mass");

    private enum MoonDelusionKey { Mass }

    private bool _active;

    /// <summary>Какие слои мы спрятали у каждой сущности, чтобы вернуть ровно их.</summary>
    private readonly Dictionary<EntityUid, List<int>> _hidden = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticMoonDelusionStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<HereticMoonDelusionStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<HereticMoonDelusionStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerAttachedEvent>>(OnAttached);
        SubscribeLocalEvent<HereticMoonDelusionStatusEffectComponent, StatusEffectRelayedEvent<LocalPlayerDetachedEvent>>(OnDetached);
    }

    private void OnApplied(Entity<HereticMoonDelusionStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (_player.LocalEntity == args.Target)
            Activate();
    }

    private void OnRemoved(Entity<HereticMoonDelusionStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (_player.LocalEntity == args.Target)
            Deactivate();
    }

    private void OnAttached(Entity<HereticMoonDelusionStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerAttachedEvent> args)
        => Activate();

    private void OnDetached(Entity<HereticMoonDelusionStatusEffectComponent> ent, ref StatusEffectRelayedEvent<LocalPlayerDetachedEvent> args)
        => Deactivate();

    /// <summary>
    /// Люди, появившиеся в поле зрения уже во время бреда, тоже становятся лунными массами.
    /// На запуск компонента подписаться нельзя — его уже слушает бред еретика.
    /// </summary>
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_active)
            return;

        var query = EntityQueryEnumerator<HumanoidProfileComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (uid != _player.LocalEntity && !_hidden.ContainsKey(uid))
                Disguise(uid);
        }
    }

    private void Activate()
    {
        if (_active)
            return;

        _active = true;
        var query = EntityQueryEnumerator<HumanoidProfileComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (uid != _player.LocalEntity)
                Disguise(uid);
        }
    }

    private void Deactivate()
    {
        if (!_active)
            return;

        _active = false;
        foreach (var (uid, layers) in _hidden)
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite))
                continue;

            Entity<SpriteComponent?> ent = (uid, sprite);
            if (_sprite.LayerMapTryGet(ent, MoonDelusionKey.Mass, out var mass, false))
                _sprite.RemoveLayer(ent, mass);

            foreach (var index in layers)
            {
                if (index < sprite.AllLayers.Count())
                    _sprite.LayerSetVisible(ent, index, true);
            }
        }

        _hidden.Clear();
    }

    /// <summary>make_delusion_image(): прячем тело и рисуем поверх лунную массу.</summary>
    private void Disguise(EntityUid uid)
    {
        if (_hidden.ContainsKey(uid) || !TryComp<SpriteComponent>(uid, out var sprite))
            return;

        Entity<SpriteComponent?> ent = (uid, sprite);
        var hidden = new List<int>();
        var index = 0;
        foreach (var layer in sprite.AllLayers)
        {
            if (layer.Visible)
            {
                _sprite.LayerSetVisible(ent, index, false);
                hidden.Add(index);
            }

            index++;
        }

        var mass = _sprite.AddLayer(ent, MoonMassSpec);
        _sprite.LayerMapSet(ent, MoonDelusionKey.Mass, mass);
        _hidden[uid] = hidden;
    }
}
