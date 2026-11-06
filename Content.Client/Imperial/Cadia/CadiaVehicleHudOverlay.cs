using System.Numerics;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Cadia;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.Imperial.Cadia;

/// <summary>Табло экипажа внизу экрана: машина, место, корпус, оружие и боезапас, перезарядка, дым, башня.</summary>
public sealed class CadiaVehicleHudOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly Color Green = Color.FromHex("#5aff78");
    private static readonly Color Amber = Color.FromHex("#ffd37a");
    private static readonly Color Red = Color.FromHex("#ff3a2a");
    private static readonly Color Panel = Color.FromHex("#0b0d0ad8");

    private readonly Font _font;
    private readonly Font _small;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public CadiaVehicleHudOverlay()
    {
        IoCManager.InjectDependencies(this);
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), 13);
        _small = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 11);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local || !_entMan.TryGetComponent<CadiaVehicleCrewComponent>(local, out var crew) ||
            !_entMan.TryGetComponent<CadiaVehicleComponent>(crew.Vehicle, out var vehicle))
            return;

        var handle = args.ScreenHandle;
        // Сверху, под панелью войны: снизу табло закрывали бы руки и инвентарь. Оружие — строками под экипажем,
        // у огневой точки экипажа и дыма нет, и оружие идёт сразу под корпусом.
        var infoRows = vehicle.GunPost ? 0 : 2;
        var weaponsTop = 44 + infoRows * 18;
        var weaponRows = Math.Min(vehicle.Weapons.Count, vehicle.Ammo.Count);
        var height = weaponsTop + weaponRows * 20 + (vehicle.Firing ? 22 : 6);
        var box = UIBox2.FromDimensions(new Vector2(args.ViewportBounds.Center.X - 230, args.ViewportBounds.Top + 148), new Vector2(460, height));
        handle.DrawRect(box, Panel);
        handle.DrawRect(box, Amber, false);

        var name = _entMan.GetComponent<MetaDataComponent>(crew.Vehicle).EntityName;
        var seat = Loc.GetString(vehicle.GunPost ? "cadia-vehicle-seat-gunner" : $"cadia-vehicle-seat-{crew.Seat.ToString().ToLowerInvariant()}");
        handle.DrawString(_font, box.TopLeft + new Vector2(10, 6), $"{name} — {seat}", Amber);

        // Корпус: полоса прочности до порога разрушения.
        var integrity = 1f;
        if (_entMan.TryGetComponent<DamageableComponent>(crew.Vehicle, out _))
        {
            var damage = (float)_entMan.System<DamageableSystem>().GetTotalDamage(crew.Vehicle);
            var max = vehicle.MaxIntegrity;
            if (max > 0)
                integrity = Math.Clamp(1f - damage / max, 0f, 1f);
        }
        var bar = UIBox2.FromDimensions(box.TopLeft + new Vector2(10, 28), new Vector2(200, 10));
        handle.DrawRect(bar, Color.FromHex("#2a2a24"));
        handle.DrawRect(UIBox2.FromDimensions(bar.TopLeft, new Vector2(200 * integrity, 10)), integrity > 0.5f ? Green : integrity > 0.25f ? Amber : Red);
        handle.DrawRect(bar, Amber.WithAlpha(0.6f), false);
        handle.DrawString(_small, bar.TopRight + new Vector2(8, -3), Loc.GetString("cadia-vehicle-hud-hull", ("percent", (int)(integrity * 100))), Color.White);

        // Экипаж и дым — только у машин.
        if (!vehicle.GunPost)
        {
            handle.DrawString(_small, box.TopLeft + new Vector2(10, 44),
                Loc.GetString("cadia-vehicle-hud-crew", ("driver", vehicle.Driver != null ? "●" : "○"), ("gunner", vehicle.Gunner != null ? "●" : "○"),
                    ("passengers", vehicle.PassengerCount), ("max", vehicle.Passengers)), Color.FromHex("#b8c8b8"));
            handle.DrawString(_small, box.TopLeft + new Vector2(10, 62), Loc.GetString("cadia-vehicle-hud-smoke", ("charges", vehicle.SmokeCharges)), Color.FromHex("#b8c8b8"));
        }

        // Оружие: выбранное, боезапас, перезарядка.
        var now = _timing.CurTime;
        var x = box.Left + 10;
        for (var i = 0; i < vehicle.Weapons.Count && i < vehicle.Ammo.Count; i++)
        {
            var weapon = vehicle.Weapons[i];
            var selected = i == vehicle.SelectedWeapon;
            var y = box.Top + weaponsTop + i * 20;
            var left = vehicle.NextShot.Count > i ? vehicle.NextShot[i] - now : TimeSpan.Zero;
            // Основное орудие: снаряд в казённике и остаток в боеукладке вместо счётчика патронов.
            var ammo = weapon.Breech ? vehicle.RackCount : vehicle.Ammo[i];
            var dry = weapon.Breech ? !vehicle.Loaded : vehicle.Ammo[i] <= 0;
            var state = dry ? Loc.GetString(weapon.Breech ? "cadia-vehicle-hud-unloaded" : "cadia-vehicle-hud-empty")
                : left > TimeSpan.FromSeconds(0.3) ? Loc.GetString("cadia-vehicle-hud-reload", ("seconds", left.TotalSeconds.ToString("0.0")))
                : Loc.GetString("cadia-vehicle-hud-ready");
            var color = dry ? Red : left > TimeSpan.FromSeconds(0.3) ? Amber : Green;
            handle.DrawString(selected ? _font : _small, new Vector2(x, y), $"{(selected ? "▶ " : "  ")}{Loc.GetString(weapon.Name)}: {ammo} — {state}", selected ? color : color.WithAlpha(0.6f));
        }
        if (vehicle.Firing)
            handle.DrawString(_font, new Vector2(x, box.Bottom - 22), Loc.GetString("cadia-aa-ui-firing"), Red);
    }
}
