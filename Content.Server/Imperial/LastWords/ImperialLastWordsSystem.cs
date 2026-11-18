using System.Linq;
using Content.Server.GameTicking;
using Content.Shared.Access.Systems;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.LastWords;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles.Jobs;
using Robust.Server.GameStates;
using Robust.Shared.Player;

namespace Content.Server.Imperial.LastWords;

/// <summary>
/// Мост между событиями мира и <see cref="ImperialLastWordsManager"/>: передаёт ему речь игроков и переход в
/// критическое состояние, а в конце раунда собирает фразы (должность, имя, жив ли) и отдаёт менеджеру на рассылку.
/// </summary>
public sealed class ImperialLastWordsSystem : EntitySystem
{
    [Dependency] private readonly ImperialLastWordsManager _manager = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly PvsOverrideSystem _pvsOverride = default!;

    public override void Initialize()
    {
        base.Initialize();
        _manager.Initialize();

        // Оба события рассылаются широковещательно — подписка без компонента не мешает чужим подпискам.
        SubscribeLocalEvent<EntitySpokeEvent>(OnSpoke);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RoundEndMessageEvent>(OnRoundEnd);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _manager.Clear());
    }

    private void OnSpoke(EntitySpokeEvent args)
    {
        // Только игроки.
        if (HasComp<ActorComponent>(args.Source))
            _manager.OnSpoke(args.Source, args.Message);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Critical)
            _manager.OnCritical(args.Target);
    }

    private void OnRoundEnd(RoundEndMessageEvent args)
    {
        var message = new MsgImperialLastWords { RoundId = _ticker.RoundId };
        foreach (var (uid, phrase) in _manager.LastWords)
        {
            var exists = !TerminatingOrDeleted(uid);
            var entry = new ImperialLastWordsEntry
            {
                Phrase = phrase,
                // Удалённое тело (сгорело, разорвано) — тоже мёртв.
                Dead = !exists || _mobState.IsDead(uid),
            };

            if (exists)
            {
                entry.Name = Name(uid);
                entry.Job = JobOf(uid);
                entry.Entity = GetNetEntity(uid);

                // Как в манифесте игроков: тело видно всем клиентам, чтобы нарисовать спрайт.
                _pvsOverride.AddGlobalOverride(uid);
            }
            else
            {
                entry.Job = Loc.GetString("imperial-last-words-unknown-job");
            }

            message.Entries.Add(entry);
        }

        // Сначала умершие, потом те, кто был при смерти, но выжил.
        message.Entries = message.Entries.OrderByDescending(e => e.Dead).ToList();
        _manager.Send(message);
    }

    /// <summary>Должность: работа разума, иначе должность на ID-карте, иначе имя персонажа.</summary>
    private string JobOf(EntityUid uid)
    {
        if (_mind.TryGetMind(uid, out var mindId, out _) && _jobs.MindTryGetJobName(mindId, out var job))
            return job;

        if (_idCard.TryFindIdCard(uid, out var card) && !string.IsNullOrEmpty(card.Comp.LocalizedJobTitle))
            return card.Comp.LocalizedJobTitle;

        return Loc.GetString("imperial-last-words-unknown-job");
    }
}
