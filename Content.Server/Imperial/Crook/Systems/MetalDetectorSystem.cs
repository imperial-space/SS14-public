// this content is under ICLA licence, read more on https://wiki.imperialspace.net/icla
// Copyright: @crookielv

using System.Linq;
using Content.Server.Imperial.Crook.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Contraband;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Crook.Visuals;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Crook.Systems;

public sealed class MetalDetectorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MetalDetectorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MetalDetectorComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<MetalDetectorComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<MetalDetectorComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<MetalDetectorComponent, EndCollideEvent>(OnEndCollide);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<MetalDetectorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.State is not (MetalDetectorVisualState.Scanning or MetalDetectorVisualState.Alert))
                continue;

            if (curTime < comp.NextStateReset)
                continue;

            ResetState((uid, comp));
        }
    }

    private void OnStartup(Entity<MetalDetectorComponent> ent, ref ComponentStartup args)
    {
        ResetState(ent);
    }

    private void OnPowerChanged(Entity<MetalDetectorComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            ent.Comp.CollidingEntities.Clear();

        SetState(ent, args.Powered ? MetalDetectorVisualState.Powered : MetalDetectorVisualState.Off);
    }

    private void OnEmagged(Entity<MetalDetectorComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(ent, EmagType.Interaction))
            return;

        args.Handled = true;
        SetState(ent, MetalDetectorVisualState.Alert, ent.Comp.AlertSound);
    }

    private void OnStartCollide(Entity<MetalDetectorComponent> ent, ref StartCollideEvent args)
    {
        if (!_power.IsPowered(ent))
            return;

        var target = args.OtherEntity;
        if (!HasComp<ItemComponent>(target) && !HasComp<InventoryComponent>(target))
            return;

        if (ent.Comp.CollidingEntities.Add(target))
            Scan(ent, target);
    }

    private void OnEndCollide(Entity<MetalDetectorComponent> ent, ref EndCollideEvent args)
    {
        if (ent.Comp.CollidingEntities.Remove(args.OtherEntity) && ent.Comp.CollidingEntities.Count == 0)
            ResetState(ent);
    }

    private void Scan(Entity<MetalDetectorComponent> ent, EntityUid target)
    {
        ent.Comp.NextStateReset = _timing.CurTime + ent.Comp.StateResetDelay;

        if (_emag.CheckFlag(ent, EmagType.Interaction))
        {
            Shock(ent, target);
            SetState(ent, MetalDetectorVisualState.Alert, ent.Comp.AlertSound);
            return;
        }

        if (HasAllowedAccess(ent.Comp, target))
        {
            SetState(ent, MetalDetectorVisualState.Scanning, ent.Comp.ClearSound);
            return;
        }

        Entity<IdCardComponent>? idCard = _idCard.TryFindIdCard(target, out var card) ? card : null;

        if (HasContraband(ent.Comp, target, idCard, 0))
            SetState(ent, MetalDetectorVisualState.Alert, ent.Comp.AlertSound);
        else
            SetState(ent, MetalDetectorVisualState.Scanning, ent.Comp.ClearSound);
    }

    private bool HasAllowedAccess(MetalDetectorComponent comp, EntityUid target)
    {
        var tags = _accessReader.FindAccessTags(target);
        return comp.AllowedAccess.Any(tags.Contains);
    }

    private bool HasContraband(MetalDetectorComponent comp, EntityUid uid, Entity<IdCardComponent>? idCard, int depth)
    {
        if (depth > comp.MaxRecursionDepth)
            return false;

        if (MetaData(uid).EntityPrototype is { } proto && comp.IgnoredPrototypes.Contains(proto.ID))
            return false;

        if (TryComp<ContrabandComponent>(uid, out var contraband) && !IsContrabandAllowed(contraband, idCard))
            return true;

        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            return false;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (HasContraband(comp, contained, idCard, depth + 1))
                    return true;
            }
        }

        return false;
    }

    private static bool IsContrabandAllowed(ContrabandComponent contraband, Entity<IdCardComponent>? idCard)
    {
        if (idCard is not { Comp: var card })
            return false;

        var allowedDepartments = contraband.AllowedDepartments;
        if (card.JobDepartments.Any(allowedDepartments.Contains))
            return true;

        var allowedJobs = contraband.AllowedJobs;
        return card.JobPrototype is { } job && allowedJobs.Contains(job);
    }

    private void Shock(Entity<MetalDetectorComponent> ent, EntityUid target)
    {
        if (!TryComp<DamageableComponent>(target, out var damageable))
            return;

        _damageable.TryChangeDamage((target, damageable), ent.Comp.ShockDamage);
        _stun.TryUpdateParalyzeDuration(target, ent.Comp.ShockDuration);
        _audio.PlayPvs(ent.Comp.ShockSound, ent);
        _popup.PopupEntity(Loc.GetString("metal-detector-electrocuted"), target, target, PopupType.SmallCaution);

        foreach (var slot in ent.Comp.DischargedSlots)
        {
            if (_inventory.TryGetSlotEntity(target, slot, out var item))
                Discharge(item.Value);
        }

        foreach (var held in _hands.EnumerateHeld(target))
        {
            Discharge(held);
        }
    }

    private void Discharge(EntityUid uid)
    {
        if (TryComp<BatteryComponent>(uid, out var battery))
            _battery.SetCharge((uid, battery), 0);

        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                Discharge(contained);
            }
        }
    }

    private void ResetState(Entity<MetalDetectorComponent> ent)
    {
        SetState(ent, _power.IsPowered(ent) ? MetalDetectorVisualState.Powered : MetalDetectorVisualState.Off);
    }

    private void SetState(Entity<MetalDetectorComponent> ent, MetalDetectorVisualState state, SoundSpecifier? sound = null)
    {
        if (sound != null)
            _audio.PlayPvs(sound, ent);

        if (ent.Comp.State == state)
            return;

        ent.Comp.State = state;
        _appearance.SetData(ent, MetalDetectorVisuals.State, state);
    }
}
