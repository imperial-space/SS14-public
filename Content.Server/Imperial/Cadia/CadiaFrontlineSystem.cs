using Content.Server.Afk;
using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Shared.Chat;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Порядок на открытом ивенте: вайтлист командных ролей, снятие AFK с поля, защита только что появившихся.
/// </summary>
public sealed class CadiaFrontlineSystem : EntitySystem
{
    [Dependency] private readonly IAfkManager _afk = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceManager _resources = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly Content.Server.GameTicking.GameTicker _ticker = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly CadiaDefenseRuleSystem _rule = default!;

    private static readonly ResPath WhitelistPath = new("/cadia_whitelist.txt");
    private static readonly TimeSpan SpawnProtection = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan AfkWarn = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan AfkRemove = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan AfkTick = TimeSpan.FromSeconds(5);

    private readonly HashSet<string> _whitelist = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<EntityUid, TimeSpan> _idleSince = new();
    private readonly HashSet<EntityUid> _warned = new();
    private TimeSpan _nextAfk;
    private bool _lobbyCleaned;

    public override void Initialize()
    {
        base.Initialize();
        LoadWhitelist();
        SubscribeLocalEvent<CadiaSpawnProtectionComponent, BeforeDamageChangedEvent>(OnProtectedDamage);
        SubscribeLocalEvent<CadiaSpawnProtectionComponent, ShotAttemptedEvent>(OnProtectedShot);
        SubscribeLocalEvent<CadiaSpawnProtectionComponent, AttackAttemptEvent>(OnProtectedAttack);
    }

    // ═══════════════════════ Вайтлист ═══════════════════════

    private void LoadWhitelist()
    {
        _whitelist.Clear();
        if (!_resources.UserData.Exists(WhitelistPath))
            return;
        foreach (var line in _resources.UserData.ReadAllText(WhitelistPath).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            _whitelist.Add(line);
    }

    private void SaveWhitelist()
    {
        _resources.UserData.WriteAllText(WhitelistPath, string.Join('\n', _whitelist));
    }

    public IReadOnlyCollection<string> Whitelist => _whitelist;

    public bool IsWhitelisted(ICommonSession session) => _whitelist.Contains(session.Name);

    public bool AddToWhitelist(string ckey)
    {
        if (!_whitelist.Add(ckey.Trim()))
            return false;
        SaveWhitelist();
        return true;
    }

    public bool RemoveFromWhitelist(string ckey)
    {
        if (!_whitelist.Remove(ckey.Trim()))
            return false;
        SaveWhitelist();
        return true;
    }

    // ═══════════════════════ Защита при появлении ═══════════════════════

    public void Protect(EntityUid mob, TimeSpan? duration = null)
    {
        var protection = EnsureComp<CadiaSpawnProtectionComponent>(mob);
        protection.Until = _timing.CurTime + (duration ?? SpawnProtection);
        Dirty(mob, protection);
    }

    private void OnProtectedDamage(Entity<CadiaSpawnProtectionComponent> ent, ref BeforeDamageChangedEvent args)
    {
        args.Cancelled = true;
    }

    // Защита для тех, кто ещё не успел осмотреться: открыл огонь — значит, уже в бою.
    private void OnProtectedShot(Entity<CadiaSpawnProtectionComponent> ent, ref ShotAttemptedEvent args)
    {
        RemCompDeferred<CadiaSpawnProtectionComponent>(ent);
    }

    private void OnProtectedAttack(EntityUid uid, CadiaSpawnProtectionComponent comp, AttackAttemptEvent args)
    {
        RemCompDeferred<CadiaSpawnProtectionComponent>(uid);
    }

    // ═══════════════════════ AFK ═══════════════════════

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        if (!_lobbyCleaned)
        {
            _lobbyCleaned = true;
            _ticker.RemoveEventLobbyBackgrounds();
        }

        var protectedMobs = EntityQueryEnumerator<CadiaSpawnProtectionComponent>();
        while (protectedMobs.MoveNext(out var uid, out var protection))
        {
            if (now >= protection.Until)
                RemCompDeferred<CadiaSpawnProtectionComponent>(uid);
        }

        if (now < _nextAfk)
            return;
        _nextAfk = now + AfkTick;
        if (!_rule.TryGetActiveWar(out var war) || war.Phase == CadiaWarPhase.Ended)
            return;

        var members = EntityQueryEnumerator<CadiaFactionMemberComponent>();
        while (members.MoveNext(out var uid, out var member))
        {
            // Лежащий в критическом состоянии ждёт медика, а не бездельничает.
            if (member.Role == null || !_mobState.IsAlive(uid) || !IsIdlePlayer(uid))
            {
                _idleSince.Remove(uid);
                _warned.Remove(uid);
                continue;
            }

            if (!_idleSince.TryGetValue(uid, out var since))
            {
                _idleSince[uid] = now;
                continue;
            }

            var idle = now - since;
            if (idle >= AfkRemove)
            {
                _idleSince.Remove(uid);
                _warned.Remove(uid);
                Message(uid, Loc.GetString("cadia-afk-removed"));
                _rule.RemoveMobAndFreeSeat(uid);
            }
            else if (idle >= AfkWarn && _warned.Add(uid))
            {
                _popup.PopupEntity(Loc.GetString("cadia-afk-warning"), uid, uid, PopupType.LargeCaution);
                Message(uid, Loc.GetString("cadia-afk-warning"));
            }
        }
    }

    /// <summary>Игрок бездействует или вышел с сервера. Тела без игрока (призванные демоны) не считаются.</summary>
    private bool IsIdlePlayer(EntityUid uid)
    {
        if (TryComp<ActorComponent>(uid, out var actor))
            return _afk.IsAfk(actor.PlayerSession);
        return TryComp<MindContainerComponent>(uid, out var container) &&
               container.Mind is { } mindId && TryComp<MindComponent>(mindId, out var mind) && mind.UserId != null;
    }

    private void Message(EntityUid uid, string text)
    {
        if (TryComp<ActorComponent>(uid, out var actor))
            _chatManager.ChatMessageToOne(ChatChannel.Server, text, text, default, false, actor.PlayerSession.Channel, Color.FromHex("#ff9a3c"));
    }
}
