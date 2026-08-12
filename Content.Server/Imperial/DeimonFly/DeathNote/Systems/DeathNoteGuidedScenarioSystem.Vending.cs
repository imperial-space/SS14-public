using System.Numerics;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Presets;
using Content.Shared.Imperial.DeimonFly.DeathNote.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Systems;

public sealed partial class DeathNoteGuidedScenarioSystem
{
    private bool TryStartVendingFall(
        EntityUid vending,
        Entity<DeathNoteGuidedScenarioComponent> target,
        TimeSpan fallDuration)
    {
        if (Deleted(vending) || Deleted(target.Owner) || HasComp<DeathNoteFallingVendingComponent>(vending))
            return false;

        var vendingXform = Transform(vending);
        var targetXform = Transform(target.Owner);
        var targetMapCoordinates = _transform.GetMapCoordinates((target.Owner, targetXform));
        if (targetMapCoordinates.MapId == MapId.Nullspace || vendingXform.MapID != targetMapCoordinates.MapId)
            return false;

        if (!DeathNoteDamageHelper.TryCreate(
                target.Comp.MinimumDamage,
                target.Comp.MaximumDamage,
                _random,
                out var damage) ||
            !TryStartGuidedEffect(target))
        {
            return false;
        }

        var impactCoordinates = _transform.ToCoordinates(vendingXform.ParentUid, targetMapCoordinates);

        var falling = EnsureComp<DeathNoteFallingVendingComponent>(vending);
        falling.Target = target.Owner;
        falling.Parent = vendingXform.ParentUid;
        falling.StartPosition = vendingXform.LocalPosition;
        falling.ImpactPosition = impactCoordinates.Position;
        falling.StartRotation = vendingXform.LocalRotation;
        falling.StartedAt = _timing.CurTime;
        falling.ImpactAt = _timing.CurTime + fallDuration;
        falling.Damage = damage;
        falling.ImpactSound = target.Comp.VendingImpactSound;

        var visual = EnsureComp<DeathNoteFallenVendingVisualComponent>(vending);
        visual.FallDuration = fallDuration;
        DirtyField(vending, visual, nameof(DeathNoteFallenVendingVisualComponent.FallDuration));
        EnsureComp<DeathNoteVendingVictimComponent>(target.Owner).VendingMachine = vending;
        if (vendingXform.Anchored)
            _transform.Unanchor(vending, vendingXform);
        return true;
    }

    private void UpdateFallingVending(
        EntityUid uid,
        DeathNoteFallingVendingComponent falling,
        TransformComponent xform)
    {
        if (Deleted(falling.Parent))
        {
            RemCompDeferred<DeathNoteFallingVendingComponent>(uid);
            return;
        }

        var duration = Math.Max((falling.ImpactAt - falling.StartedAt).TotalSeconds, 0.001);
        var elapsed = (_timing.CurTime - falling.StartedAt).TotalSeconds;
        var progress = (float) Math.Clamp(elapsed / duration, 0d, 1d);
        var position = Vector2.Lerp(falling.StartPosition, falling.ImpactPosition, progress);
        _transform.SetCoordinates(uid, new EntityCoordinates(falling.Parent, position));
        _transform.SetLocalRotation(
            uid,
            falling.StartRotation + new Angle(MathHelper.PiOver2 * progress),
            xform);

        if (_timing.CurTime < falling.ImpactAt)
            return;

        if (!Deleted(falling.Target))
        {
            if (!TryComp(falling.Target, out MobStateComponent? targetState) ||
                !_mobState.IsDead(falling.Target, targetState))
            {
                _damageable.TryChangeDamage(
                    falling.Target,
                    new Content.Shared.Damage.DamageSpecifier(falling.Damage),
                    ignoreResistances: true,
                    interruptsDoAfters: true,
                    origin: uid,
                    ignoreGlobalModifiers: true);
            }

            RemCompDeferred<DeathNoteVendingVictimComponent>(falling.Target);
        }

        if (falling.ImpactSound != null)
            _audio.PlayPvs(falling.ImpactSound, uid);
        RemCompDeferred<DeathNoteFallingVendingComponent>(uid);
    }

    private void OnVendingVictimCanMove(
        Entity<DeathNoteVendingVictimComponent> ent,
        ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnFallingVendingShutdown(
        Entity<DeathNoteFallingVendingComponent> ent,
        ref ComponentShutdown args)
    {
        if (!Deleted(ent.Comp.Target))
            RemCompDeferred<DeathNoteVendingVictimComponent>(ent.Comp.Target);
    }
}
