using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Drunk;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Jittering;
using Content.Shared.Movement.Systems;
using Content.Shared.Speech.Muting;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.MartialArts;

/// <summary>Общие эффекты приёмов: урон, выносливость, оглушения, броски, обезоруживание.</summary>
public sealed partial class MartialArtsSystem
{
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly MovementModStatusSystem _movementMod = default!;
    [Dependency] private readonly SharedDrunkSystem _drunk = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly Content.Shared.StatusEffectNew.StatusEffectsSystem _newStatusEffects = default!;

    private static readonly EntProtoId ForcedSleeping = "StatusEffectForcedSleeping";
    private static readonly EntProtoId StaggeredEffect = "MartialStaggeredStatusEffect";

    /// <summary>STAGGERED_SLOWDOWN_STRENGTH.</summary>
    private const float StaggeredSlowdown = 0.85f;

    /// <summary>apply_damage(amount, BRUTE).</summary>
    public void Brute(EntityUid target, float amount, EntityUid? origin, string type = "Blunt")
    {
        if (amount <= 0)
            return;

        var spec = new DamageSpecifier();
        spec.DamageDict[type] = FixedPoint2.New(amount);
        _damage.TryChangeDamage(target, spec, origin: origin);
    }

    /// <summary>apply_damage(amount, STAMINA) / adjust_stamina_loss().</summary>
    public void Stamina(EntityUid target, float amount, EntityUid? origin = null)
    {
        _stamina.TakeStaminaDamage(target, amount, source: origin);
    }

    public void Oxy(EntityUid target, float amount, EntityUid? origin = null)
    {
        Brute(target, amount, origin, "Asphyxiation");
    }

    // Длительности приёмов записаны как в TG и переводятся в масштаб SS14 (ImperialControlNorms).

    /// <summary>Knockdown().</summary>
    public void Knockdown(EntityUid target, TimeSpan time)
    {
        _stun.TryKnockdown(target, Content.Shared.Imperial.Balance.ImperialControlNorms.Knockdown(time), true);
    }

    /// <summary>Paralyze(): сбит с ног и обездвижен.</summary>
    public void Paralyze(EntityUid target, TimeSpan time)
    {
        _stun.TryUpdateParalyzeDuration(target, Content.Shared.Imperial.Balance.ImperialControlNorms.HardStun(time));
    }

    /// <summary>Stun().</summary>
    public void Stun(EntityUid target, TimeSpan time)
    {
        _stun.TryUpdateStunDuration(target, Content.Shared.Imperial.Balance.ImperialControlNorms.HardStun(time));
    }

    /// <summary>Unconscious() / SetSleeping().</summary>
    public void Sleep(EntityUid target, TimeSpan time)
    {
        _newStatusEffects.TryAddStatusEffectDuration(target, ForcedSleeping, Content.Shared.Imperial.Balance.ImperialControlNorms.Sleep(time));
    }

    /// <summary>adjust_silence_up_to().</summary>
    public void Silence(EntityUid target, TimeSpan time)
    {
        _statusEffects.TryAddStatusEffect<MutedComponent>(target, "Muted", time, true);
    }

    /// <summary>adjust_confusion() / adjust_dizzy(): в SS14 ближе всего опьянение, как и у вирусологии.</summary>
    public void Confuse(EntityUid target, TimeSpan time)
    {
        _drunk.TryApplyDrunkenness(target, time);
    }

    /// <summary>adjust_temp_blindness().</summary>
    public void Blind(EntityUid target, TimeSpan time)
    {
        _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(target, TemporaryBlindnessSystem.BlindingStatusEffect, time, true);
    }

    /// <summary>set_jitter_if_lower().</summary>
    public void Jitter(EntityUid target, TimeSpan time)
    {
        _jitter.DoJitter(target, time, true);
    }

    /// <summary>adjust_staggered_up_to(): замедление на STAGGERED_SLOWDOWN_STRENGTH.</summary>
    public void Stagger(EntityUid target, TimeSpan time)
    {
        _movementMod.TryUpdateMovementSpeedModDuration(target, StaggeredEffect, time, StaggeredSlowdown);
    }

    public bool IsStaggered(EntityUid target)
    {
        return _newStatusEffects.HasStatusEffect(target, StaggeredEffect);
    }

    /// <summary>losebreath = clamp(losebreath + amount, 0, 10).</summary>
    public void LoseBreath(EntityUid target, int amount)
    {
        var comp = EnsureComp<MartialLoseBreathComponent>(target);
        if (comp.Ticks > 10)
            return;

        comp.Ticks = Math.Clamp(comp.Ticks + amount, 0, 10);
        comp.NextTick = _timing.CurTime;
    }

    /// <summary>throw_at(get_edge_target_turf(defender, dir), range, speed).</summary>
    public void ThrowAway(EntityUid target, EntityUid from, float range, float speed = 4f)
    {
        var dir = _xform.GetWorldPosition(target) - _xform.GetWorldPosition(from);
        if (dir.LengthSquared() < 0.001f)
            dir = new Vector2(1, 0);

        _throwing.TryThrow(target, Vector2.Normalize(dir) * range, speed * 2f, from, playSound: false);
    }

    /// <summary>drop_all_held_items().</summary>
    public void DropAll(EntityUid target)
    {
        if (!TryComp<HandsComponent>(target, out var hands))
            return;

        foreach (var hand in _hands.EnumerateHands((target, hands)))
        {
            _hands.TryDrop((target, hands), hand, checkActionBlocker: false);
        }
    }

    /// <summary>Выбить предмет из активной руки; если <paramref name="taker"/> задан — забрать себе.</summary>
    public EntityUid? Disarm(EntityUid target, EntityUid? taker)
    {
        if (!_hands.TryGetActiveItem(target, out var item))
            return null;

        if (!_hands.TryDrop(target, item.Value, checkActionBlocker: false))
            return null;

        if (taker != null)
            _hands.TryPickupAnyHand(taker.Value, item.Value);

        return item;
    }

    public void Say(EntityUid user, string message)
    {
        _chatSystem.TrySendInGameICMessage(user, message, InGameICChatType.Speak, false, ignoreActionBlocker: true);
    }

    public void Emote(EntityUid user, string emote)
    {
        _chatSystem.TryEmoteWithChat(user, emote, ignoreActionBlocker: true, forceEmote: true);
    }

    public void Sound(EntityUid at, string path, float volume = 0f)
    {
        _audio.PlayPvs(new SoundPathSpecifier(path), at, AudioParams.Default.WithVolume(volume));
    }

    public bool IsLying(EntityUid uid) => _standing.IsDown(uid);

    public bool IsHuman(EntityUid uid) => HasComp<HumanoidProfileComponent>(uid);

    public bool IsConscious(EntityUid uid) => _mobs.IsAlive(uid) && !HasComp<SleepingComponent>(uid);

    public bool IsStaminaCrit(EntityUid uid)
    {
        return TryComp<Content.Shared.Damage.Components.StaminaComponent>(uid, out var stamina) && stamina.Critical;
    }

    public float StaminaLoss(EntityUid uid)
    {
        return CompOrNull<Content.Shared.Damage.Components.StaminaComponent>(uid)?.StaminaDamage ?? 0f;
    }

    /// <summary>Тики сбитого дыхания и «рычага» дзюдо.</summary>
    private void UpdateEffects(TimeSpan now)
    {
        var breath = EntityQueryEnumerator<MartialLoseBreathComponent>();
        while (breath.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextTick)
                continue;

            if (comp.Ticks <= 0)
            {
                RemCompDeferred<MartialLoseBreathComponent>(uid);
                continue;
            }

            comp.Ticks--;
            comp.NextTick = now + StrangleInterval;
            Oxy(uid, 3f);
        }

        var armbar = EntityQueryEnumerator<JudoArmbarComponent>();
        while (armbar.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.ExpiresAt)
                RemCompDeferred<JudoArmbarComponent>(uid);
        }
    }
}
