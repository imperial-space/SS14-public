using Content.Shared.Imperial.Medieval.Surrender;
using Content.Server.Actions;
using Content.Shared.CombatMode.Pacification;
using Robust.Shared.Timing;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Content.Shared.Coordinates;

namespace Content.Server.Imperial.Medieval.Surrender;

public sealed class SurrenderSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly IGameTiming _tick = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<CanSurrenderComponent, ComponentInit>(CompInit);
        SubscribeLocalEvent<CanSurrenderComponent, MedievalSurrenderEvent>(Surrender);
    }
    private void CompInit(EntityUid uid, CanSurrenderComponent component, ComponentInit args)
    {
        if (HasComp<PacifiedComponent>(uid))
            return;
        _actions.AddAction(uid, "ActionSurrender");
    }
    private void Surrender(EntityUid uid, CanSurrenderComponent component, MedievalSurrenderEvent args)
    {
        if (args.Handled)
            return;
        if (HasComp<PacifiedComponent>(uid))
            return;
        //_actions.SetCooldown((args.Action.Owner, args.Action.Comp), component.SurrenderTime); poshel nahui
        EnsureComp<PacifiedComponent>(uid);
        component.SurrenderActive = true;
        component.Unsurrender = _tick.CurTime + component.SurrenderTime;
        _appearance.SetData(uid, SurrenderVisuals.Key, true);
        _audio.PlayPvs(component.Sound, uid.ToCoordinates());
        Dirty(uid, component);
        args.Handled = true;
    }
    public override void Update(float delta)
    {
        foreach (var component in EntityQuery<CanSurrenderComponent>())
        {
            if (!component.SurrenderActive)
                continue;
            if (_tick.CurTime < component.Unsurrender)
                continue;
            RemComp<PacifiedComponent>(component.Owner);
            component.SurrenderActive = false;
            _appearance.SetData(component.Owner, SurrenderVisuals.Key, false);
            Dirty(component.Owner, component);
        }
    }
}

