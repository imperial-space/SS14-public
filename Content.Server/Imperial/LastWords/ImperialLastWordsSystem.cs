using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Imperial.LastWords.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.LastWords;
using Content.Shared.Imperial.LastWords.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles.Jobs;
using Robust.Server.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.LastWords;

/// <summary>
/// Последние слова перед смертью. Учитываются только тела игроков (<see cref="ImperialLastWordsComponent"/>
/// вешается при вселении игрока в моба). Фраза, сказанная в пределах окна до критического состояния, вместе с именем
/// и должностью на этот момент записывается в отдельную сущность (<see cref="ImperialLastWordsRecordComponent"/>).
/// В конце раунда записи рассылаются всем клиентам: сначала умершие, потом выжившие.
/// </summary>
public sealed class ImperialLastWordsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly PvsOverrideSystem _pvsOverride = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<ImperialLastWordsComponent, EntitySpokeEvent>(OnSpoke);
        SubscribeLocalEvent<ImperialLastWordsComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (HasComp<MobStateComponent>(args.Entity))
            EnsureComp<ImperialLastWordsComponent>(args.Entity);
    }

    private void OnSpoke(EntityUid uid, ImperialLastWordsComponent comp, EntitySpokeEvent args)
    {
        if (string.IsNullOrWhiteSpace(args.Message))
            return;

        comp.Recent = args.Message;
        comp.RecentAt = _timing.CurTime;
    }

    private void OnMobStateChanged(Entity<ImperialLastWordsComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical)
            return;

        // Перед этим критом ничего не сказал — прежняя запись (с прошлого крита) больше не последние слова.
        if (ent.Comp.Recent == null || _timing.CurTime - ent.Comp.RecentAt > ent.Comp.Window)
        {
            if (ent.Comp.Record is { } old)
                QueueDel(old);
            ent.Comp.Record = null;
            return;
        }

        var record = ent.Comp.Record ?? Spawn(null, MapCoordinates.Nullspace);
        ent.Comp.Record = record;
        var data = EnsureComp<ImperialLastWordsRecordComponent>(record);
        data.Phrase = ent.Comp.Recent;
        data.Name = Name(ent);
        data.Job = JobOf(ent);
        data.Body = ent;
    }

    private void OnRoundEnd(RoundEndMessageEvent args)
    {
        var entries = new List<ImperialLastWordsEntry>();
        var query = EntityQueryEnumerator<ImperialLastWordsRecordComponent>();
        while (query.MoveNext(out var data))
        {
            var entry = new ImperialLastWordsEntry
            {
                Phrase = data.Phrase,
                Name = data.Name,
                Job = data.Job,
            };

            if (data.Body is { } body && !TerminatingOrDeleted(body))
            {
                entry.Dead = _mobState.IsDead(body);
                entry.Entity = GetNetEntity(body);

                // Как в манифесте игроков: тело видно всем клиентам, чтобы нарисовать спрайт.
                _pvsOverride.AddGlobalOverride(body);
            }
            else
            {
                // Тела нет (разорвано, сожжено) — не выжил.
                entry.Dead = true;
            }

            entries.Add(entry);
        }

        // Сначала умершие, потом те, кто был при смерти, но выжил.
        entries = entries.OrderByDescending(e => e.Dead).ToList();
        RaiseNetworkEvent(new ImperialLastWordsEvent(_ticker.RoundId, entries));
    }

    /// <summary>Должность: работа разума, иначе должность на ID-карте.</summary>
    private string JobOf(EntityUid uid)
    {
        if (_mind.TryGetMind(uid, out var mindId, out _) && _jobs.MindTryGetJobName(mindId, out var job))
            return job;

        if (_idCard.TryFindIdCard(uid, out var card) && !string.IsNullOrEmpty(card.Comp.LocalizedJobTitle))
            return card.Comp.LocalizedJobTitle;

        return Loc.GetString("imperial-last-words-unknown-job");
    }
}
