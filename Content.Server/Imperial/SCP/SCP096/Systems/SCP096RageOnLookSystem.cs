using System.Numerics;
using Content.Server.Imperial.SCP.SCP096.Components;
using Content.Server.Imperial.SCP.SCPBlink.Components;
using Content.Server.Popups;
using Content.Shared.Imperial.SCP.SCP096;
using Robust.Shared.Audio.Systems;
using Content.Shared.Audio;
using Content.Shared.Doors.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.SCP.SCP096.Systems;

public sealed class SCP096RageOnLookSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SCP096RageOnLookComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMove);
        SubscribeLocalEvent<SCP096RageOnLookComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<SCP096RageOnLookComponent, IsEquippingTargetAttemptEvent>(OnIsEquippingTargetAttempt);
    }

    private void OnAttackAttempt(Entity<SCP096RageOnLookComponent> ent, ref AttackAttemptEvent args)
    {
        if (ent.Comp.IsEnraged)
        {
            if (args.Target is not { } target)
                return;

            // Allow attacking doors regardless of rage target
            if (HasComp<DoorComponent>(target))
                return;

            // Allow attacking rage targets
            if (ent.Comp.RageTargets.Contains(target))
                return;
        }

        // Block all other attacks (non-enraged or non-target entities)
        args.Cancel();
    }

    private void OnIsEquippingTargetAttempt(Entity<SCP096RageOnLookComponent> ent, ref IsEquippingTargetAttemptEvent args)
    {
        if (!ent.Comp.IsRageWindup)
            return;

        if (!HasComp<SCP096RageSuppressorComponent>(args.Equipment))
            return;

        args.Cancel();
        args.Reason = "inventory-component-can-equip-cannot";
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var curTime = _timing.CurTime;

        var query = EntityQueryEnumerator<SCP096RageOnLookComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var comp, out var mobState))
        {
            if (mobState.CurrentState == MobState.Dead)
            {
                if (comp.IsEnraged || comp.IsRageWindup)
                    ResetToCalm(uid, comp);

                UpdateStageAmbient(uid, comp, false);
                SetVisualState(uid, SCP096VisualState.Dead);
                continue;
            }

            if (IsRageSuppressed(uid))
            {
                if (comp.IsEnraged || comp.IsRageWindup)
                    ResetToCalm(uid, comp);

                UpdateStageAmbient(uid, comp, true);
                SetVisualState(uid, SCP096VisualState.Calm);
                continue;
            }

            PruneRageTargets(comp);

            var isBeingWatched = UpdateRageTargets(uid, comp);

            if (comp.IsRageWindup)
            {
                SetVisualState(uid, SCP096VisualState.Screaming);

                if (curTime < comp.RageWindupEndTime)
                    continue;

                comp.IsRageWindup = false;
                comp.IsEnraged = true;
                comp.RageEndTime = curTime + comp.RageDuration;
                _movement.RefreshMovementSpeedModifiers(uid);

                if (TryComp<MeleeWeaponComponent>(uid, out var enragedMelee))
                {
                    comp.OriginalAttackRate ??= enragedMelee.AttackRate;
                    enragedMelee.AttackRate = comp.EnragedAttackRate;
                    Dirty(uid, enragedMelee);
                }

                _popup.PopupEntity(Loc.GetString(comp.RagePopup), uid, PopupType.LargeCaution);
                UpdateStageAmbient(uid, comp, true);
                SetVisualState(uid, SCP096VisualState.Chasing);
                continue;
            }

            if (comp.IsEnraged)
            {
                if (curTime >= comp.RageEndTime)
                    ResetToCalm(uid, comp);

                UpdateStageAmbient(uid, comp, true);
                SetVisualState(uid, SCP096VisualState.Chasing);
                continue;
            }

            if (!isBeingWatched)
                continue;

            comp.IsRageWindup = true;
            comp.RageWindupEndTime = curTime + comp.RageWindup;
            _movement.RefreshMovementSpeedModifiers(uid);
            _popup.PopupEntity(Loc.GetString(comp.RageWindupPopup), uid, PopupType.MediumCaution);
            _audio.PlayPvs(comp.RageSound, uid);
            UpdateStageAmbient(uid, comp, true);
            SetVisualState(uid, SCP096VisualState.Screaming);
        }
    }

    private void ResetToCalm(EntityUid uid, SCP096RageOnLookComponent comp)
    {
        comp.IsEnraged = false;
        comp.IsRageWindup = false;
        comp.RageEndTime = TimeSpan.Zero;
        comp.RageWindupEndTime = TimeSpan.Zero;
        comp.RageTargets.Clear();

        _movement.RefreshMovementSpeedModifiers(uid);

        if (TryComp<MeleeWeaponComponent>(uid, out var calmMelee))
        {
            calmMelee.AttackRate = comp.OriginalAttackRate ?? comp.CalmAttackRate;
            comp.OriginalAttackRate = null;
            Dirty(uid, calmMelee);
        }

        _popup.PopupEntity(Loc.GetString(comp.RageCalmPopup), uid, PopupType.Medium);
        UpdateStageAmbient(uid, comp, true);
        SetVisualState(uid, SCP096VisualState.Calm);
    }

    private void SetVisualState(EntityUid uid, SCP096VisualState state)
    {
        _appearance.SetData(uid, SCP096Visuals.State, state);
    }

    private void OnRefreshMove(Entity<SCP096RageOnLookComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.IsRageWindup)
        {
            args.ModifySpeed(0f, 0f);
            return;
        }

        if (!ent.Comp.IsEnraged)
            return;

        args.ModifySpeed(ent.Comp.EnragedWalkModifier, ent.Comp.EnragedSprintModifier);
    }

    private bool UpdateRageTargets(EntityUid target, SCP096RageOnLookComponent comp)
    {
        var targetXform = Transform(target);
        var targetPosition = _transform.GetWorldPosition(targetXform);
        var targetMap = targetXform.MapID;
        var watched = false;

        foreach (var observer in _lookup.GetEntitiesInRange(target, comp.ObserveRadius, LookupFlags.Dynamic))
        {
            if (observer == target)
                continue;

            if (TryComp<SCPBlinkableComponent>(observer, out var blink) && blink.IsBlinking)
                continue;

            if (!TryComp<MobStateComponent>(observer, out var mobState) || mobState.CurrentState != MobState.Alive)
                continue;

            if (!TryComp<TransformComponent>(observer, out var observerTransform))
                continue;

            if (observerTransform.MapID != targetMap)
                continue;

            if (comp.RequireUnobstructed && !_interaction.InRangeUnobstructed(observer, target, comp.ObserveRadius + 0.1f))
                continue;

            var observerPosition = _transform.GetWorldPosition(observer);
            var toTarget = targetPosition - observerPosition;
            if (toTarget.LengthSquared() <= 0.001f)
                continue;

            var lookDirection = _transform.GetWorldRotation(observerTransform).ToWorldVec();
            var dot = Vector2.Dot(Vector2.Normalize(lookDirection), Vector2.Normalize(toTarget));
            if (dot >= comp.MinLookDot)
            {
                watched = true;
                comp.RageTargets.Add(observer);
            }
        }

        return watched;
    }

    private bool IsRageSuppressed(EntityUid uid)
    {
        if (_inventory.TryGetSlotEntity(uid, "head", out var headEnt) && HasComp<SCP096RageSuppressorComponent>(headEnt))
            return true;

        if (_inventory.TryGetSlotEntity(uid, "mask", out var maskEnt) && HasComp<SCP096RageSuppressorComponent>(maskEnt))
            return true;

        return false;
    }

    private void UpdateStageAmbient(EntityUid uid, SCP096RageOnLookComponent comp, bool alive)
    {
        if (!TryComp<AmbientSoundComponent>(uid, out var ambient))
            return;

        if (!alive)
        {
            _ambientSound.SetAmbience(uid, false, ambient);
            comp.UsingRageLoopSound = false;
            return;
        }

        if (comp.IsEnraged)
        {
            if (!comp.UsingRageLoopSound)
            {
                _ambientSound.SetSound(uid, comp.RageLoopSound, ambient);
                comp.UsingRageLoopSound = true;
            }

            _ambientSound.SetAmbience(uid, true, ambient);
            return;
        }

        if (comp.UsingRageLoopSound)
        {
            _ambientSound.SetSound(uid, comp.CryingSound, ambient);
            comp.UsingRageLoopSound = false;
        }

        _ambientSound.SetAmbience(uid, true, ambient);
    }

    private void PruneRageTargets(SCP096RageOnLookComponent comp)
    {
        comp.RageTargets.RemoveWhere(target => Deleted(target)
            || !TryComp<MobStateComponent>(target, out var mobState)
            || mobState.CurrentState != MobState.Alive);
    }
}
