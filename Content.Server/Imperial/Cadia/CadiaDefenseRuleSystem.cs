using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Station.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage.Systems;
using Content.Shared.Dataset;
using Content.Shared.Destructible;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// «Оборона Кадии»: Империум держит пилоны и цитадель Касра, Хаос штурмует.
/// Подготовка (пилоны под полем) → орбитальный обстрел → штурм (поле падает, высаживаются Ультрамарины) →
/// развязка (Чёрный легион). Хаос побеждает, снеся все пилоны или удержав цитадель; Империум — выстояв до конца.
/// Бойцы — призрачные роли; места погибших возвращаются волнами подкреплений своей стороны.
/// </summary>
public sealed class CadiaDefenseRuleSystem : GameRuleSystem<CadiaDefenseRuleComponent>
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StationSpawningSystem _spawning = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PvsOverrideSystem _pvs = default!;
    [Dependency] private readonly CadiaPsykerSystem _psykers = default!;
    [Dependency] private readonly CadiaAstartesSystem _astartes = default!;
    [Dependency] private readonly Content.Server.Explosion.EntitySystems.ExplosionSystem _explosion = default!;
    [Dependency] private readonly CadiaStatsSystem _stats = default!;
    [Dependency] private readonly CadiaFrontlineSystem _frontline = default!;
    [Dependency] private readonly CadiaSquadSystem _squads = default!;
    [Dependency] private readonly CadiaEvacuationSystem _evac = default!;
    [Dependency] private readonly Content.Shared.Actions.SharedActionsSystem _actions = default!;
    [Dependency] private readonly Content.Server.Ghost.GhostSystem _ghost = default!;
    [Dependency] private readonly Content.Shared.Mind.SharedMindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    private static readonly Color ImperiumColor = Color.FromHex("#e8c66a");
    private static readonly Color ChaosColor = Color.FromHex("#d23b2a");
    private static readonly TimeSpan TickRate = TimeSpan.FromSeconds(1);
    private static readonly Color EyeOfTerror = Color.FromHex("#7a1f24");
    private const float SkyStep = 0.02f;
    private const int DefaultCommanderMedals = 3;
    private static readonly EntProtoId BriefingAction = "ActionCadiaBriefing";
    private const int FairSideHysteresis = 2;
    private readonly Dictionary<EntityUid, Color> _skyBase = new();
    private readonly List<EntityUid> _pendingRemovals = new();
    private float _skyShare;
    private float _skyTarget;

    private readonly HashSet<Entity<CadiaFactionMemberComponent>> _inRange = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CadiaFactionMemberComponent, GhostRoleSpawnerUsedEvent>(OnSpawnerUsed);
        SubscribeLocalEvent<CadiaBriefingPendingComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<CadiaFactionMemberComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<CadiaPylonComponent, BeforeDamageChangedEvent>(OnPylonBeforeDamage);
        SubscribeLocalEvent<CadiaPylonComponent, DestructionEventArgs>(OnPylonDestroyed);
        SubscribeLocalEvent<CadiaPylonComponent, MapInitEvent>(OnPylonMapInit);
        SubscribeLocalEvent<CadiaFactionMemberComponent, CadiaBriefingActionEvent>(OnBriefingAction);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnAnyAttached);
    }

    // ───────────────────────── Жизненный цикл ─────────────────────────

    protected override void Started(EntityUid uid, CadiaDefenseRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var state = Spawn(null, MapCoordinates.Nullspace);
        _meta.SetEntityName(state, "Cadia war state");
        var war = EnsureComp<CadiaWarStateComponent>(state);
        _pvs.AddGlobalOverride(state);
        component.State = state;

        RecountPylons(war);
        war.WarpPower = component.StartWarp;
        _skyBase.Clear();
        _skyShare = 0f;
        _skyTarget = 0f;
        _stats.Reset();
        component.NextTick = _timing.CurTime;
        SetPhase((uid, component), CadiaWarPhase.Preparation);
        ShowIntro(component, false);
    }

    protected override void Ended(EntityUid uid, CadiaDefenseRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        foreach (var spawner in component.Spawners.Values)
            QueueDel(spawner);
        component.Spawners.Clear();

        _skyShare = 0f;
        ApplySky();
        _skyBase.Clear();

        if (component.State is { } state)
            QueueDel(state);
        component.State = null;
    }

    protected override void ActiveTick(EntityUid uid, CadiaDefenseRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        // Менять тело игрока внутри события входа в тело нельзя: снимаем на следующем кадре.
        if (_pendingRemovals.Count > 0)
        {
            foreach (var pending in _pendingRemovals)
            {
                if (!TerminatingOrDeleted(pending))
                    RemoveMobAndFreeSeat(pending);
            }
            _pendingRemovals.Clear();
        }

        var now = _timing.CurTime;
        if (component.EndRoundAt is { } endAt)
        {
            if (now >= endAt)
            {
                component.EndRoundAt = null;
                GameTicker.EndRound(component.WinReason ?? string.Empty);
            }
            return;
        }

        if (component.ExterminatusAt is { } doom && now >= doom)
        {
            component.ExterminatusAt = null;
            FinishExterminatus((uid, component));
            return;
        }

        if (now < component.NextTick || !TryGetWar(component, out var war))
            return;
        var dt = (float) (now - component.NextTick + TickRate).TotalSeconds;
        component.NextTick = now + TickRate;
        var rule = new Entity<CadiaDefenseRuleComponent>(uid, component);

        if (war.Phase == CadiaWarPhase.Ended)
            return;

        StepSky();
        // Онлайн заметно изменился — места ролей пересчитываются сразу, не дожидаясь волны.
        if (RoundedOnline(component) != component.SlotOnline)
        {
            RefreshSpawners(rule, war, CadiaFaction.Imperium);
            RefreshSpawners(rule, war, CadiaFaction.Chaos);
        }
        UpdateFairSide(rule, war, CadiaFaction.Imperium);
        UpdateFairSide(rule, war, CadiaFaction.Chaos);
        UpdateSeats(rule, war);

        if (now >= war.NextImperiumWave)
            Wave(rule, war, CadiaFaction.Imperium);
        if (now >= war.NextChaosWave)
            Wave(rule, war, CadiaFaction.Chaos);

        if (war.Phase >= CadiaWarPhase.Bombardment && component.WarpPerMinute > 0 && now >= component.NextWarpSeep)
        {
            var seep = war.Phase == CadiaWarPhase.Bombardment ? component.WarpPerMinute * 0.5f : component.WarpPerMinute;
            component.NextWarpSeep = now + TimeSpan.FromMinutes(1f / seep);
            war.WarpPower++;
            if (component.State is { } warState)
                Dirty(warState, war);
        }

        UpdateCitadels(rule, war, Math.Min(dt, 5f));
        if (war.Phase == CadiaWarPhase.Ended)
            return;

        if (now >= war.PhaseEnd)
        {
            if (war.Phase == CadiaWarPhase.Finale)
                Victory(rule, CadiaFaction.Imperium, Loc.GetString("cadia-win-imperium-held"));
            else if (war.Phase == CadiaWarPhase.Evacuation)
                FinishFall(rule);
            else
                SetPhase(rule, war.Phase + 1);
        }
    }

    // ───────────────────────── Фазы ─────────────────────────

    public void SetPhase(Entity<CadiaDefenseRuleComponent> rule, CadiaWarPhase phase)
    {
        if (!TryGetWar(rule.Comp, out var war) || rule.Comp.State is not { } state)
            return;

        var now = _timing.CurTime;
        war.Phase = phase;
        war.PhaseStart = now;
        war.PhaseEnd = now + phase switch
        {
            CadiaWarPhase.Preparation => rule.Comp.PreparationTime,
            CadiaWarPhase.Bombardment => rule.Comp.BombardmentTime,
            CadiaWarPhase.Assault => rule.Comp.AssaultTime,
            CadiaWarPhase.Finale => rule.Comp.FinaleTime,
            CadiaWarPhase.Evacuation => rule.Comp.EvacuationTime,
            _ => TimeSpan.Zero,
        };

        // Пилоны защищены полем, пока враг не высадился.
        var shielded = phase < CadiaWarPhase.Assault;
        var pylons = EntityQueryEnumerator<CadiaPylonComponent>();
        while (pylons.MoveNext(out var pylon, out var comp))
            SetShield((pylon, comp), shielded);

        Dirty(state, war);
        UpdateSky(war);

        var sound = phase switch
        {
            CadiaWarPhase.Bombardment => rule.Comp.BombardmentSound,
            CadiaWarPhase.Finale or CadiaWarPhase.Evacuation => rule.Comp.FinaleSound,
            _ => rule.Comp.PhaseSound,
        };
        var key = phase.ToString().ToLowerInvariant();
        Announce(CadiaFaction.Imperium, Loc.GetString($"cadia-phase-{key}-imperium"), sound);
        Announce(CadiaFaction.Chaos, Loc.GetString($"cadia-phase-{key}-chaos"), sound);
        Feed(Loc.GetString($"cadia-hud-phase-{key}"), phase >= CadiaWarPhase.Finale ? CadiaFaction.Chaos : CadiaFaction.Imperium);

        // С началом фазы открываются её роли (Ультрамарины к штурму, Чёрный легион к развязке) — сразу,
        // не дожидаясь волны: высадка — главное событие фазы.
        RefreshSpawners(rule, war, CadiaFaction.Imperium);
        RefreshSpawners(rule, war, CadiaFaction.Chaos);
        war.NextImperiumWave = now + rule.Comp.ImperiumWaveInterval;
        war.NextChaosWave = now + rule.Comp.ChaosWaveInterval;
        Dirty(state, war);
    }

    // ───────────────────────── Волны ─────────────────────────

    public void Wave(Entity<CadiaDefenseRuleComponent> rule, CadiaWarStateComponent war, CadiaFaction faction)
    {
        var now = _timing.CurTime;
        var interval = faction == CadiaFaction.Imperium ? rule.Comp.ImperiumWaveInterval : rule.Comp.ChaosWaveInterval;
        if (faction == CadiaFaction.Imperium)
            war.NextImperiumWave = now + interval;
        else
            war.NextChaosWave = now + interval;
        if (rule.Comp.State is { } state)
            Dirty(state, war);

        var free = RefreshSpawners(rule, war, faction);
        if (free == 0)
            return;

        // Подкрепления слышат свои, а призраки видят новые места в меню ролей.
        var sound = faction == CadiaFaction.Imperium ? rule.Comp.WaveImperiumSound : rule.Comp.WaveChaosSound;
        var text = Loc.GetString(faction == CadiaFaction.Imperium ? "cadia-wave-imperium" : "cadia-wave-chaos", ("count", free));
        _chat.DispatchFilteredAnnouncement(FactionFilter(faction, false), text,
            sender: Loc.GetString(faction == CadiaFaction.Imperium ? "cadia-sender-imperium" : "cadia-sender-chaos"),
            playSound: true, announcementSound: sound, colorOverride: faction == CadiaFaction.Imperium ? ImperiumColor : ChaosColor);
    }

    /// <summary>Онлайн с шагом <see cref="CadiaDefenseRuleComponent.SlotOnlineStep"/>, не меньше одного шага.</summary>
    private int RoundedOnline(CadiaDefenseRuleComponent rule)
    {
        var step = Math.Max(1, rule.SlotOnlineStep);
        return Math.Max(step, (int) Math.Round(_players.PlayerCount / (float) step) * step);
    }

    /// <summary>
    /// Сколько бойцов роли может быть в живых при текущем онлайне. Одиночные роли (командиры, герои) всегда по одному,
    /// массовые — пропорционально онлайну к расчётным <see cref="CadiaDefenseRuleComponent.SlotReferencePlayers"/>.
    /// </summary>
    public int ScaledSlots(CadiaDefenseRuleComponent rule, CadiaRolePrototype role)
    {
        if (role.Slots <= 1)
            return role.Slots;
        var scale = Math.Clamp(rule.SlotOnline / (float) Math.Max(1, rule.SlotReferencePlayers), 0f, rule.MaxSlotScale);
        return Math.Max(1, (int) Math.Round(role.Slots * scale));
    }

    /// <summary>
    /// Пересоздаёт спавнеры стороны: на каждую открытую роль — столько мест, сколько не хватает до
    /// <see cref="ScaledSlots"/> живых. Возвращает, сколько мест открыто всего.
    /// </summary>
    private int RefreshSpawners(Entity<CadiaDefenseRuleComponent> rule, CadiaWarStateComponent war, CadiaFaction faction)
    {
        rule.Comp.SlotOnline = RoundedOnline(rule.Comp);
        var alive = new Dictionary<string, int>();
        var members = EntityQueryEnumerator<CadiaFactionMemberComponent>();
        while (members.MoveNext(out var mob, out var member))
        {
            if (member.Role is { } role && !_mobState.IsDead(mob))
                alive[role] = alive.GetValueOrDefault(role) + 1;
        }
        var overstaffed = IsOverstaffed(rule, faction);
        if (overstaffed)
            rule.Comp.ClosedSides.Add(faction);
        else
            rule.Comp.ClosedSides.Remove(faction);

        var total = 0;
        foreach (var role in _proto.EnumeratePrototypes<CadiaRolePrototype>().Where(r => r.Faction == faction).OrderBy(r => r.Order))
        {
            if (rule.Comp.Spawners.Remove(role.ID, out var old))
                QueueDel(old);

            // Во время эвакуации защитникам уже неоткуда взять подкрепления.
            if (role.AvailableFrom > war.Phase || overstaffed || war.Phase == CadiaWarPhase.Evacuation && faction == CadiaFaction.Imperium)
                continue;
            var free = ScaledSlots(rule.Comp, role) + rule.Comp.BonusSlots.GetValueOrDefault(role.ID) - alive.GetValueOrDefault(role.ID);
            if (free <= 0)
                continue;
            if (SpawnRoleSpawner(role, free) is not { } spawner)
                continue;
            rule.Comp.Spawners[role.ID] = spawner;
            total += free;
        }
        return total;
    }

    /// <summary>
    /// Игроки сами выбирают роли: без этого большинство уйдёт на одну сторону и война кончится в первую волну.
    /// Закрытая сторона открывается чуть раньше порога, чтобы набор не мигал при каждом новом бойце.
    /// </summary>
    private bool IsOverstaffed(Entity<CadiaDefenseRuleComponent> rule, CadiaFaction faction)
    {
        var ours = 0;
        var theirs = 0;
        var members = EntityQueryEnumerator<CadiaFactionMemberComponent>();
        while (members.MoveNext(out var mob, out var member))
        {
            if (member.Role == null || _mobState.IsDead(mob))
                continue;
            if (member.Faction == faction)
                ours++;
            else
                theirs++;
        }
        if (rule.Comp.ForcedClosed.Contains(faction))
            return true;
        var margin = rule.Comp.ClosedSides.Contains(faction) ? FairSideHysteresis : 0;
        return ours > theirs * rule.Comp.FairSideRatio + rule.Comp.FairSideSlack - margin;
    }

    private void UpdateFairSide(Entity<CadiaDefenseRuleComponent> rule, CadiaWarStateComponent war, CadiaFaction faction)
    {
        if (IsOverstaffed(rule, faction) != rule.Comp.ClosedSides.Contains(faction))
            RefreshSpawners(rule, war, faction);
    }

    /// <summary>Свободные места и закрытый набор: это видят ждущие призраки и ведущий.</summary>
    private void UpdateSeats(Entity<CadiaDefenseRuleComponent> rule, CadiaWarStateComponent war)
    {
        var imperium = 0;
        var chaos = 0;
        foreach (var spawner in rule.Comp.Spawners.Values)
        {
            if (!TryComp<CadiaRoleSpawnerComponent>(spawner, out var roleSpawner) || !TryComp<GhostRoleMobSpawnerComponent>(spawner, out var seats) ||
                !_proto.TryIndex(roleSpawner.Role, out var role))
                continue;
            var left = Math.Max(0, seats.AvailableTakeovers - seats.CurrentTakeovers);
            if (role.Faction == CadiaFaction.Imperium)
                imperium += left;
            else
                chaos += left;
        }
        var imperiumClosed = rule.Comp.ClosedSides.Contains(CadiaFaction.Imperium);
        var chaosClosed = rule.Comp.ClosedSides.Contains(CadiaFaction.Chaos);
        if (war.ImperiumSeats == imperium && war.ChaosSeats == chaos && war.ImperiumClosed == imperiumClosed && war.ChaosClosed == chaosClosed)
            return;
        war.ImperiumSeats = imperium;
        war.ChaosSeats = chaos;
        war.ImperiumClosed = imperiumClosed;
        war.ChaosClosed = chaosClosed;
        DirtyWar();
    }

    public void ToggleForcedClose(Entity<CadiaDefenseRuleComponent> rule, CadiaWarStateComponent war, CadiaFaction faction)
    {
        if (!rule.Comp.ForcedClosed.Remove(faction))
            rule.Comp.ForcedClosed.Add(faction);
        RefreshSpawners(rule, war, faction);
    }

    private EntityUid? SpawnRoleSpawner(CadiaRolePrototype role, int count)
    {
        if (PickSpawnPoint(role.Faction, role.SpawnGroup) is not { } coords)
        {
            Log.Warning($"Cadia: no spawn point for {role.Faction}, role {role.ID} skipped");
            return null;
        }

        var spawner = Spawn("CadiaRoleSpawner", coords);
        EnsureComp<CadiaRoleSpawnerComponent>(spawner).Role = role.ID;
        _meta.SetEntityName(spawner, Loc.GetString(role.Name));

        var mobSpawner = EnsureComp<GhostRoleMobSpawnerComponent>(spawner);
        mobSpawner.Prototype = role.Mob;
        mobSpawner.AvailableTakeovers = count;
        mobSpawner.DeleteOnSpawn = true;

        var ghost = EnsureComp<GhostRoleComponent>(spawner);
        // Геттеры GhostRoleComponent сами прогоняют текст через Loc — отдаём ключи, а не готовые строки.
        ghost.RoleName = role.Name;
        ghost.RoleDescription = role.Description;
        ghost.RoleRules = role.Whitelisted ? "cadia-rules-whitelist" : role.Faction == CadiaFaction.Imperium ? "cadia-rules-imperium" : "cadia-rules-chaos";
        return spawner;
    }

    private EntityCoordinates? PickSpawnPoint(CadiaFaction faction, string? group)
    {
        var points = new List<EntityCoordinates>();
        var query = EntityQueryEnumerator<CadiaSpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out var point, out var xform))
        {
            if (point.Enabled && point.Faction == faction && point.Group == group)
                points.Add(xform.Coordinates);
        }
        // Нет корабля на карте — флот высаживается с остальными.
        if (points.Count == 0 && group != null)
            return PickSpawnPoint(faction, null);
        return points.Count == 0 ? null : _random.Pick(points);
    }

    // ───────────────────────── Бойцы ─────────────────────────

    private void OnSpawnerUsed(Entity<CadiaFactionMemberComponent> ent, ref GhostRoleSpawnerUsedEvent args)
    {
        if (!TryComp<CadiaRoleSpawnerComponent>(args.Spawner, out var spawner) ||
            !_proto.TryIndex(spawner.Role, out var role))
            return;

        ent.Comp.Faction = role.Faction;
        ent.Comp.Role = role.ID;
        Dirty(ent);

        // Моб приходит в снаряжении по умолчанию (чтобы не был голым при ручном спавне) — меняем его на ролевое.
        if (role.Gear is { } gear)
        {
            StripGear(ent);
            _spawning.EquipStartingGear(ent, gear);
        }

        if (role.RandomName)
            _meta.SetEntityName(ent, MakeName(role));
        else
            _meta.SetEntityName(ent, Loc.GetString(role.Name));

        // Разносим бойцов по всем точкам высадки стороны, чтобы не стояли в одной клетке.
        if (role.Components is { } extra)
            EntityManager.AddComponents(ent, extra, false);
        // Спавнер отдаёт уже инициализированного моба: действия выдаём сами, MapInit у новых компонентов не придёт.
        if (TryComp<CadiaPsykerComponent>(ent, out var psyker))
            _psykers.GrantPowers((ent, psyker));
        if (TryComp<CadiaRoleActionsComponent>(ent, out var roleActions))
            _astartes.GrantRoleActions((ent, roleActions));
        if (TryComp<CadiaMarkCandidateComponent>(ent, out var candidate))
            _astartes.GrantMarkChoices((ent, candidate));
        _stats.GrantMedals(ent, role.Medals ?? (role.Commander ? DefaultCommanderMedals : 0));
        _frontline.Protect(ent);

        if (PickSpawnPoint(role.Faction, role.SpawnGroup) is { } coords)
        {
            _xform.SetCoordinates(ent, coords.Offset(_random.NextVector2(0.6f)));
            _xform.AttachToGridOrMap(ent);
        }

        EnsureComp<CadiaBriefingPendingComponent>(ent);
    }

    private void StripGear(EntityUid mob)
    {
        var items = new List<EntityUid>();
        var slots = _inventory.GetSlotEnumerator(mob);
        while (slots.NextItem(out var item))
            items.Add(item);
        items.AddRange(_hands.EnumerateHeld(mob));
        foreach (var item in items)
            Del(item);
    }

    private string MakeName(CadiaRolePrototype role)
    {
        var parts = new List<string>();
        if (role.Rank is { } rank)
            parts.Add(Loc.GetString(rank));
        if (role.FirstNames is { } first && _proto.TryIndex(first, out var firstSet) && firstSet.Values.Count > 0)
            parts.Add(_random.Pick(firstSet.Values));
        if (role.LastNames is { } last && _proto.TryIndex(last, out var lastSet) && lastSet.Values.Count > 0)
            parts.Add(_random.Pick(lastSet.Values));
        return parts.Count == 0 ? Loc.GetString(role.Name) : string.Join(' ', parts);
    }

    private void OnPlayerAttached(Entity<CadiaBriefingPendingComponent> ent, ref PlayerAttachedEvent args)
    {
        RemCompDeferred<CadiaBriefingPendingComponent>(ent);
        if (!TryComp<CadiaFactionMemberComponent>(ent, out var member) ||
            member.Role is not { } roleId ||
            !_proto.TryIndex(roleId, out var role))
            return;

        if (IsBanned(args.Player.UserId))
        {
            _pendingRemovals.Add(ent);
            return;
        }

        // Сквозь меню ролей проверить нельзя: ванильный спавнер не спрашивает. Поэтому снимаем сразу при входе в тело и возвращаем место.
        if (role.Whitelisted && !_frontline.IsWhitelisted(args.Player))
        {
            var denied = Loc.GetString("cadia-whitelist-denied");
            ChatManager.ChatMessageToOne(ChatChannel.Server, denied, denied, default, false, args.Player.Channel, Color.FromHex("#ff9a3c"));
            _pendingRemovals.Add(ent);
            return;
        }

        _stats.Joined(args.Player, ent);
        _actions.AddAction(ent, BriefingAction);
        RaiseNetworkEvent(new CadiaBriefingEvent(role.ID, Name(ent)), args.Player.Channel);

        // Флот на корабле, а старшие офицеры и демоны в отряды не входят.
        if (role.SpawnGroup == null && !role.Whitelisted && !HasComp<CadiaDaemonComponent>(ent))
            _squads.AutoAssign(ent, role.Faction, role.Commander);
        if (role.Whitelisted)
            Feed(Loc.GetString("cadia-feed-arrived", ("name", Name(ent))), role.Faction);

        var color = role.Faction == CadiaFaction.Imperium ? ImperiumColor : ChaosColor;
        var title = Loc.GetString(role.Faction == CadiaFaction.Imperium ? "cadia-brief-title-imperium" : "cadia-brief-title-chaos",
            ("name", Name(ent)), ("role", Loc.GetString(role.Name)));
        var body = role.Briefing is { } briefing ? Loc.GetString(briefing) : Loc.GetString(role.Description);
        var message = $"{title}\n{body}";
        var wrapped = $"[font size=14][bold]{title}[/bold][/font]\n{body}";
        ChatManager.ChatMessageToOne(ChatChannel.Server, message, wrapped, default, false, args.Player.Channel, color);

        var sound = role.Faction == CadiaFaction.Imperium
            ? new SoundPathSpecifier("/Audio/Misc/notice1.ogg")
            : new SoundPathSpecifier("/Audio/Ambience/Antag/headrev_start.ogg");
        _audio.PlayGlobal(sound, args.Player);
    }

    private void OnBriefingAction(Entity<CadiaFactionMemberComponent> ent, ref CadiaBriefingActionEvent args)
    {
        if (args.Handled || ent.Comp.Role is not { } role || !TryComp<ActorComponent>(ent, out var actor))
            return;
        args.Handled = true;
        RaiseNetworkEvent(new CadiaBriefingEvent(role, Name(ent)), actor.PlayerSession.Channel);
    }

    public static bool IsForcedClosed(Entity<CadiaDefenseRuleComponent> rule, CadiaFaction faction)
    {
        return rule.Comp.ForcedClosed.Contains(faction);
    }

    public bool IsBanned(Robust.Shared.Network.NetUserId user)
    {
        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out var rule, out _))
        {
            if (rule.BannedPlayers.Contains(user))
                return true;
        }
        return false;
    }

    /// <summary>Убрать бойца с поля и сразу вернуть его место в меню ролей: это не потеря стороны, а замена игрока.</summary>
    public void RemoveMobAndFreeSeat(EntityUid mob)
    {
        if (!TryComp<CadiaFactionMemberComponent>(mob, out var member))
            return;
        var faction = member.Faction;
        // Роль считается занятой, пока боец жив, поэтому сначала её снимаем.
        member.Role = null;
        SendToGhosts(mob);
        if (TryGetActiveRule(out var rule, out var war))
            RefreshSpawners(rule, war, faction);
    }

    private void SendToGhosts(EntityUid mob)
    {
        if (_mind.TryGetMind(mob, out var mindId, out var mind))
            _ghost.OnGhostAttempt(mindId, false, true, true, mind);
        QueueDel(mob);
    }

    /// <summary>Ведущий убирает игрока с поля; с запретом он не сможет вернуться и через меню ролей.</summary>
    public void RemoveFromField(Robust.Shared.Network.NetUserId user, bool ban)
    {
        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out var rule, out _))
        {
            if (ban)
                rule.BannedPlayers.Add(user);
        }
        if (_players.TryGetSessionById(user, out var session) && session.AttachedEntity is { } mob &&
            HasComp<CadiaFactionMemberComponent>(mob))
            RemoveMobAndFreeSeat(mob);
    }

    private void OnAnyAttached(PlayerAttachedEvent args)
    {
        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out var rule, out _))
        {
            if (TryGetWar(rule, out var war) && war.Phase <= CadiaWarPhase.Bombardment && rule.IntroSeen.Add(args.Player.UserId))
                RaiseNetworkEvent(new CadiaIntroEvent(), args.Player.Channel);
        }
    }

    private void ShowIntro(CadiaDefenseRuleComponent rule, bool again)
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity != null && (rule.IntroSeen.Add(session.UserId) || again))
                RaiseNetworkEvent(new CadiaIntroEvent(), session.Channel);
        }
    }

    public void ShowIntroToAll()
    {
        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out var rule, out _))
            ShowIntro(rule, true);
    }

    private void OnMobStateChanged(Entity<CadiaFactionMemberComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || ent.Comp.Role == null)
            return;

        _stats.MemberDied(ent, ent.Comp);
        if (_proto.TryIndex(ent.Comp.Role, out var fallenRole) && fallenRole.Commander)
            Feed(Loc.GetString("cadia-feed-commander-fell", ("name", Name(ent))), ent.Comp.Faction == CadiaFaction.Imperium ? CadiaFaction.Chaos : CadiaFaction.Imperium);

        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out var rule, out _))
        {
            if (ent.Comp.Faction == CadiaFaction.Imperium)
            {
                rule.ImperiumDeaths++;
                if (TryGetWar(rule, out var war) && rule.State is { } state)
                {
                    war.WarpPower += rule.WarpPerKill;
                    Dirty(state, war);
                }
            }
            else
            {
                rule.ChaosDeaths++;
            }
        }
    }

    // ───────────────────────── Пилоны ─────────────────────────

    private void OnPylonMapInit(Entity<CadiaPylonComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent, CadiaPylonVisuals.Shielded, ent.Comp.Shielded);
    }

    private void SetShield(Entity<CadiaPylonComponent> pylon, bool shielded)
    {
        if (pylon.Comp.Shielded == shielded)
            return;
        pylon.Comp.Shielded = shielded;
        Dirty(pylon);
        _appearance.SetData(pylon, CadiaPylonVisuals.Shielded, shielded);
        _audio.PlayPvs(new SoundPathSpecifier(shielded ? "/Audio/Imperial/MiningWeapons/Smasher/activate_shield.ogg" : "/Audio/Imperial/MiningWeapons/Smasher/deactivate_shield.ogg"), pylon);
    }

    private void OnPylonBeforeDamage(Entity<CadiaPylonComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (ent.Comp.Shielded)
            args.Cancelled = true;
    }

    private void OnPylonDestroyed(Entity<CadiaPylonComponent> ent, ref DestructionEventArgs args)
    {
        var rules = QueryActiveRules();
        while (rules.MoveNext(out var uid, out _, out var rule, out _))
        {
            if (!TryGetWar(rule, out var war) || rule.State is not { } state || war.Phase == CadiaWarPhase.Ended)
                continue;

            rule.PylonsFallen++;
            _stats.PylonFell(ent);
            war.PylonsAlive = Math.Max(0, war.PylonsAlive - 1);
            war.WarpPower += rule.WarpPerPylon;
            Dirty(state, war);

            UpdateSky(war);
            RaiseNetworkEvent(new CadiaScreenFxEvent(CadiaScreenFx.PylonFall));
            var number = ToRoman(ent.Comp.Number);
            Announce(CadiaFaction.Imperium, Loc.GetString("cadia-pylon-fallen-imperium", ("number", number), ("left", war.PylonsAlive)), rule.PylonFallSound);
            Announce(CadiaFaction.Chaos, Loc.GetString("cadia-pylon-fallen-chaos", ("number", number), ("left", war.PylonsAlive)), rule.PylonFallSound);
            Feed(Loc.GetString("cadia-feed-pylon", ("number", number), ("left", war.PylonsAlive)), CadiaFaction.Chaos);

            if (war.PylonsAlive == 0)
                FallOfCadia((uid, rule), Loc.GetString("cadia-win-chaos-pylons"));
        }
    }

    private void RecountPylons(CadiaWarStateComponent war)
    {
        var count = 0;
        var query = EntityQueryEnumerator<CadiaPylonComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!TerminatingOrDeleted(uid))
                count++;
        }
        war.PylonsTotal = count;
        war.PylonsAlive = count;
    }

    /// <summary>Пилоны поставили после начала (тестовая карта, админ) — пересчитать.</summary>
    public void RecountPylons()
    {
        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out var rule, out _))
        {
            if (!TryGetWar(rule, out var war) || rule.State is not { } state)
                continue;
            RecountPylons(war);
            var shielded = war.Phase < CadiaWarPhase.Assault;
            var pylons = EntityQueryEnumerator<CadiaPylonComponent>();
            while (pylons.MoveNext(out var pylon, out var comp))
                SetShield((pylon, comp), shielded);
            Dirty(state, war);
        }
    }

    // ───────────────────────── Цитадель ─────────────────────────

    private void UpdateCitadels(Entity<CadiaDefenseRuleComponent> rule, CadiaWarStateComponent war, float dt)
    {
        var query = EntityQueryEnumerator<CadiaCitadelComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var citadel, out var xform))
        {
            if (citadel.Captured)
                continue;

            _inRange.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates, citadel.Radius, _inRange);
            var chaos = 0;
            var imperium = 0;
            foreach (var member in _inRange)
            {
                if (member.Comp.Role == null || !_mobState.IsAlive(member))
                    continue;
                if (member.Comp.Faction == CadiaFaction.Chaos)
                    chaos++;
                else
                    imperium++;
            }

            var rate = dt / (float) citadel.CaptureTime.TotalSeconds;
            var old = citadel.Progress;
            // Цитадель можно брать только когда поле пилонов пало, то есть со штурма.
            if (war.Phase >= CadiaWarPhase.Assault && chaos > 0 && imperium == 0)
                citadel.Progress = Math.Min(1f, citadel.Progress + rate);
            else if (imperium > 0 && chaos == 0)
                citadel.Progress = Math.Max(0f, citadel.Progress - rate * 2f);

            war.CitadelProgress = citadel.Progress;
            war.CitadelContested = chaos > 0 && imperium > 0;
            if (Math.Abs(old - citadel.Progress) > float.Epsilon || war.CitadelContested)
            {
                Dirty(uid, citadel);
                if (rule.Comp.State is { } state)
                    Dirty(state, war);
            }

            // Пороги 25/50/75%: сирена и тревога для защитников, ликование для еретиков.
            foreach (var threshold in new[] { 25, 50, 75 })
            {
                if (citadel.Progress * 100f >= threshold && rule.Comp.CitadelWarnings.Add(threshold))
                {
                    Announce(CadiaFaction.Imperium, Loc.GetString("cadia-citadel-warning-imperium", ("percent", threshold)), rule.Comp.CitadelAlarmSound);
                    Announce(CadiaFaction.Chaos, Loc.GetString("cadia-citadel-warning-chaos", ("percent", threshold)), rule.Comp.WaveChaosSound);
                }
                else if (citadel.Progress * 100f < threshold - 10)
                {
                    rule.Comp.CitadelWarnings.Remove(threshold);
                }
            }

            if (citadel.Progress >= 1f)
            {
                citadel.Captured = true;
                Dirty(uid, citadel);
                _appearance.SetData(uid, CadiaCitadelVisuals.Captured, true);
                _light.SetColor(uid, ChaosColor);
                FallOfCadia(rule, Loc.GetString("cadia-win-chaos-citadel"));
                return;
            }
        }
    }

    // ───────────────────────── Падение Кадии ─────────────────────────

    /// <summary>
    /// Хаос сломил оборону. Если на карте есть маяк, война не кончается сразу: у защитников остаётся
    /// последняя задача вывезти как можно больше людей. Без маяка это обычная победа Хаоса.
    /// </summary>
    public void FallOfCadia(Entity<CadiaDefenseRuleComponent> rule, string reason)
    {
        if (!TryGetWar(rule.Comp, out var war) || war.Phase is CadiaWarPhase.Evacuation or CadiaWarPhase.Ended)
            return;
        if (!_evac.HasBeacon())
        {
            Victory(rule, CadiaFaction.Chaos, reason);
            return;
        }
        rule.Comp.FallReason = reason;
        SetPhase(rule, CadiaWarPhase.Evacuation);
    }

    /// <summary>Время вышло, маяка нет или спасать некого: итог зависит от того, сколько людей успели вывезти.</summary>
    public void FinishFall(Entity<CadiaDefenseRuleComponent> rule)
    {
        if (!TryGetWar(rule.Comp, out var war) || war.Phase != CadiaWarPhase.Evacuation)
            return;
        var saved = war.EvacSaved;
        var tier = saved >= rule.Comp.EvacGoodTier ? "cadia-fall-good" : saved >= rule.Comp.EvacPoorTier ? "cadia-fall-poor" : "cadia-fall-none";
        var ending = saved >= rule.Comp.EvacGoodTier ? CadiaEnding.Retreat : saved >= rule.Comp.EvacPoorTier ? CadiaEnding.Bitter : CadiaEnding.Annihilation;
        var reason = $"{rule.Comp.FallReason} {Loc.GetString(tier, ("saved", saved))}";
        Victory(rule, CadiaFaction.Chaos, reason, ending);
    }

    // ───────────────────────── Победа ─────────────────────────

    public void Victory(Entity<CadiaDefenseRuleComponent> rule, CadiaFaction winner, string reason, CadiaEnding? ending = null)
    {
        if (rule.Comp.Winner != null || !TryGetWar(rule.Comp, out var war) || rule.Comp.State is not { } state)
            return;

        rule.Comp.Winner = winner;
        rule.Comp.WinReason = reason;
        war.Winner = winner;
        war.Phase = CadiaWarPhase.Ended;
        war.PhaseEnd = _timing.CurTime + rule.Comp.EndRoundDelay;
        Dirty(state, war);

        foreach (var spawner in rule.Comp.Spawners.Values)
            QueueDel(spawner);
        rule.Comp.Spawners.Clear();

        var sound = winner == CadiaFaction.Imperium ? rule.Comp.ImperiumVictorySound : rule.Comp.ChaosVictorySound;
        var text = Loc.GetString(winner == CadiaFaction.Imperium ? "cadia-victory-imperium" : "cadia-victory-chaos", ("reason", reason));
        _chat.DispatchGlobalAnnouncement(text, Loc.GetString("cadia-sender-war"), true, sound,
            winner == CadiaFaction.Imperium ? ImperiumColor : ChaosColor);

        var scene = ending ?? (winner == CadiaFaction.Imperium ? CadiaEnding.Held : CadiaEnding.Triumph);
        RaiseNetworkEvent(new CadiaEndingEvent(scene, war.EvacSaved));

        rule.Comp.EndRoundAt = _timing.CurTime + rule.Comp.EndRoundDelay;
    }

    protected override void AppendRoundEndText(EntityUid uid, CadiaDefenseRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        args.AddLine(Loc.GetString("cadia-roundend-title"));
        args.AddLine(component.Winner switch
        {
            CadiaFaction.Imperium => Loc.GetString("cadia-roundend-imperium"),
            CadiaFaction.Chaos => Loc.GetString("cadia-roundend-chaos"),
            _ => Loc.GetString("cadia-roundend-none"),
        });
        if (component.WinReason != null)
            args.AddLine(component.WinReason);
        args.AddLine(Loc.GetString("cadia-roundend-stats",
            ("imperium", component.ImperiumDeaths),
            ("chaos", component.ChaosDeaths),
            ("pylons", component.PylonsFallen)));

        // Кто дожил до конца — по ролям.
        var members = EntityQueryEnumerator<CadiaFactionMemberComponent>();
        var survivors = new Dictionary<CadiaFaction, int>();
        while (members.MoveNext(out var mob, out var member))
        {
            if (member.Role != null && !_mobState.IsDead(mob))
                survivors[member.Faction] = survivors.GetValueOrDefault(member.Faction) + 1;
        }
        args.AddLine(Loc.GetString("cadia-roundend-survivors",
            ("imperium", survivors.GetValueOrDefault(CadiaFaction.Imperium)),
            ("chaos", survivors.GetValueOrDefault(CadiaFaction.Chaos))));

        var report = _stats.BuildReport();
        if (report.Count > 0)
        {
            args.AddLine(string.Empty);
            args.AddLine(Loc.GetString("cadia-stats-title"));
            foreach (var line in report)
                args.AddLine(line);
        }
    }

    // ───────────────────────── Сила варпа, босс, Экстерминатус ─────────────────────────

    public bool TryGetActiveWar([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out CadiaWarStateComponent? war)
    {
        war = null;
        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out var rule, out _))
        {
            if (TryGetWar(rule, out war))
                return true;
        }
        return false;
    }

    public bool TryGetActiveRule(out Entity<CadiaDefenseRuleComponent> rule, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out CadiaWarStateComponent? war)
    {
        rule = default;
        war = null;
        var rules = QueryActiveRules();
        while (rules.MoveNext(out var uid, out _, out var comp, out _))
        {
            if (!TryGetWar(comp, out war))
                continue;
            rule = (uid, comp);
            return true;
        }
        return false;
    }

    /// <summary>Состояние войны изменила другая система: отправить клиентам.</summary>
    public void DirtyWar()
    {
        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out var rule, out _))
        {
            if (rule.State is { } state && TryComp<CadiaWarStateComponent>(state, out var war))
                Dirty(state, war);
        }
    }

    public bool TrySpendWarp(int amount)
    {
        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out var rule, out _))
        {
            if (!TryGetWar(rule, out var war) || rule.State is not { } state || war.WarpPower < amount)
                continue;
            war.WarpPower -= amount;
            Dirty(state, war);
            return true;
        }
        return false;
    }

    public void AddWarp(int amount)
    {
        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out var rule, out _))
        {
            if (!TryGetWar(rule, out var war) || rule.State is not { } state)
                continue;
            war.WarpPower += amount;
            Dirty(state, war);
        }
    }

    public void AddBonusSlot(string role)
    {
        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out var rule, out _))
            rule.BonusSlots[role] = rule.BonusSlots.GetValueOrDefault(role) + 1;
    }

    /// <summary>Принца демонов можно призвать, когда пала половина пилонов или идёт развязка.</summary>
    public bool BossUnlocked()
    {
        return TryGetActiveWar(out var war) &&
               (war.Phase >= CadiaWarPhase.Finale || war.PylonsTotal > 0 && war.PylonsAlive * 2 <= war.PylonsTotal);
    }

    /// <summary>Небо багровеет с каждым павшим пилоном: Око Ужаса всё ближе. К развязке — ещё темнее.</summary>
    private void UpdateSky(CadiaWarStateComponent war)
    {
        var fallen = war.PylonsTotal > 0 ? 1f - (float) war.PylonsAlive / war.PylonsTotal : 0f;
        _skyTarget = Math.Clamp(fallen * 0.7f + (war.Phase >= CadiaWarPhase.Finale ? 0.25f : 0f), 0f, 0.9f);
    }

    private void StepSky()
    {
        if (MathHelper.CloseTo(_skyShare, _skyTarget))
            return;
        _skyShare = _skyShare < _skyTarget
            ? Math.Min(_skyShare + SkyStep, _skyTarget)
            : Math.Max(_skyShare - SkyStep, _skyTarget);
        ApplySky();
    }

    private void ApplySky()
    {
        var levels = EntityQueryEnumerator<CadiaFlightLevelComponent, MapLightComponent>();
        while (levels.MoveNext(out var uid, out var level, out var light))
        {
            if (level.Level == CadiaFlightLevel.Orbit)
                continue;
            if (!_skyBase.TryGetValue(uid, out var original))
                _skyBase[uid] = original = light.AmbientLightColor;
            light.AmbientLightColor = Color.InterpolateBetween(original, EyeOfTerror, _skyShare);
            Dirty(uid, light);
        }
    }

    /// <summary>Вызвали принца демонов: все слышат и видят.</summary>
    public void PrinceArrived()
    {
        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out var rule, out _))
        {
            RaiseNetworkEvent(new CadiaScreenFxEvent(CadiaScreenFx.Prince));
            Feed(Loc.GetString("cadia-feed-prince"), CadiaFaction.Chaos);
            _chat.DispatchGlobalAnnouncement(Loc.GetString("cadia-prince-arrived"), Loc.GetString("cadia-sender-war"), true, rule.FinaleSound, ChaosColor);
        }
    }

    /// <summary>Принц демонов изгнан — Империум побеждает.</summary>
    public void BossBanished()
    {
        var rules = QueryActiveRules();
        while (rules.MoveNext(out var uid, out _, out var rule, out _))
            Victory((uid, rule), CadiaFaction.Imperium, Loc.GetString("cadia-win-imperium-boss"), CadiaEnding.Banished);
    }

    /// <summary>Инквизитор объявил Экстерминатус: минута на эвакуацию, затем поверхность сгорает.</summary>
    public void Exterminatus()
    {
        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out var rule, out _))
        {
            if (rule.ExterminatusAt != null || rule.Winner != null)
                continue;
            rule.ExterminatusAt = _timing.CurTime + TimeSpan.FromMinutes(1);
            _chat.DispatchGlobalAnnouncement(Loc.GetString("cadia-exterminatus-announce"), Loc.GetString("cadia-sender-war"), true,
                rule.CitadelAlarmSound, ImperiumColor);
        }
    }

    private void FinishExterminatus(Entity<CadiaDefenseRuleComponent> rule)
    {
        RaiseNetworkEvent(new CadiaScreenFxEvent(CadiaScreenFx.Exterminatus));
        // Огненный шторм вокруг каждого пилона и цитадели — там, где идёт бой.
        var pylons = EntityQueryEnumerator<CadiaPylonComponent, TransformComponent>();
        while (pylons.MoveNext(out var uid, out _, out _))
            _explosion.QueueExplosion(_xform.GetMapCoordinates(uid), "Default", 400f, 6f, 30f, null, canCreateVacuum: false);
        var citadels = EntityQueryEnumerator<CadiaCitadelComponent, TransformComponent>();
        while (citadels.MoveNext(out var uid, out _, out _))
            _explosion.QueueExplosion(_xform.GetMapCoordinates(uid), "Default", 400f, 6f, 30f, null, canCreateVacuum: false);
        Victory(rule, CadiaFaction.Imperium, Loc.GetString("cadia-win-exterminatus"), CadiaEnding.Exterminatus);
    }

    // ───────────────────────── Вспомогательное ─────────────────────────

    public bool TryGetWar(CadiaDefenseRuleComponent rule, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out CadiaWarStateComponent? war)
    {
        war = null;
        return rule.State is { } state && TryComp(state, out war);
    }

    /// <summary>
    /// Объявление стороне. Имперское слышат все, кроме Хаоса (наблюдатели и призраки — тоже, чтобы понимать ход боя);
    /// хаосское — только Хаос.
    /// </summary>
    public void Announce(CadiaFaction faction, string text, SoundSpecifier sound)
    {
        _chat.DispatchFilteredAnnouncement(FactionFilter(faction, faction == CadiaFaction.Imperium), text,
            sender: Loc.GetString(faction == CadiaFaction.Imperium ? "cadia-sender-imperium" : "cadia-sender-chaos"),
            playSound: true, announcementSound: sound,
            colorOverride: faction == CadiaFaction.Imperium ? ImperiumColor : ChaosColor);
    }

    /// <summary>Лента событий: цвет стороны, в чью пользу произошло событие.</summary>
    public void Feed(string text, CadiaFaction side)
    {
        RaiseNetworkEvent(new CadiaFeedEvent(text, side == CadiaFaction.Imperium ? ImperiumColor : ChaosColor));
    }

    /// <summary>Объявление ведущего: всем или одной стороне. Пустой текст не отправляем.</summary>
    public void Broadcast(string text, CadiaFaction? faction)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        var rules = QueryActiveRules();
        while (rules.MoveNext(out _, out var rule, out _))
        {
            if (faction is { } side)
                Announce(side, text, rule.PhaseSound);
            else
                _chat.DispatchGlobalAnnouncement(text, Loc.GetString("cadia-sender-war"), true, rule.PhaseSound, ImperiumColor);
        }
    }

    private Filter FactionFilter(CadiaFaction faction, bool includeNeutral)
    {
        return Filter.Empty().AddWhere(session =>
        {
            if (session.AttachedEntity is not { } mob || !TryComp<CadiaFactionMemberComponent>(mob, out var member) || member.Role == null)
                return includeNeutral;
            return member.Faction == faction;
        });
    }

    public static string ToRoman(int number)
    {
        if (number <= 0)
            return number.ToString();
        var map = new (int Value, string Numeral)[]
        {
            (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
        };
        var result = string.Empty;
        foreach (var (value, numeral) in map)
        {
            while (number >= value)
            {
                result += numeral;
                number -= value;
            }
        }
        return result;
    }
}
