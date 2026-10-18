using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mindshield.Components;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Network;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>
/// Бойки TG (code/modules/projectiles/pins.dm): вставляются в оружие и ограничивают, кто и где может стрелять.
/// </summary>
public sealed class ImperialFiringPinSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly INetManager _net = default!;

    /// <summary>Дальность от мишени тира, в которой работает тировый боёк.</summary>
    private const float TestRangeRadius = 7f;

    public override void Initialize()
    {
        SubscribeLocalEvent<ImperialFiringPinComponent, AfterInteractEvent>(OnPinInteract);
        SubscribeLocalEvent<ImperialFiringPinnedComponent, ShotAttemptedEvent>(OnPinnedShot);
        SubscribeLocalEvent<ImperialFiringPinnedComponent, ExaminedEvent>(OnPinnedExamined);
    }

    private void OnPinInteract(Entity<ImperialFiringPinComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } gun || !HasComp<GunComponent>(gun))
            return;

        args.Handled = true;

        if (HasComp<ImperialFiringPinnedComponent>(gun))
        {
            _popup.PopupClient(Loc.GetString("imperial-firing-pin-already"), gun, args.User);
            return;
        }

        var pinned = EnsureComp<ImperialFiringPinnedComponent>(gun);
        pinned.Mode = ent.Comp.Mode;
        Dirty(gun, pinned);

        _popup.PopupClient(Loc.GetString("imperial-firing-pin-installed", ("gun", gun)), gun, args.User);
        if (_net.IsServer)
            QueueDel(ent);
    }

    private void OnPinnedShot(Entity<ImperialFiringPinnedComponent> ent, ref ShotAttemptedEvent args)
    {
        if (CanFire(ent.Comp.Mode, args.User, ent))
            return;

        args.Cancel();
        _popup.PopupClient(Loc.GetString($"imperial-firing-pin-denied-{ent.Comp.Mode.ToString().ToLowerInvariant()}"), ent, args.User);
    }

    private void OnPinnedExamined(Entity<ImperialFiringPinnedComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString($"imperial-firing-pin-examine-{ent.Comp.Mode.ToString().ToLowerInvariant()}"));
    }

    private bool CanFire(ImperialFiringPinMode mode, EntityUid user, EntityUid gun)
    {
        switch (mode)
        {
            case ImperialFiringPinMode.Mindshield:
                return HasComp<MindShieldComponent>(user);

            case ImperialFiringPinMode.Explorer:
                // Не стреляет на гридах станции.
                var grid = Transform(user).GridUid;
                return grid == null || !HasComp<StationMemberComponent>(grid);

            case ImperialFiringPinMode.TestRange:
                // В SS14 нет зон «тир»: стрелять можно рядом с мишенями (TargetHuman, TargetClown и т.д.).
                foreach (var target in _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(user), TestRangeRadius))
                {
                    var proto = MetaData(target).EntityPrototype?.ID;
                    if (proto != null && proto.StartsWith("Target") && proto != "TargetDarts")
                        return true;
                }
                return false;
        }

        return true;
    }
}
