using Content.Shared.Damage;
using Content.Shared.Emag.Systems;
using Content.Shared.Imperial.Security;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Security;

/// <summary>
/// gun/projectile/automatic/paintball_gun (Paradise): цвет выбирается мультитулом,
/// емаг перегружает компрессор, магазин перцовых шариков стреляет слезоточивым облачком.
/// </summary>
public sealed class PaintballSystem : EntitySystem
{
    private const string MagazineSlot = "gun_magazine";

    private static readonly (string Name, Color Color, PaintballGunOverlay Full, PaintballGunOverlay Empty)[] Colors =
    {
        ("blue", Color.FromHex("#0000CC"), PaintballGunOverlay.Blue, PaintballGunOverlay.BlueEmpty),
        ("red", Color.FromHex("#CC0000"), PaintballGunOverlay.Red, PaintballGunOverlay.RedEmpty),
        ("green", Color.FromHex("#00CC00"), PaintballGunOverlay.Green, PaintballGunOverlay.GreenEmpty),
        ("yellow", Color.FromHex("#CCCC00"), PaintballGunOverlay.Yellow, PaintballGunOverlay.YellowEmpty),
    };

    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaintballGunComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PaintballGunComponent, EntInsertedIntoContainerMessage>(OnMagazineChanged);
        SubscribeLocalEvent<PaintballGunComponent, EntRemovedFromContainerMessage>(OnMagazineChanged);
        SubscribeLocalEvent<PaintballGunComponent, GunShotEvent>(OnShot);
        SubscribeLocalEvent<PaintballGunComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PaintballGunComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<PaintballGunComponent, Content.Shared.Examine.ExaminedEvent>(OnExamined);

        SubscribeLocalEvent<PaintballProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnMapInit(Entity<PaintballGunComponent> ent, ref MapInitEvent args)
    {
        UpdateVisuals(ent);
    }

    private void OnMagazineChanged(Entity<PaintballGunComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateVisuals(ent);
    }

    private void OnMagazineChanged(Entity<PaintballGunComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateVisuals(ent);
    }

    private EntityUid? GetMagazine(EntityUid gun)
    {
        return _container.TryGetContainer(gun, MagazineSlot, out var slot) && slot.ContainedEntities.Count > 0
            ? slot.ContainedEntities[0]
            : null;
    }

    /// <summary>update_icon_state() и update_overlays().</summary>
    private void UpdateVisuals(Entity<PaintballGunComponent> ent)
    {
        if (GetMagazine(ent) is not { } mag)
        {
            _appearance.SetData(ent, PaintballGunVisuals.Overlay, PaintballGunOverlay.None);
            _item.SetHeldPrefix(ent, null);
            return;
        }

        var ev = new GetAmmoCountEvent();
        RaiseLocalEvent(mag, ref ev);
        var empty = ev.Count == 0;

        if (HasComp<PepperballMagazineComponent>(mag))
        {
            _appearance.SetData(ent, PaintballGunVisuals.Overlay, empty ? PaintballGunOverlay.PepperEmpty : PaintballGunOverlay.Pepper);
            _item.SetHeldPrefix(ent, "pepper");
            return;
        }

        var color = Colors[ent.Comp.ColorIndex % Colors.Length];
        _appearance.SetData(ent, PaintballGunVisuals.Overlay, empty ? color.Empty : color.Full);
        _item.SetHeldPrefix(ent, color.Name);
    }

    /// <summary>chamber_round() → color_paintball(): шарик окрашивается, после емага бьёт на 8.</summary>
    private void OnShot(Entity<PaintballGunComponent> ent, ref GunShotEvent args)
    {
        var color = Colors[ent.Comp.ColorIndex % Colors.Length].Color;
        foreach (var (uid, _) in args.Ammo)
        {
            if (uid is not { } projectile)
                continue;

            if (HasComp<PaintballProjectileComponent>(projectile))
                EnsureComp<PaintballColorComponent>(projectile).Color = color;

            if (ent.Comp.Emagged && TryComp<ProjectileComponent>(projectile, out var proj))
                proj.Damage = new DamageSpecifier { DamageDict = { ["Blunt"] = 8 } };
        }

        UpdateVisuals(ent);
    }

    /// <summary>multitool_act(): выбрать цвет краски (кроме перцовых шариков).</summary>
    private void OnInteractUsing(Entity<PaintballGunComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, "Pulsing"))
            return;

        args.Handled = true;
        if (GetMagazine(ent) is not { } mag || HasComp<PepperballMagazineComponent>(mag))
            return;

        ent.Comp.ColorIndex = (ent.Comp.ColorIndex + 1) % Colors.Length;
        _popup.PopupEntity(Loc.GetString($"paintball-color-{Colors[ent.Comp.ColorIndex].Name}"), ent, args.User);
        UpdateVisuals(ent);
    }

    private void OnEmagged(Entity<PaintballGunComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction) || ent.Comp.Emagged)
            return;

        ent.Comp.Emagged = true;
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("paintball-emagged", ("gun", ent.Owner)), ent, args.UserUid, PopupType.MediumCaution);
    }

    private void OnExamined(Entity<PaintballGunComponent> ent, ref Content.Shared.Examine.ExaminedEvent args)
    {
        if (GetMagazine(ent) is { } mag && HasComp<PepperballMagazineComponent>(mag))
            args.PushMarkup(Loc.GetString("paintball-examine-pepper"));
        else
            args.PushMarkup(Loc.GetString("paintball-examine-color",
                ("color", Loc.GetString($"paintball-color-name-{Colors[ent.Comp.ColorIndex % Colors.Length].Name}"))));

        if (ent.Comp.Emagged)
            args.PushMarkup(Loc.GetString("paintball-examine-emagged"));
    }

    /// <summary>projectile/paintball/on_hit(): пятно краски под целью.</summary>
    private void OnProjectileHit(Entity<PaintballProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        var splat = Spawn(ent.Comp.Splat, Transform(args.Target).Coordinates);
        EnsureComp<PaintballColorComponent>(splat).Color = CompOrNull<PaintballColorComponent>(ent)?.Color ?? Color.White;
        Dirty(splat, Comp<PaintballColorComponent>(splat));
    }
}
