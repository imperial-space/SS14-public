using Content.Server.Imperial.SyndieBattle.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.SyndieBattle.Systems;

public sealed class SyndieBattleSpawnProtectionSystem : EntitySystem
{
    [Dependency] private readonly SharedGodmodeSystem _godmodeSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SyndieBattleSpawnProtectionComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SyndieBattleSpawnProtectionComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_timing.CurTime < component.EndTime)
                continue;

            RemCompDeferred<SyndieBattleSpawnProtectionComponent>(uid);
            _popupSystem.PopupEntity(Loc.GetString(component.EndMessage), uid, uid);
        }
    }

    public void Apply(EntityUid uid, TimeSpan duration, LocId startMessage, LocId endMessage)
    {
        var isNew = !HasComp<SyndieBattleSpawnProtectionComponent>(uid);
        var protection = EnsureComp<SyndieBattleSpawnProtectionComponent>(uid);
        if (isNew)
        {
            protection.AddedGodmode = !HasComp<GodmodeComponent>(uid);
            protection.AddedPacified = !HasComp<PacifiedComponent>(uid);
        }

        protection.EndTime = _timing.CurTime + duration;
        protection.EndMessage = endMessage;

        if (isNew && protection.AddedGodmode)
            _godmodeSystem.EnableGodmode(uid);
        EnsureComp<PacifiedComponent>(uid);
        _popupSystem.PopupEntity(Loc.GetString(startMessage, ("seconds", (int)Math.Ceiling(duration.TotalSeconds))), uid, uid);
    }

    private void OnShutdown(EntityUid uid, SyndieBattleSpawnProtectionComponent component, ComponentShutdown args)
    {
        if (TerminatingOrDeleted(uid))
            return;

        if (component.AddedGodmode)
            _godmodeSystem.DisableGodmode(uid);
        if (component.AddedPacified)
            RemCompDeferred<PacifiedComponent>(uid);
    }
}
