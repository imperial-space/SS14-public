using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Chaplain;
using Content.Shared.Imperial.Chaplain.Components;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Chaplain;

/// <summary>
/// Маяк вооружения капеллана (choice_beacon/holy из SS13).
/// </summary>
public sealed class HolyArmamentsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ImperialReligionSystem _religion = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");
    private static readonly SoundSpecifier SummonSound = new SoundPathSpecifier("/Audio/Effects/holy.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HolyArmamentsBeaconComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<HolyArmamentsBeaconComponent, HolyArmamentsPickMessage>(OnPick);
    }

    private void OnOpenAttempt(Entity<HolyArmamentsBeaconComponent> beacon, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<ImperialHolyComponent>(args.User))
        {
            args.Cancel();
            if (!args.Silent)
                _audio.PlayPvs(DenySound, beacon);
            return;
        }

        // Набор станции уже выбран — маяк сразу присылает его.
        if (_religion.StationArmamentsKit is { } kit)
        {
            args.Cancel();
            if (!args.Silent)
            {
                _popup.PopupEntity(Loc.GetString("holy-armaments-already-chosen"), beacon, args.User);
                Summon(beacon, args.User, kit);
            }
        }
    }

    private void OnPick(Entity<HolyArmamentsBeaconComponent> beacon, ref HolyArmamentsPickMessage args)
    {
        if (!HasComp<ImperialHolyComponent>(args.Actor) || !_hands.IsHolding(args.Actor, beacon))
            return;

        if (!_proto.TryIndex(args.Kit, out var proto)
            || proto.Abstract
            || !proto.TryGetComponent<HolyArmamentsKitComponent>(out _, EntityManager.ComponentFactory))
        {
            return;
        }

        var kit = _religion.StationArmamentsKit ?? proto.ID;
        _religion.StationArmamentsKit = kit;
        Summon(beacon, args.Actor, kit);
    }

    private void Summon(EntityUid beacon, EntityUid user, EntProtoId kit)
    {
        var box = Spawn(kit, Transform(user).Coordinates);
        _audio.PlayPvs(SummonSound, user);
        Del(beacon);
        _hands.PickupOrDrop(user, box);
    }
}
