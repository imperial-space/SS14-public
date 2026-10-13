using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Xenobiology;

/// <summary>
/// Модификаторы скорости ксенобиологии: блюспейс/сепия плитка (turf slowdown), смола, сепиевый стабилизированный экстракт и т.д.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SlimeSpeedModifierComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<string, float> Modifiers = new();
}

public sealed class SlimeSpeedModifierSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeSpeedModifierComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);
        SubscribeLocalEvent<SlimeSpeedModifierComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<SlimeSpeedModifierComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnRefresh(Entity<SlimeSpeedModifierComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.LifeStage >= ComponentLifeStage.Stopping)
            return;

        foreach (var modifier in ent.Comp.Modifiers.Values)
        {
            args.ModifySpeed(modifier);
        }
    }

    private void OnState(Entity<SlimeSpeedModifierComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _speed.RefreshMovementSpeedModifiers(ent);
    }

    private void OnShutdown(Entity<SlimeSpeedModifierComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent))
            _speed.RefreshMovementSpeedModifiers(ent);
    }

    /// <summary>Ставит или снимает (модификатор 1) именованный множитель скорости.</summary>
    public void SetModifier(EntityUid uid, string key, float modifier)
    {
        var remove = MathHelper.CloseTo(modifier, 1f);
        if (!TryComp<SlimeSpeedModifierComponent>(uid, out var comp))
        {
            if (remove)
                return;
            comp = AddComp<SlimeSpeedModifierComponent>(uid);
        }

        if (remove)
        {
            if (!comp.Modifiers.Remove(key))
                return;
        }
        else
        {
            if (comp.Modifiers.TryGetValue(key, out var old) && MathHelper.CloseTo(old, modifier))
                return;
            comp.Modifiers[key] = modifier;
        }

        if (comp.Modifiers.Count == 0)
        {
            RemCompDeferred<SlimeSpeedModifierComponent>(uid);
            return;
        }

        Dirty(uid, comp);
        _speed.RefreshMovementSpeedModifiers(uid);
    }

    public bool HasModifier(EntityUid uid, string key)
    {
        return TryComp<SlimeSpeedModifierComponent>(uid, out var comp) && comp.Modifiers.ContainsKey(key);
    }
}
