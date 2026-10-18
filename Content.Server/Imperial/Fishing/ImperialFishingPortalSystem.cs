using System.Linq;
using Content.Server.Imperial.Research;
using Content.Server.Power.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Генератор рыболовных порталов: измерения открываются экспериментами по сканированию рыб,
/// эмаг даёт измерение Синдиката, мультитулом можно привязать другие места ловли.
/// Здесь же выпуск рыбы обратно в воду, лаву, лёд и портал (TRAIT_CATCH_AND_RELEASE).
/// </summary>
public sealed class ImperialFishingPortalSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly TgExperimentSystem _experiments = default!;
    [Dependency] private readonly ImperialFishingSpotSystem _spots = default!;
    [Dependency] private readonly ImperialFishSystem _fish = default!;

    private const string DefaultSource = "ImperialFishSourcePortal";
    private const string SyndicateSource = "ImperialFishSourcePortalSyndicate";

    /// <summary>fish_source_reward экспериментов /datum/experiment/scanning/fish.</summary>
    private static readonly (string Experiment, string Source)[] ExperimentRewards =
    {
        ("TgExpFish", "ImperialFishSourcePortalBeach"),
        ("TgExpFishSecond", "ImperialFishSourcePortalOcean"),
        ("TgExpFishThird", "ImperialFishSourcePortalChasm"),
        ("TgExpFishHolo", "ImperialFishSourcePortalRandom"),
        ("TgExpFishFourth", "ImperialFishSourcePortalHyperspace"),
    };

    /// <summary>Источники на воде, лаве и льду, куда можно выпустить рыбу.</summary>
    private static readonly HashSet<string> CatchAndReleaseSources = new()
    {
        "ImperialFishSourceCursedSpring", "ImperialFishSourceLavaland", "ImperialFishSourceLavalandIcemoon",
        "ImperialFishSourceOcean", "ImperialFishSourceOceanBeach", "ImperialFishSourceIceFishing",
        "ImperialFishSourceRiver", "ImperialFishSourceHotSpring", "ImperialFishSourceTizira",
    };

    private static readonly ProtoId<Content.Shared.Tools.ToolQualityPrototype> Pulsing = "Pulsing";

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialFishingPortalComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<ImperialFishingPortalComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ImperialFishingPortalComponent, GetVerbsEvent<AlternativeVerb>>(OnAltVerbs);
        SubscribeLocalEvent<ImperialFishingPortalComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<ImperialFishingPortalComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<ImperialFishingPortalComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ImperialFishingPortalComponent, ImperialFishingRollRewardEvent>(OnRollReward);
        SubscribeLocalEvent<ImperialFishingPortalComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ImperialFishingPortalComponent, Content.Server.Imperial.MachineParts.ImperialMachinePartsRefreshEvent>(OnRefreshParts);
        SubscribeLocalEvent<ImperialFishingPortalBufferComponent, AfterInteractEvent>(OnBufferAfterInteract);
        Subs.BuiEvents<ImperialFishingPortalComponent>(ImperialFishingPortalUiKey.Key, subs =>
        {
            subs.Event<ImperialFishingPortalSelectMessage>(OnSelect);
        });

        SubscribeLocalEvent<ImperialFishComponent, AfterInteractEvent>(OnFishAfterInteract);
        SubscribeLocalEvent<ImperialFishComponent, ImperialFishReleaseDoAfterEvent>(OnReleaseDoAfter);
    }

    #region Включение

    private bool Powered(EntityUid uid) => _power.IsPowered(uid);

    private void OnActivate(Entity<ImperialFishingPortalComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        if (ent.Comp.Active != null)
        {
            Deactivate(ent);
            return;
        }

        var options = GetOptions(ent);
        if (options.Count == 1)
        {
            Activate(ent, options[0].Key, args.User);
            return;
        }

        _ui.OpenUi(ent.Owner, ImperialFishingPortalUiKey.Key, args.User);
        _ui.SetUiState(ent.Owner, ImperialFishingPortalUiKey.Key, new ImperialFishingPortalBoundUserInterfaceState(options, false));
    }

    /// <summary>select_fish_source(): стандартный аквариум, Синдикат, измерения из экспериментов и привязанные места.</summary>
    private List<ImperialFishingPortalOption> GetOptions(Entity<ImperialFishingPortalComponent> ent)
    {
        var options = new List<ImperialFishingPortalOption>();
        void AddSource(string id)
        {
            if (options.Any(o => o.Key == id) || !_proto.TryIndex<ImperialFishSourcePrototype>(id, out var source))
                return;

            options.Add(new ImperialFishingPortalOption
            {
                Key = id,
                Name = Loc.GetString($"imperial-fishing-portal-{id.ToLowerInvariant()}"),
                RadialState = source.RadialState,
            });
        }

        AddSource(DefaultSource);
        if (ent.Comp.Emagged)
            AddSource(SyndicateSource);

        if (ent.Comp.AllDestinations)
        {
            foreach (var source in _proto.EnumeratePrototypes<ImperialFishSourcePrototype>())
                AddSource(source.ID);
        }
        else if (_experiments.FindServer(ent) is { } server && TryComp<TgExperimentStateComponent>(server, out var state))
        {
            foreach (var (experiment, source) in ExperimentRewards)
            {
                if (state.Completed.Contains(experiment))
                    AddSource(source);
            }
        }

        var index = 1;
        foreach (var (spot, sourceId) in ent.Comp.Linked)
        {
            var source = _proto.Index<ImperialFishSourcePrototype>(sourceId);
            options.Add(new ImperialFishingPortalOption
            {
                Key = "linked:" + GetNetEntity(spot),
                Name = $"{index++} - {Name(spot)}",
                RadialState = source.RadialState,
                Linked = true,
            });
        }

        return options;
    }

    private void OnSelect(Entity<ImperialFishingPortalComponent> ent, ref ImperialFishingPortalSelectMessage args)
    {
        _ui.CloseUi(ent.Owner, ImperialFishingPortalUiKey.Key);
        if (args.Key.StartsWith("unlink:"))
        {
            if (NetEntity.TryParse(args.Key["unlink:".Length..], out var net) && GetEntity(net) is { Valid: true } spot)
            {
                Unlink(ent, spot);
                _popup.PopupEntity(Loc.GetString("imperial-fishing-portal-unlinked"), ent, args.Actor);
            }

            return;
        }

        var key = args.Key;
        if (ent.Comp.Active != null || GetOptions(ent).All(o => o.Key != key))
            return;

        Activate(ent, key, args.Actor);
    }

    /// <summary>activate().</summary>
    private void Activate(Entity<ImperialFishingPortalComponent> ent, string key, EntityUid user)
    {
        if (!Powered(ent))
        {
            _popup.PopupEntity(Loc.GetString("imperial-fishing-portal-no-power"), ent, user);
            return;
        }

        string sourceId;
        EntityUid? linked = null;
        if (key.StartsWith("linked:"))
        {
            if (!NetEntity.TryParse(key["linked:".Length..], out var net) || GetEntity(net) is not { Valid: true } spot
                || !ent.Comp.Linked.TryGetValue(spot, out var linkedSource))
                return;

            if (!ent.Comp.AllDestinations && !ent.Comp.LongRange && Transform(spot).MapID != Transform(ent).MapID)
            {
                _popup.PopupEntity(Loc.GetString("imperial-fishing-portal-cannot-reach"), ent, user);
                return;
            }

            sourceId = linkedSource;
            linked = spot;
        }
        else
        {
            sourceId = key;
        }

        ent.Comp.Active = sourceId;
        ent.Comp.LinkedAtom = linked;
        var spotComp = EnsureComp<ImperialFishingSpotComponent>(ent);
        spotComp.Source = sourceId;
        UpdateAppearance(ent);
    }

    /// <summary>deactivate().</summary>
    public void Deactivate(Entity<ImperialFishingPortalComponent> ent)
    {
        if (ent.Comp.Active == null)
            return;

        ent.Comp.Active = null;
        ent.Comp.LinkedAtom = null;
        RemComp<ImperialFishingSpotComponent>(ent);
        UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<ImperialFishingPortalComponent> ent)
    {
        var overlay = string.Empty;
        if (ent.Comp.Active is { } id && _proto.TryIndex<ImperialFishSourcePrototype>(id, out var source))
            overlay = source.OverlayState;

        _appearance.SetData(ent, ImperialFishingPortalVisuals.Overlay, overlay);
    }

    private void OnPowerChanged(Entity<ImperialFishingPortalComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            Deactivate(ent);
    }

    /// <summary>RefreshParts(): привязки — половина суммы уровней контейнеров материи, дальняя связь — конденсатор от 3-го уровня.</summary>
    private void OnRefreshParts(Entity<ImperialFishingPortalComponent> ent, ref Content.Server.Imperial.MachineParts.ImperialMachinePartsRefreshEvent args)
    {
        var bins = args.Ratings.Sum(Content.Shared.Imperial.MachineParts.ImperialStockPartType.MatterBin, 2);
        ent.Comp.MaxSpots = Math.Max(1, (int) MathF.Ceiling(bins * 0.5f));
        ent.Comp.LongRange = args.Ratings.Max(Content.Shared.Imperial.MachineParts.ImperialStockPartType.Capacitor) >= 3;

        if (ent.Comp.Linked.Count <= ent.Comp.MaxSpots)
            return;

        Deactivate(ent);
        foreach (var spot in ent.Comp.Linked.Keys.Skip(ent.Comp.MaxSpots).ToList())
            ent.Comp.Linked.Remove(spot);
    }

    private void OnShutdown(Entity<ImperialFishingPortalComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.Linked.Clear();
    }

    private void OnEmagged(Entity<ImperialFishingPortalComponent> ent, ref GotEmaggedEvent args)
    {
        if (args.Type != EmagType.Interaction || ent.Comp.Emagged)
            return;

        ent.Comp.Emagged = true;
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("imperial-fishing-portal-emagged"), ent, args.UserUid);
        _audio.PlayPvs("/Audio/Effects/sparks4.ogg", ent);
    }

    private void OnExamined(Entity<ImperialFishingPortalComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("imperial-fishing-portal-examine"));
        if (ent.Comp.Active != null)
            _spots.ExamineSpot(ent, args);
    }

    /// <summary>Источник привязанного места (аквариум, автомат) считает награду по самому месту.</summary>
    private void OnRollReward(Entity<ImperialFishingPortalComponent> ent, ref ImperialFishingRollRewardEvent args)
    {
        if (ent.Comp.LinkedAtom is not { } linked || TerminatingOrDeleted(linked))
            return;

        var ev = new ImperialFishingRollRewardEvent(args.Challenge, args.Rod);
        RaiseLocalEvent(linked, ref ev);
        args.RewardEntity = ev.RewardEntity;
        args.RewardProto = ev.RewardProto;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;

        _accumulator = 0;
        var query = EntityQueryEnumerator<ImperialFishingPortalComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = (uid, comp);
            foreach (var spot in comp.Linked.Keys.ToList())
            {
                if (TerminatingOrDeleted(spot) || _spots.GetSource(spot) == null)
                    Unlink(ent, spot);
            }

            // check_fishing_spot_z().
            if (comp.LinkedAtom is { } linked && !comp.LongRange && !comp.AllDestinations && Transform(linked).MapID != Transform(uid).MapID)
                Deactivate(ent);
        }
    }

    #endregion

    #region Привязка мультитулом

    private void OnInteractUsing(Entity<ImperialFishingPortalComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, Pulsing))
            return;

        args.Handled = true;
        if (!Powered(ent))
        {
            _popup.PopupEntity(Loc.GetString("imperial-fishing-portal-no-power"), ent, args.User);
            return;
        }

        if (TryComp<ImperialFishingPortalBufferComponent>(args.Used, out var buffer) && buffer.Portal == ent.Owner)
        {
            RemComp<ImperialFishingPortalBufferComponent>(args.Used);
            _popup.PopupEntity(Loc.GetString("imperial-fishing-portal-buffer-cleared"), ent, args.User);
            return;
        }

        EnsureComp<ImperialFishingPortalBufferComponent>(args.Used).Portal = ent;
        _popup.PopupEntity(Loc.GetString("imperial-fishing-portal-buffer-set"), ent, args.User);
    }

    /// <summary>link_fishing_spot().</summary>
    private void OnBufferAfterInteract(Entity<ImperialFishingPortalBufferComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || HasComp<ImperialFishingPortalComponent>(target))
            return;

        if (!TryComp<ImperialFishingPortalComponent>(ent.Comp.Portal, out var portal) || _spots.GetSource(target) is not { } source)
            return;

        args.Handled = true;
        if (portal.Linked.Count >= portal.MaxSpots)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fishing-portal-cannot-link-more"), target, args.User);
            return;
        }

        if (portal.Linked.ContainsValue(source.ID) || source.Special is "portalRandom" || source.ID.StartsWith("ImperialFishSourcePortal"))
        {
            _popup.PopupEntity(Loc.GetString(source.ID.StartsWith("ImperialFishSourcePortal")
                ? "imperial-fishing-portal-unlinkable"
                : "imperial-fishing-portal-already-linked"), target, args.User);
            _audio.PlayPvs("/Audio/Machines/buzz-sigh.ogg", ent);
            return;
        }

        portal.Linked[target] = source.ID;
        _popup.PopupEntity(Loc.GetString("imperial-fishing-portal-linked"), target, args.User);
        _audio.PlayPvs("/Audio/Machines/ping.ogg", target);
    }

    private void OnAltVerbs(Entity<ImperialFishingPortalComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Linked.Count == 0)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-fishing-portal-verb-unlink"),
            Act = () =>
            {
                var index = 1;
                var options = ent.Comp.Linked.Select(pair => new ImperialFishingPortalOption
                {
                    Key = "unlink:" + GetNetEntity(pair.Key),
                    Name = $"{Name(pair.Key)} ({index++})",
                    RadialState = _proto.Index<ImperialFishSourcePrototype>(pair.Value).RadialState,
                    Linked = true,
                }).ToList();

                _ui.OpenUi(ent.Owner, ImperialFishingPortalUiKey.Key, user);
                _ui.SetUiState(ent.Owner, ImperialFishingPortalUiKey.Key, new ImperialFishingPortalBoundUserInterfaceState(options, true));
            },
        });
    }

    /// <summary>unlink_fishing_spot().</summary>
    private void Unlink(Entity<ImperialFishingPortalComponent> ent, EntityUid spot)
    {
        if (!ent.Comp.Linked.Remove(spot))
            return;

        if (ent.Comp.LinkedAtom == spot)
            Deactivate(ent);
    }

    #endregion

    #region Выпуск рыбы

    /// <summary>TRAIT_CATCH_AND_RELEASE: вода, лава, лёд и работающий портал.</summary>
    public bool CanReleaseInto(EntityUid target)
    {
        if (TryComp<ImperialFishingPortalComponent>(target, out var portal))
            return portal.Active != null;

        return _spots.GetSource(target) is { } source && CatchAndReleaseSources.Contains(source.ID);
    }

    private void OnFishAfterInteract(Entity<ImperialFishComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !CanReleaseInto(target))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("imperial-fish-releasing"), target, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(3), new ImperialFishReleaseDoAfterEvent(), ent, target, ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnReleaseDoAfter(Entity<ImperialFishComponent> ent, ref ImperialFishReleaseDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        var key = ent.Comp.Dead ? "imperial-fish-released-dead" : "imperial-fish-released";
        _popup.PopupEntity(Loc.GetString(key, ("fish", ent.Owner), ("user", args.User), ("target", target)), target, args.User);
        _fish.Released(ent, target, args.User);
    }

    #endregion
}
