using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Imperial.Antimagic;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Antimagic;

/// <summary>
/// Антимагия (can_block_magic / can_cast_magic из SS13). Защищают предметы с <see cref="ImperialAntimagicComponent"/>
/// в руках или на теле, а от нечестивой магии — ещё и святая вода в теле.
/// Заклинатель может сам запретить колдовство через <see cref="ImperialMagicCastAttemptEvent"/>.
/// </summary>
public sealed class ImperialAntimagicSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly SoundSpecifier BlockSound = new SoundPathSpecifier("/Audio/Effects/holy.ogg");

    /// <summary>Эффект блокировки показывается не чаще раза в 6 секунд (TRAIT_RECENTLY_BLOCKED_MAGIC).</summary>
    private static readonly TimeSpan BlockEffectCooldown = TimeSpan.FromSeconds(6);

    private readonly Dictionary<EntityUid, TimeSpan> _lastBlockEffect = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialMagicActionComponent, ActionAttemptEvent>(OnActionAttempt);
        SubscribeLocalEvent<ImperialMagicActionComponent, ActionValidateEvent>(OnActionValidate);
    }

    /// <summary>
    /// Защищён ли <paramref name="target"/> от магии вида <paramref name="magic"/>.
    /// </summary>
    /// <param name="effect">Показать сияние защиты, как при блоке в SS13.</param>
    public bool CanBlockMagic(EntityUid target, ImperialMagicResistance magic = ImperialMagicResistance.Magic, bool effect = true)
    {
        if (magic == ImperialMagicResistance.None)
            return false;

        var blocked = (magic & ImperialMagicResistance.Holy) != 0 && HasComp<ImperialHolyWaterComponent>(target)
            || HasAntimagicItem(target, magic);

        if (blocked && effect)
            ShowBlockEffect(target);

        return blocked;
    }

    /// <summary>
    /// Может ли <paramref name="caster"/> колдовать: антимагия в руках или на теле мешает колдовать самому.
    /// </summary>
    public bool CanCastMagic(EntityUid caster, ImperialMagicResistance magic = ImperialMagicResistance.Magic)
    {
        return !HasAntimagicItem(caster, magic);
    }

    private bool HasAntimagicItem(EntityUid uid, ImperialMagicResistance magic)
    {
        foreach (var item in _inventory.GetHandOrInventoryEntities(uid))
        {
            if (TryComp<ImperialAntimagicComponent>(item, out var antimagic) && (antimagic.Resistance & magic) != 0)
                return true;
        }

        return false;
    }

    private void ShowBlockEffect(EntityUid target)
    {
        var now = _timing.CurTime;
        if (_lastBlockEffect.TryGetValue(target, out var last) && now < last + BlockEffectCooldown)
            return;

        _lastBlockEffect[target] = now;
        _popup.PopupEntity(Loc.GetString("imperial-antimagic-block", ("target", target)), target, PopupType.Medium);
        _audio.PlayPvs(BlockSound, target);
    }

    private void OnActionAttempt(Entity<ImperialMagicActionComponent> action, ref ActionAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var user = args.User;

        // Сам антагонист может запретить колдовство (еретик со святой водой в теле).
        var castAttempt = new ImperialMagicCastAttemptEvent(user);
        RaiseLocalEvent(user, ref castAttempt);
        if (castAttempt.Cancelled)
        {
            args.Cancelled = true;
            return;
        }

        if (!CanCastMagic(user, action.Comp.Resistance))
        {
            args.Cancelled = true;
            _popup.PopupEntity(Loc.GetString("imperial-antimagic-cant-cast"), user, user, PopupType.MediumCaution);
        }
    }

    /// <summary>Заклинание с целью не действует на защищённую цель.</summary>
    private void OnActionValidate(Entity<ImperialMagicActionComponent> action, ref ActionValidateEvent args)
    {
        if (args.Invalid || GetEntity(args.Input.EntityTarget) is not { } target || target == args.User)
            return;

        if (!CanBlockMagic(target, action.Comp.Resistance))
            return;

        // Как в SS13: заклинание потрачено, но не подействовало.
        args.Invalid = true;
        if (TryComp<ActionComponent>(action, out var actionComp) && actionComp.UseDelay is { } delay)
            _actions.SetCooldown(action.Owner, delay);
        _popup.PopupEntity(Loc.GetString("imperial-antimagic-no-effect"), args.User, args.User);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_lastBlockEffect.Count > 64)
            _lastBlockEffect.Clear();
    }
}
