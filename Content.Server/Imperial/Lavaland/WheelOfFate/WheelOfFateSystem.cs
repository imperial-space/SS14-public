using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Lavaland.WheelOfFate;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.WheelOfFate;

public sealed class WheelOfFateSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WheelOfFateComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<WheelOfFateComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnUseInHand(Entity<WheelOfFateComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled) return;
        TrySpin(ent, args.User);
        args.Handled = true;
    }

    private void OnInteractHand(Entity<WheelOfFateComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled) return;
        TrySpin(ent, args.User);
        args.Handled = true;
    }

    private void TrySpin(Entity<WheelOfFateComponent> ent, EntityUid user)
    {
        var now = _timing.CurTime;
        if (now < ent.Comp.NextSpinTime)
        {
            _popup.PopupEntity(Loc.GetString("wheel-of-fate-cooldown"), ent, user, PopupType.Small);
            return;
        }

        ent.Comp.NextSpinTime = now + ent.Comp.SpinCooldown;
        _audio.PlayPvs(new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Items/Dice/dice1.ogg"), ent);

        if (_random.Prob(ent.Comp.DiceChance))
        {
            SpawnNextToOrDrop("D20FateDice", ent, user);
            _popup.PopupEntity(Loc.GetString("wheel-of-fate-lucky"), ent, user, PopupType.LargeCaution);
            QueueDel(ent);
        }
        else
        {
            var dmg = new DamageSpecifier();
            dmg.DamageDict["Cellular"] = ent.Comp.FailureDamage;
            _damageable.TryChangeDamage(user, dmg, ignoreResistances: true);
            _popup.PopupEntity(Loc.GetString("wheel-of-fate-unlucky"), ent, user, PopupType.LargeCaution);
        }
    }

    private void SpawnNextToOrDrop(string proto, EntityUid structure, EntityUid user)
    {
        var coords = Transform(user).Coordinates;
        SpawnAtPosition(proto, coords);
    }
}
