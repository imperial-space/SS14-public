using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Cargo.Components;
using Content.Shared.Chat;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Contraband;
using Content.Shared.Imperial.Security;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Security;

/// <summary>
/// Гражданские задания СБ из SS13 (bounty_machinery.dm, bounties/security.dm):
/// «Патруль» — пройти заданное число метров по району станции; «Конфискованная контрабанда» —
/// сдать на пад пять предметов контрабанды. 30% награды сразу идут на счёт отдела СБ,
/// а куб награды карго продаёт за полную стоимость.
/// </summary>
public sealed class SecurityBountySystem : EntitySystem
{
    /// <summary>CARGO_CRATE_VALUE * 5 за 100 метров патруля.</summary>
    private const int PatrolRewardPer100 = 1000;

    /// <summary>CARGO_CRATE_VALUE * 4.</summary>
    private const int ContrabandReward = 800;

    /// <summary>CIV_BOUNTY_SPLIT.</summary>
    private const float CivSplit = 0.3f;

    /// <summary>Район патруля — круг вокруг навигационного маяка (в SS13 — зона станции).</summary>
    private const int PatrolRadius = 8;

    private const string SecurityAccount = "Security";
    private const string CubeProto = "SecurityBountyCube";

    private static readonly HashSet<string> ContrabandSeverities = new() { "Major", "GrandTheft", "HighlyIllegal", "Syndicate", "Magical" };

    private static readonly SoundSpecifier YesSound = new SoundPathSpecifier("/Audio/Machines/chime.ogg");
    private static readonly SoundSpecifier NoSound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");
    private static readonly SoundSpecifier SendSound = new SoundPathSpecifier("/Audio/Machines/high_tech_confirm.ogg");

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private TimeSpan _nextTrack;
    private TimeSpan _nextUi;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SecurityBountyConsoleComponent, MapInitEvent>(OnConsoleMapInit);
        SubscribeLocalEvent<SecurityBountyConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<SecurityBountyConsoleComponent, SecurityBountyActionMessage>(OnAction);
        SubscribeLocalEvent<SecurityBountyConsoleComponent, SecurityBountyPickMessage>(OnPick);
        SubscribeLocalEvent<SecurityBountyConsoleComponent, EntInsertedIntoContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<SecurityBountyConsoleComponent, EntRemovedFromContainerMessage>(OnSlotChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var consoles = EntityQueryEnumerator<SecurityBountyConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            if (console.SendAt is { } at && now >= at)
            {
                console.SendAt = null;
                Send((uid, console));
                UpdateUi((uid, console));
            }
        }

        if (now >= _nextTrack)
        {
            _nextTrack = now + TimeSpan.FromSeconds(0.2);
            TrackPatrols();
        }

        if (now >= _nextUi)
        {
            _nextUi = now + TimeSpan.FromSeconds(1);
            var query = EntityQueryEnumerator<SecurityBountyConsoleComponent>();
            while (query.MoveNext(out var uid, out var console))
            {
                if (_ui.IsUiOpen(uid, SecurityBountyUiKey.Key))
                    UpdateUi((uid, console));
            }
        }
    }

    #region Консоль

    /// <summary>post_machine_initialize(): пад в радиусе 4 клеток.</summary>
    private void OnConsoleMapInit(Entity<SecurityBountyConsoleComponent> ent, ref MapInitEvent args)
    {
        FindPad(ent);
    }

    private EntityUid? FindPad(Entity<SecurityBountyConsoleComponent> ent)
    {
        if (ent.Comp.Pad is { } pad && !TerminatingOrDeleted(pad))
            return pad;

        foreach (var found in _lookup.GetEntitiesInRange<SecurityBountyPadComponent>(Transform(ent).Coordinates, 4f))
        {
            ent.Comp.Pad = found;
            return found;
        }

        return null;
    }

    private void OnUiOpened(Entity<SecurityBountyConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnSlotChanged(Entity<SecurityBountyConsoleComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateUi(ent);
    }

    private void OnSlotChanged(Entity<SecurityBountyConsoleComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateUi(ent);
    }

    private Entity<SecurityBountyCardComponent>? GetCard(EntityUid console)
    {
        if (_itemSlots.GetItemOrNull(console, SecurityBountyConsoleComponent.IdSlot) is not { } id)
            return null;

        return (id, EnsureComp<SecurityBountyCardComponent>(id));
    }

    private void OnAction(Entity<SecurityBountyConsoleComponent> ent, ref SecurityBountyActionMessage args)
    {
        switch (args.Action)
        {
            case SecurityBountyAction.Request:
                AddBounties(ent, args.Actor);
                break;
            case SecurityBountyAction.Recalc:
                Recalc(ent);
                break;
            case SecurityBountyAction.Send:
                StartSend(ent);
                break;
            case SecurityBountyAction.Eject:
                if (_itemSlots.TryGetSlot(ent, SecurityBountyConsoleComponent.IdSlot, out var slot))
                    _itemSlots.TryEjectToHands(ent, slot, args.Actor, true);
                break;
        }

        UpdateUi(ent);
    }

    /// <summary>pick_bounty().</summary>
    private void OnPick(Entity<SecurityBountyConsoleComponent> ent, ref SecurityBountyPickMessage args)
    {
        if (GetCard(ent) is not { } card || card.Comp.Options is not { } options ||
            args.Index < 0 || args.Index >= options.Count)
        {
            _audio.PlayPvs(NoSound, ent);
            return;
        }

        card.Comp.Current = options[args.Index];
        card.Comp.Options = null;
        UpdateUi(ent);
    }

    /// <summary>add_bounties(): три задания раз в 5 минут.</summary>
    private void AddBounties(Entity<SecurityBountyConsoleComponent> ent, EntityUid user)
    {
        if (GetCard(ent) is not { } card)
            return;

        var now = _timing.CurTime;
        if ((card.Comp.Current != null || card.Comp.Options != null) && now < card.Comp.NextRequest)
        {
            var left = (int) Math.Ceiling((card.Comp.NextRequest - now).TotalSeconds);
            _popup.PopupEntity(Loc.GetString("security-bounty-cooldown", ("seconds", left)), ent, user);
            return;
        }

        card.Comp.NextRequest = now + TimeSpan.FromMinutes(5);
        card.Comp.Options = GenerateBounties(ent, 3);
        card.Comp.Current = null;
    }

    /// <summary>generate_bounty_list(): 75% патруль, 25% контрабанда, контрабанда не повторяется.</summary>
    private List<SecurityBounty> GenerateBounties(EntityUid console, int count)
    {
        var list = new List<SecurityBounty>();
        var attempts = 20;
        while (list.Count < count && attempts-- > 0)
        {
            SecurityBounty? bounty = _random.Prob(0.75f) ? CreatePatrol(console) : CreateContraband();
            if (bounty == null)
                continue;

            if (bounty.Kind == SecurityBountyKind.Contraband && list.Any(b => b.Kind == SecurityBountyKind.Contraband))
                continue;

            if (bounty.Kind == SecurityBountyKind.Patrol && list.Any(b => b.Kind == SecurityBountyKind.Patrol && b.Area == bounty.Area))
                continue;

            list.Add(bounty);
        }

        return list;
    }

    private SecurityBounty CreateContraband()
    {
        return new SecurityBounty
        {
            Kind = SecurityBountyKind.Contraband,
            Name = Loc.GetString("security-bounty-contraband-name"),
            Description = Loc.GetString("security-bounty-contraband-desc"),
            Reward = ContrabandReward,
            Required = 5,
        };
    }

    /// <summary>patrol/New(): район, 40–80% его клеток, награда пропорциональна длине.</summary>
    private SecurityBounty? CreatePatrol(EntityUid console)
    {
        if (_station.GetOwningStation(console) is not { } station || _station.GetLargestGrid(station) is not { } grid ||
            !TryComp<MapGridComponent>(grid, out var mapGrid))
        {
            return null;
        }

        var beacons = new List<(EntityUid Uid, string Name)>();
        var query = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var beacon, out var xform))
        {
            if (xform.GridUid != grid || !beacon.Enabled)
                continue;

            var name = beacon.Text ?? (beacon.DefaultText is { } loc ? Loc.GetString(loc) : null);
            if (!string.IsNullOrWhiteSpace(name))
                beacons.Add((uid, name));
        }

        if (beacons.Count == 0)
            return null;

        var (beaconUid, area) = _random.Pick(beacons);
        var center = Transform(beaconUid).Coordinates;
        var total = CountFloor(grid, mapGrid, center);
        var needed = (int) Math.Round(total * _random.Next(4, 9) * 0.1);

        // can_get(): только задания, ради которых стоит идти.
        if (needed < 20)
            return null;

        return new SecurityBounty
        {
            Kind = SecurityBountyKind.Patrol,
            Name = Loc.GetString("security-bounty-patrol-name", ("area", area)),
            Description = Loc.GetString("security-bounty-patrol-desc", ("area", area), ("meters", needed), ("radius", PatrolRadius)),
            Reward = PatrolRewardPer100 * needed / 100,
            Grid = grid,
            Center = center,
            Area = area,
            NeededCoverage = needed,
        };
    }

    /// <summary>Сколько проходимых клеток пола в районе патруля.</summary>
    private int CountFloor(EntityUid grid, MapGridComponent mapGrid, EntityCoordinates center)
    {
        var centerTile = _map.TileIndicesFor(grid, mapGrid, center);
        var count = 0;
        for (var x = -PatrolRadius; x <= PatrolRadius; x++)
        {
            for (var y = -PatrolRadius; y <= PatrolRadius; y++)
            {
                if (x * x + y * y > PatrolRadius * PatrolRadius)
                    continue;

                var tile = centerTile + new Vector2i(x, y);
                if (IsWalkable(grid, mapGrid, tile))
                    count++;
            }
        }

        return count;
    }

    private bool IsWalkable(EntityUid grid, MapGridComponent mapGrid, Vector2i tile)
    {
        if (!_map.TryGetTileRef(grid, mapGrid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(grid, mapGrid, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (TryComp<AirtightComponent>(ent, out var airtight) && airtight.AirBlocked)
                return false;
        }

        return true;
    }

    /// <summary>recalc(): есть ли на паде подходящая цель.</summary>
    private void Recalc(Entity<SecurityBountyConsoleComponent> ent)
    {
        if (GetCard(ent) is not { } card)
        {
            SetStatus(ent, Loc.GetString("security-bounty-status-no-id"), false);
            return;
        }

        if (card.Comp.Current is not { } bounty)
        {
            SetStatus(ent, Loc.GetString("security-bounty-status-no-bounty"), false);
            return;
        }

        var applies = bounty.CanClaim || GetPadItems(ent).Any(item => AppliesTo(bounty, item));
        SetStatus(ent, Loc.GetString(applies ? "security-bounty-status-applies" : "security-bounty-status-not-applies"), applies);
    }

    private void SetStatus(Entity<SecurityBountyConsoleComponent> ent, string status, bool good)
    {
        ent.Comp.StatusReport = status;
        _audio.PlayPvs(good ? YesSound : NoSound, ent);
    }

    private IEnumerable<EntityUid> GetPadItems(Entity<SecurityBountyConsoleComponent> ent)
    {
        if (FindPad(ent) is not { } pad)
            return Enumerable.Empty<EntityUid>();

        return _lookup.GetEntitiesInRange(Transform(pad).Coordinates, 0.45f, LookupFlags.Dynamic | LookupFlags.Sundries)
            .Where(e => e != pad && HasComp<ItemComponent>(e));
    }

    /// <summary>contraband/applies_to(): HAS_TRAIT(TRAIT_CONTRABAND).</summary>
    private bool AppliesTo(SecurityBounty bounty, EntityUid item)
    {
        return bounty.Kind == SecurityBountyKind.Contraband &&
               TryComp<ContrabandComponent>(item, out var contraband) &&
               ContrabandSeverities.Contains(contraband.Severity);
    }

    private void StartSend(Entity<SecurityBountyConsoleComponent> ent)
    {
        if (ent.Comp.SendAt != null || FindPad(ent) == null)
            return;

        if (GetCard(ent) is not { Comp.Current: not null })
        {
            Recalc(ent);
            return;
        }

        ent.Comp.StatusReport = Loc.GetString("security-bounty-status-sending");
        ent.Comp.SendAt = _timing.CurTime + TimeSpan.FromSeconds(3);
    }

    /// <summary>send(): забрать цели с пада и засчитать выполненное задание.</summary>
    private void Send(Entity<SecurityBountyConsoleComponent> ent)
    {
        _audio.PlayPvs(SendSound, ent);
        if (GetCard(ent) is not { } card || card.Comp.Current is not { } bounty)
            return;

        var shipped = 0;
        foreach (var item in GetPadItems(ent).ToList())
        {
            if (bounty.CanClaim || !AppliesTo(bounty, item))
                continue;

            bounty.Shipped++;
            shipped++;
            QueueDel(item);
        }

        var status = shipped > 0 ? Loc.GetString("security-bounty-status-shipped", ("count", shipped)) + " " : string.Empty;

        if (!bounty.CanClaim)
        {
            ent.Comp.StatusReport = shipped > 0 ? status : Loc.GetString("security-bounty-status-nothing");
            _audio.PlayPvs(shipped > 0 ? YesSound : NoSound, ent);
            return;
        }

        // Выполнено: доля СБ сразу на счёт отдела, куб — карго.
        if (_station.GetOwningStation(ent) is { } station && TryComp<StationBankAccountComponent>(station, out var bank))
            _cargo.UpdateBankAccount((station, bank), (int) (bounty.Reward * CivSplit), SecurityAccount);

        var padCoords = FindPad(ent) is { } pad ? Transform(pad).Coordinates : Transform(ent).Coordinates;
        var cube = Spawn(CubeProto, padCoords);
        _meta.SetEntityName(cube, Loc.GetString("security-bounty-cube-name", ("bounty", bounty.Name)));
        EnsureComp<StaticPriceComponent>(cube).Price = bounty.Reward;

        card.Comp.Current = null;
        ent.Comp.StatusReport = status + Loc.GetString("security-bounty-status-complete", ("share", (int) (bounty.Reward * CivSplit)));
        _audio.PlayPvs(YesSound, ent);
    }

    private void UpdateUi(Entity<SecurityBountyConsoleComponent> ent)
    {
        var state = new SecurityBountyState
        {
            HasPad = FindPad(ent) != null,
            StatusReport = ent.Comp.StatusReport,
            Sending = ent.Comp.SendAt != null,
        };

        if (GetCard(ent) is { } card)
        {
            state.HasId = true;
            state.IdName = CompOrNull<IdCardComponent>(card)?.FullName ?? Name(card);

            if (card.Comp.Current is { } current)
            {
                state.Current = ToEntry(current);
                state.CurrentProgress = current.Kind == SecurityBountyKind.Patrol
                    ? Loc.GetString("security-bounty-progress-patrol", ("progress", current.Progress), ("needed", current.NeededCoverage))
                    : Loc.GetString("security-bounty-progress-contraband", ("progress", current.Shipped), ("needed", current.Required));
            }

            if (card.Comp.Options is { } options)
                state.Options = options.Select(ToEntry).ToList();
        }

        _ui.SetUiState(ent.Owner, SecurityBountyUiKey.Key, state);
    }

    private static SecurityBountyEntry ToEntry(SecurityBounty bounty)
    {
        return new SecurityBountyEntry
        {
            Name = bounty.Name,
            Description = bounty.Description,
            Reward = (int) (bounty.Reward * CivSplit),
        };
    }

    #endregion

    #region Патруль

    /// <summary>
    /// on_card_moved(): ID-карта с заданием считает пройденные клетки района,
    /// каждую не больше трёх раз, и сообщает о прогрессе по четвертям.
    /// </summary>
    private void TrackPatrols()
    {
        var query = EntityQueryEnumerator<SecurityBountyCardComponent>();
        while (query.MoveNext(out var cardUid, out var card))
        {
            if (card.Current is not { Kind: SecurityBountyKind.Patrol } bounty || bounty.Alerted >= 5)
                continue;

            var holder = GetHolder(cardUid);
            var xform = Transform(holder);
            if (xform.GridUid != bounty.Grid || !TryComp<MapGridComponent>(bounty.Grid, out var mapGrid))
                continue;

            var tile = _map.TileIndicesFor(bounty.Grid, mapGrid, xform.Coordinates);
            if (bounty.LastTile == tile)
                continue;

            bounty.LastTile = tile;
            var center = _map.TileIndicesFor(bounty.Grid, mapGrid, bounty.Center);
            var delta = tile - center;
            if (delta.X * delta.X + delta.Y * delta.Y > PatrolRadius * PatrolRadius || !IsWalkable(bounty.Grid, mapGrid, tile))
                continue;

            if (bounty.WalkedTiles.GetValueOrDefault(tile) >= 3)
                continue;

            bounty.WalkedTiles[tile] = bounty.WalkedTiles.GetValueOrDefault(tile) + 1;
            AlertProgress(holder, bounty);
        }
    }

    /// <summary>Кто несёт карту: поднимаемся по контейнерам до моба или до грида.</summary>
    private EntityUid GetHolder(EntityUid card)
    {
        var current = card;
        while (_container.TryGetContainingContainer((current, null, null), out var container))
        {
            current = container.Owner;
        }

        return current;
    }

    private void AlertProgress(EntityUid holder, SecurityBounty bounty)
    {
        var percent = (float) bounty.Progress / bounty.NeededCoverage;
        string? message = null;

        if (bounty.Alerted == 0)
        {
            bounty.Alerted = 1;
            message = Loc.GetString("security-bounty-patrol-started", ("meters", bounty.NeededCoverage));
        }
        else if (bounty.Progress >= bounty.NeededCoverage && bounty.Alerted < 5)
        {
            bounty.Alerted = 5;
            message = Loc.GetString("security-bounty-patrol-complete");
        }
        else if (percent >= 0.75f && bounty.Alerted < 4)
        {
            bounty.Alerted = 4;
            message = Loc.GetString("security-bounty-patrol-percent", ("percent", 75));
        }
        else if (percent >= 0.5f && bounty.Alerted < 3)
        {
            bounty.Alerted = 3;
            message = Loc.GetString("security-bounty-patrol-percent", ("percent", 50));
        }
        else if (percent >= 0.25f && bounty.Alerted < 2)
        {
            bounty.Alerted = 2;
            message = Loc.GetString("security-bounty-patrol-percent", ("percent", 25));
        }

        if (message == null)
            return;

        // bank_card_talk(): карта «говорит» владельцу.
        _popup.PopupEntity(message, holder, holder);
        if (_players.TryGetSessionByEntity(holder, out var session))
            _chatManager.ChatMessageToOne(ChatChannel.Local, message, message, default, false, session.Channel);
    }

    #endregion
}
