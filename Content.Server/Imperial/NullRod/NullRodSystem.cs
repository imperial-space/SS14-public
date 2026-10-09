using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.NullRod;
using Content.Shared.Imperial.NullRod.Components;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.NullRod;

/// <summary>
/// Превращение нулевого стержня в выбранную форму святого оружия (subtype_picker из SS13).
/// </summary>
public sealed class NullRodSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    /// <summary>Святое оружие станции в этом раунде (GLOB.holy_weapon_type в SS13).</summary>
    private EntProtoId? _stationHolyWeapon;

    /// <summary>Выбранное в этом раунде святое оружие станции, если его уже выбрали.</summary>
    public EntProtoId? StationHolyWeapon => _stationHolyWeapon;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NullRodComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<NullRodComponent, NullRodPickVariantMessage>(OnPickVariant);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _stationHolyWeapon = null;
    }

    private void OnOpenAttempt(Entity<NullRodComponent> rod, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!rod.Comp.StationHolyItem || _stationHolyWeapon == null)
            return;

        args.Cancel();
        if (!args.Silent)
            _popup.PopupEntity(Loc.GetString("null-rod-already-chosen"), rod, args.User);
    }

    private void OnPickVariant(Entity<NullRodComponent> rod, ref NullRodPickVariantMessage args)
    {
        var user = args.Actor;
        if (rod.Comp.StationHolyItem && _stationHolyWeapon != null)
            return;

        if (!_hands.IsHolding(user, rod, out var hand))
            return;

        if (!_proto.TryIndex(args.Variant, out var proto)
            || proto.Abstract
            || !proto.TryGetComponent<NullRodVariantComponent>(out var variant, EntityManager.ComponentFactory)
            || !variant.ChaplainSpawnable)
        {
            return;
        }

        _ui.CloseUi(rod.Owner, NullRodUiKey.Key);

        var weapon = Spawn(proto.ID, Transform(user).Coordinates);
        Del(rod);
        _hands.TryPickup(user, weapon, hand);

        foreach (var extra in variant.SpawnOnPick)
        {
            _hands.PickupOrDrop(user, Spawn(extra, Transform(user).Coordinates));
        }

        if (rod.Comp.StationHolyItem)
            _stationHolyWeapon = proto.ID;

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):user} turned a null rod into {ToPrettyString(weapon):weapon}");
    }
}
