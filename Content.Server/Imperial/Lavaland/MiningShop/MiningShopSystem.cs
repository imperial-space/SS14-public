using Content.Server.Chat.Systems;
using Content.Server.Imperial.Lavaland.Gps;
using Content.Server.Imperial.Lavaland.Megafauna;
using Content.Server.Imperial.Lavaland.MegafaunaTracker;
using Content.Server.Imperial.Lavaland.Storm;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Access.Systems;
using Content.Shared.Administration.Systems;
using Content.Shared.Chat;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Lavaland;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.MiningShop;

/// <summary>Предметы шахтёрского магазина из SS13: инжектор Лазаря, браслеты Кхейрала, погодное радио.</summary>
public sealed class MiningShopSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly RadioSystem _radio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LazarusInjectorComponent, AfterInteractEvent>(OnLazarusInteract);
        SubscribeLocalEvent<LazarusInjectorComponent, ExaminedEvent>(OnLazarusExamined);
        SubscribeLocalEvent<LazarusInjectorComponent, EmpPulseEvent>(OnLazarusEmp);

        SubscribeLocalEvent<KheiralCuffsComponent, GotEquippedEvent>(OnCuffsEquipped);
        SubscribeLocalEvent<KheiralCuffsComponent, GotUnequippedEvent>(OnCuffsUnequipped);
        SubscribeLocalEvent<KheiralCuffsComponent, ExaminedEvent>(OnCuffsExamined);

        SubscribeLocalEvent<WeatherRadioComponent, MapInitEvent>((uid, comp, _) => UpdateRadioVisuals(uid, comp));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var cuffs = EntityQueryEnumerator<KheiralCuffsComponent>();
        while (cuffs.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextCheck)
                continue;
            comp.NextCheck = now + TimeSpan.FromSeconds(1);
            UpdateCuffs((uid, comp));
        }

        var radios = EntityQueryEnumerator<WeatherRadioComponent>();
        while (radios.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextCheck)
                continue;
            comp.NextCheck = now + TimeSpan.FromSeconds(1);
            UpdateRadio(uid, comp);
        }
    }

    #region Lazarus

    /// <summary>interact_with_atom: только мёртвые животные, не боссы и не гуманоиды.</summary>
    private void OnLazarusInteract(Entity<LazarusInjectorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !ent.Comp.Loaded || args.Target is not { } target ||
            !HasComp<MobStateComponent>(target))
            return;
        args.Handled = true;

        if (HasComp<HumanoidProfileComponent>(target) || HasComp<MegafaunaAiComponent>(target) ||
            HasComp<LavalandMegafaunaComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("lazarus-invalid"), target, args.User);
            return;
        }

        if (!_mobState.IsDead(target))
        {
            _popup.PopupEntity(Loc.GetString("lazarus-not-dead"), target, args.User);
            return;
        }

        // lazarus_revive: оживает и дружит с экипажем; неисправный — враг всем.
        _rejuvenate.PerformRejuvenate(target);
        if (TryComp<NpcFactionMemberComponent>(target, out var faction))
        {
            _faction.ClearFactions((target, faction));
            _faction.AddFaction((target, faction), ent.Comp.Malfunctioning ? "AllHostile" : "PetsNT");
        }

        _popup.PopupEntity(Loc.GetString("lazarus-revived", ("user", args.User), ("target", target), ("injector", ent.Owner)), target, PopupType.Medium);
        _audio.PlayPvs(ent.Comp.UseSound, ent);
        ent.Comp.Loaded = false;
        _appearance.SetData(ent, MegafaunaVisuals.State, "lazarus_empty");
    }

    private void OnLazarusExamined(Entity<LazarusInjectorComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.Loaded)
            args.PushMarkup(Loc.GetString("lazarus-empty"));
        if (ent.Comp.Malfunctioning)
            args.PushMarkup(Loc.GetString("lazarus-malfunction"));
    }

    private void OnLazarusEmp(Entity<LazarusInjectorComponent> ent, ref EmpPulseEvent args)
    {
        ent.Comp.Malfunctioning = true;
        args.Affected = true;
    }

    #endregion

    #region Kheiral cuffs

    private void OnCuffsEquipped(Entity<KheiralCuffsComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & SlotFlags.GLOVES) == 0)
            return;
        ent.Comp.Wearer = args.EquipTarget;
        _audio.PlayPvs(ent.Comp.EquipSound, ent);
        UpdateCuffs(ent);
    }

    private void OnCuffsUnequipped(Entity<KheiralCuffsComponent> ent, ref GotUnequippedEvent args)
    {
        if (ent.Comp.Wearer == null)
            return;
        ent.Comp.Wearer = null;
        _audio.PlayPvs(ent.Comp.EquipSound, ent);
        UpdateCuffs(ent);
    }

    private void OnCuffsExamined(Entity<KheiralCuffsComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.GpsEnabled)
            args.PushMarkup(Loc.GetString("kheiral-gps-on"));
    }

    private bool OnStation(EntityUid uid)
    {
        var map = Transform(uid).MapUid;
        var query = EntityQueryEnumerator<StationMemberComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapUid == map)
                return true;
        }

        return false;
    }

    /// <summary>connect/remove_kheiral_network: GPS только на руке и вдали от станции.</summary>
    private void UpdateCuffs(Entity<KheiralCuffsComponent> ent)
    {
        var shouldEnable = ent.Comp.Wearer is { } wearer && !TerminatingOrDeleted(wearer) && !OnStation(ent);
        if (shouldEnable == ent.Comp.GpsEnabled)
            return;

        ent.Comp.GpsEnabled = shouldEnable;
        if (shouldEnable)
        {
            var name = _idCard.TryFindIdCard(ent.Comp.Wearer!.Value, out var id) && id.Comp.FullName is { } full
                ? full
                : Loc.GetString("kheiral-unknown");
            EnsureComp<ImperialGpsComponent>(ent).Tag = Loc.GetString("kheiral-gps-tag", ("name", name));
            _popup.PopupEntity(Loc.GetString("kheiral-gps-activated"), ent, ent.Comp.Wearer!.Value);
        }
        else
        {
            RemComp<ImperialGpsComponent>(ent);
            if (ent.Comp.Wearer is { } user)
                _popup.PopupEntity(Loc.GetString("kheiral-gps-deactivated"), ent, user);
        }
    }

    #endregion

    #region Weather radio

    /// <summary>set_current_alert_level по состоянию бури лаваленда.</summary>
    private (WeatherAlertLevel Level, bool Dangerous) GetAlert(WeatherRadioComponent comp)
    {
        var query = EntityQueryEnumerator<LavalandMapComponent>();
        while (query.MoveNext(out _, out var map))
        {
            return map.StormState switch
            {
                LavalandStormState.Idle => (map.StormTimer <= comp.ClearDelay ? WeatherAlertLevel.Incoming : WeatherAlertLevel.Clear, true),
                LavalandStormState.Warning or LavalandStormState.Active => (WeatherAlertLevel.ImminentOrActive, true),
                LavalandStormState.PassingBy => (WeatherAlertLevel.ImminentOrActive, false),
                _ => (WeatherAlertLevel.Clear, true),
            };
        }

        return (WeatherAlertLevel.Clear, true);
    }

    private void UpdateRadio(EntityUid uid, WeatherRadioComponent comp)
    {
        var (level, dangerous) = GetAlert(comp);
        if (level == comp.Level && dangerous == comp.Dangerous)
            return;

        comp.Level = level;
        comp.Dangerous = dangerous;

        var message = !dangerous
            ? Loc.GetString("weather-radio-not-dangerous")
            : Loc.GetString(level switch
            {
                WeatherAlertLevel.Incoming => "weather-radio-incoming",
                WeatherAlertLevel.ImminentOrActive => "weather-radio-imminent",
                _ => "weather-radio-clear",
            });

        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Speak, ChatTransmitRange.Normal, hideLog: true, ignoreActionBlocker: true);
        _radio.SendRadioMessage(uid, message, comp.Channel, uid);
        UpdateRadioVisuals(uid, comp);
    }

    private void UpdateRadioVisuals(EntityUid uid, WeatherRadioComponent comp)
    {
        var (state, color) = comp.Level switch
        {
            WeatherAlertLevel.Incoming => ("urgentwarning", Color.FromHex("#FFCC66")),
            WeatherAlertLevel.ImminentOrActive when comp.Dangerous => ("direwarning", Color.FromHex("#FF3333")),
            WeatherAlertLevel.ImminentOrActive => ("urgentwarning", Color.FromHex("#FFCC66")),
            _ => ("weatherwarning", Color.FromHex("#64C864")),
        };
        _appearance.SetData(uid, MegafaunaVisuals.Overlay, state);
        if (_light.TryGetLight(uid, out var light))
            _light.SetColor(uid, color, light);
    }

    #endregion
}
