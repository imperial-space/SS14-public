using Content.Server.Administration.Logs;
using Content.Server.GameTicking.Rules;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Player;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Systems;

/// <summary>
/// Пока правило активно, тела игроков становятся необратимыми строго при переходе в состояние смерти.
/// Критическое состояние намеренно не затрагивается.
/// </summary>
public sealed class DeathNoteRoundUnrevivableRuleSystem
    : GameRuleSystem<DeathNoteRoundUnrevivableRuleComponent>
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActorComponent, ComponentStartup>(OnActorStartup);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    protected override void Started(
        EntityUid uid,
        DeathNoteRoundUnrevivableRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        // Учитываем и игроков, которые уже были мертвы на момент запуска правила.
        var query = EntityQueryEnumerator<DeathNotePlayerBodyComponent, MobStateComponent>();
        while (query.MoveNext(out var player, out _, out var mobState))
        {
            if (mobState.CurrentState == MobState.Dead)
                MakeUnrevivable(player);
        }

        _adminLog.Add(
            LogType.Action,
            LogImpact.Extreme,
            $"DeathNoteRoundUnrevivable game rule started: player deaths are irreversible while it is active.");
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead ||
            !HasComp<DeathNotePlayerBodyComponent>(args.Target) ||
            !IsActive())
        {
            return;
        }

        MakeUnrevivable(args.Target);
        _adminLog.Add(
            LogType.Action,
            LogImpact.Extreme,
            $"{args.Target:player} became unrevivable after entering Dead under the Death Note game rule.");
    }

    private void OnActorStartup(Entity<ActorComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<DeathNotePlayerBodyComponent>(ent.Owner);
    }

    public bool IsActive()
    {
        var query = QueryActiveRules();
        return query.MoveNext(out _, out _, out _, out _);
    }

    private void MakeUnrevivable(EntityUid uid)
    {
        if (HasComp<UnrevivableComponent>(uid))
            return;

        var component = EnsureComp<UnrevivableComponent>(uid);
        component.Analyzable = false;
        component.Cloneable = false;
        component.ReasonMessage = "death-note-unrevivable";
        Dirty(uid, component);
    }
}
