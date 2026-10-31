using Content.Server.Chat.Systems;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Голоса войны. Абаддон и тёмный апостол говорят с флотом по громкой связи флагмана, Лорд-кастелян Крид — с гвардией
/// по воксу, а изредка Абаддон врывается в вокс защитников. Реплики по фазам, раз в несколько минут, без повторов подряд.
/// На события войны — павший пилон, взятый сектор, гибель командующего, прибытие Ультрамаринов — Крид и Абаддон
/// отвечают сразу, каждый своей стороне.
/// </summary>
public sealed class CadiaProclamationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly CadiaDefenseRuleSystem _rule = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    private static readonly SoundSpecifier ChaosSound = new SoundPathSpecifier("/Audio/Imperial/Cadia/Ambience/dark_chant.ogg");
    private static readonly SoundSpecifier VoxSound = new SoundPathSpecifier("/Audio/Imperial/Cadia/Ambience/vox_static.ogg");
    private static readonly Color ChaosColor = Color.FromHex("#ff4a3a");
    private static readonly Color ImperiumColor = Color.FromHex("#e8c66a");

    /// <summary>Голос: ключ отправителя, начало ключей реплик, сколько реплик на фазу, кто слышит, звук и цвет.</summary>
    private readonly record struct Voice(string Sender, string Lines, int Count, CadiaFaction Hears, SoundSpecifier Sound, Color Color);

    private static readonly Voice[] Voices =
    {
        new("cadia-voice-abaddon-sender", "cadia-voice-abaddon", 3, CadiaFaction.Chaos, ChaosSound, ChaosColor),
        new("cadia-voice-apostle-sender", "cadia-voice-apostle", 3, CadiaFaction.Chaos, ChaosSound, ChaosColor),
        new("cadia-voice-creed-sender", "cadia-voice-creed", 3, CadiaFaction.Imperium, VoxSound, ImperiumColor),
        new("cadia-voice-taunt-sender", "cadia-voice-taunt", 2, CadiaFaction.Imperium, VoxSound, ChaosColor),
    };

    private static readonly TimeSpan MinGap = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan MaxGap = TimeSpan.FromMinutes(5);

    private TimeSpan _next;
    private CadiaWarPhase? _phase;
    private readonly Dictionary<string, string> _last = new();

    // ─── Реакции на события ───
    private static readonly TimeSpan ReactionGap = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ReactionCheck = TimeSpan.FromSeconds(1);
    private static readonly HashSet<string> ImperialCommanders = new() { "CadiaLordCastellan" };
    private static readonly HashSet<string> ChaosCommanders = new() { "CadiaChaosLord", "CadiaAbaddon" };
    private TimeSpan _nextReaction;
    private TimeSpan _nextCheck;
    private int _pylons = -1;
    private readonly Dictionary<EntityUid, CadiaFaction?> _sectors = new();
    private readonly HashSet<EntityUid> _fallenCommanders = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_rule.TryGetActiveWar(out var war) || war.Phase >= CadiaWarPhase.Ended)
        {
            _phase = null;
            _pylons = -1;
            _sectors.Clear();
            _fallenCommanders.Clear();
            return;
        }
        var now = _timing.CurTime;
        // Новая фаза — первое слово через минуту, когда схлынут объявления фазы.
        if (_phase != war.Phase)
        {
            if (_phase != null && war.Phase == CadiaWarPhase.Assault)
                React("assault", force: true);
            _phase = war.Phase;
            _next = now + TimeSpan.FromMinutes(1);
            return;
        }
        if (now >= _nextCheck)
        {
            _nextCheck = now + ReactionCheck;
            CheckEvents(war);
        }
        if (now < _next)
            return;
        _next = now + _random.Next(MinGap, MaxGap);

        // Крид и Абаддон говорят всегда, перехват — реже.
        var voice = _random.Prob(0.2f) ? Voices[3] : _random.Pick(new[] { Voices[0], Voices[1], Voices[2], Voices[2] });
        Speak(voice, war.Phase);
    }

    /// <summary>Что случилось с прошлой проверки: пилоны, сектора, командующие.</summary>
    private void CheckEvents(CadiaWarStateComponent war)
    {
        if (_pylons >= 0 && war.PylonsAlive < _pylons)
            React("pylon");
        _pylons = war.PylonsAlive;

        var sectors = EntityQueryEnumerator<CadiaSectorComponent>();
        while (sectors.MoveNext(out var uid, out var sector))
        {
            if (_sectors.TryGetValue(uid, out var owner) && owner != sector.Owner && sector.Owner is { } side)
                React(side == CadiaFaction.Imperium ? "sector-imperium" : "sector-chaos");
            _sectors[uid] = sector.Owner;
        }

        var members = EntityQueryEnumerator<CadiaFactionMemberComponent>();
        while (members.MoveNext(out var uid, out var member))
        {
            if (member.Role is not { } role || _fallenCommanders.Contains(uid) || !_mobState.IsDead(uid))
                continue;
            if (ImperialCommanders.Contains(role))
                React("commander-imperium");
            else if (ChaosCommanders.Contains(role))
                React("commander-chaos");
            else
                continue;
            _fallenCommanders.Add(uid);
        }
    }

    /// <summary>Крид отвечает гвардии, Абаддон — своим; не чаще раза в двадцать секунд, если это не прибытие подкреплений.</summary>
    private void React(string what, bool force = false)
    {
        var now = _timing.CurTime;
        if (!force && now < _nextReaction)
            return;
        _nextReaction = now + ReactionGap;
        Say(Voices[2], $"cadia-react-{what}-imperium");
        Say(Voices[0], $"cadia-react-{what}-chaos");
    }

    /// <summary>Случайная реплика из набора «ключ-1», «ключ-2»… голосом этого командира.</summary>
    private void Say(Voice voice, string prefix)
    {
        var options = new List<string>();
        for (var i = 1; i <= 3; i++)
        {
            if (Loc.HasString($"{prefix}-{i}"))
                options.Add($"{prefix}-{i}");
        }
        if (options.Count > 0)
            Send(voice, _random.Pick(options));
    }

    private void Send(Voice voice, string line)
    {
        var filter = Filter.Empty().AddWhere(session =>
            session.AttachedEntity is { } mob && TryComp<CadiaFactionMemberComponent>(mob, out var member) &&
            member.Role != null && member.Faction == voice.Hears);
        _chat.DispatchFilteredAnnouncement(filter, Loc.GetString(line), sender: Loc.GetString(voice.Sender),
            playSound: true, announcementSound: voice.Sound, colorOverride: voice.Color);
    }

    private void Speak(Voice voice, CadiaWarPhase phase)
    {
        var phaseKey = phase.ToString().ToLowerInvariant();
        var options = new List<string>();
        for (var i = 1; i <= voice.Count; i++)
        {
            var key = $"{voice.Lines}-{phaseKey}-{i}";
            if (Loc.HasString(key) && (!_last.TryGetValue(voice.Lines, out var last) || last != key))
                options.Add(key);
        }
        if (options.Count == 0)
            return;
        var line = _random.Pick(options);
        _last[voice.Lines] = line;
        Send(voice, line);
    }
}
