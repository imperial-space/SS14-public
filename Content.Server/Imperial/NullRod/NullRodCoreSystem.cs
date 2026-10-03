using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Imperial.Antimagic;
using Content.Shared.Imperial.NullRod.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Whitelist;

namespace Content.Server.Imperial.NullRod;

/// <summary>
/// Общие свойства святого оружия (nullrod_core из SS13): стирание рун и дополнительный урон по духам.
/// </summary>
public sealed class NullRodCoreSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NullRodCoreComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<NullRodCoreComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnAfterInteract(Entity<NullRodCoreComponent> rod, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (TryComp<CultRuneComponent>(target, out var cultRune))
        {
            // Как CultRuneSystem.DestroyRune: эффект разрушения, затем удаление.
            if (cultRune.DestructionState != null)
                Spawn("CultRuneDestructionEffect", Transform(target).Coordinates);
        }
        else if (!HasComp<ImperialNullRodErasableComponent>(target))
        {
            return;
        }

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("null-rod-rune-removed", ("rune", target), ("weapon", rod.Owner)), args.User, args.User);
        _chat.TrySendInGameICMessage(args.User, Loc.GetString(rod.Comp.RuneRemoveLine), InGameICChatType.Speak, false);
        QueueDel(target);
    }

    private void OnMeleeHit(Entity<NullRodCoreComponent> rod, ref MeleeHitEvent args)
    {
        if (!args.IsHit || rod.Comp.Banes.Count == 0)
            return;

        foreach (var target in args.HitEntities)
        {
            foreach (var bane in rod.Comp.Banes)
            {
                if (_whitelist.IsWhitelistPass(bane.Whitelist, target))
                    _damageable.TryChangeDamage(target, bane.Damage, origin: args.User);
            }
        }
    }
}
