using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.NullRod.Components;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Mech.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.NullRod;

/// <summary>
/// Шанс отбить атаку святым оружием в руке (block_chance / hit_reaction из SS13).
/// </summary>
/// <remarks>
/// Удар ближнего боя распознаётся по AttackedEvent, который приходит перед уроном от того же атакующего
/// в тот же тик. Оружие, блокирующее любые атаки (посохи), отбивает любой урон, у которого есть атакующий.
/// </remarks>
public sealed class NullRodBlockSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NullRodBlockComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<NullRodBlockComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<NullRodBlockerComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<NullRodBlockerComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnEquipped(Entity<NullRodBlockComponent> weapon, ref GotEquippedHandEvent args)
    {
        EnsureComp<NullRodBlockerComponent>(args.User);
    }

    private void OnUnequipped(Entity<NullRodBlockComponent> weapon, ref GotUnequippedHandEvent args)
    {
        foreach (var held in _hands.EnumerateHeld(args.User))
        {
            if (held != weapon.Owner && HasComp<NullRodBlockComponent>(held))
                return;
        }

        RemCompDeferred<NullRodBlockerComponent>(args.User);
    }

    private void OnAttacked(Entity<NullRodBlockerComponent> holder, ref AttackedEvent args)
    {
        var isMech = HasComp<MechComponent>(args.User) || HasComp<MechPilotComponent>(args.User);
        foreach (var held in _hands.EnumerateHeld(holder.Owner))
        {
            if (!TryComp<NullRodBlockComponent>(held, out var block) || !block.MeleeOnly)
                continue;

            if (block.MechsOnly && !isMech)
                continue;

            if (!RollBlock((held, block)))
                continue;

            holder.Comp.BlockedAttacker = args.User;
            holder.Comp.BlockingWeapon = held;
            holder.Comp.BlockTick = _timing.CurTick;
            return;
        }
    }

    private void OnDamageModify(Entity<NullRodBlockerComponent> holder, ref DamageModifyEvent args)
    {
        if (args.Origin is not { } attacker || attacker == holder.Owner || !IsHarmful(args.Damage))
            return;

        // Удар ближнего боя, который уже решено отбить.
        if (holder.Comp.BlockedAttacker == attacker && holder.Comp.BlockTick == _timing.CurTick)
        {
            var weapon = holder.Comp.BlockingWeapon;
            holder.Comp.BlockedAttacker = null;
            holder.Comp.BlockingWeapon = null;
            if (weapon != null && TryComp<NullRodBlockComponent>(weapon, out var meleeBlock))
                Block(holder, (weapon.Value, meleeBlock), args);
            return;
        }

        // Оружие, которое отбивает любые атаки.
        foreach (var held in _hands.EnumerateHeld(holder.Owner))
        {
            if (!TryComp<NullRodBlockComponent>(held, out var block) || block.MeleeOnly || block.MechsOnly)
                continue;

            if (!RollBlock((held, block)))
                continue;

            Block(holder, (held, block), args);
            return;
        }
    }

    private bool RollBlock(Entity<NullRodBlockComponent> weapon)
    {
        if (weapon.Comp.RequiresToggle && !_toggle.IsActivated(weapon.Owner))
            return false;

        var chance = weapon.Comp.Chance;
        if (TryComp<WieldableComponent>(weapon, out var wieldable) && wieldable.Wielded)
            chance *= weapon.Comp.WieldedMultiplier;

        return _random.Prob(Math.Clamp(chance, 0f, 1f));
    }

    private void Block(Entity<NullRodBlockerComponent> holder, Entity<NullRodBlockComponent> weapon, DamageModifyEvent args)
    {
        args.Damage = new DamageSpecifier();
        _popup.PopupEntity(
            Loc.GetString("null-rod-block", ("user", holder.Owner), ("weapon", weapon.Owner)),
            holder,
            PopupType.MediumCaution);
        _audio.PlayPvs(weapon.Comp.BlockSound, holder);
    }

    private static bool IsHarmful(DamageSpecifier damage)
    {
        foreach (var value in damage.DamageDict.Values)
        {
            if (value > 0)
                return true;
        }

        return false;
    }
}
