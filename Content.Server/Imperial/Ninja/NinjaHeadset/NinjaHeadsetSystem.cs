using System.Diagnostics.CodeAnalysis;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Imperial.NinjaHeadset.Components;
using Content.Shared.Imperial.NinjaHeadset.Events;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Ninja.Components;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.NinjaHeadset.Systems;

public sealed class NinjaHeadsetSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private const string EarsSlot = "ears";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaHeadsetComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<NinjaHeadsetComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<NinjaHeadsetComponent, NinjaHackDoAfterEvent>(OnDoAfter);
    }

    private void OnExamined(Entity<NinjaHeadsetComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.CopiedFrequencies.Count == 0)
        {
            args.PushMarkup(Loc.GetString("ninja-headset-examine-empty"));
            return;
        }

        using (args.PushGroup(nameof(NinjaHeadsetComponent)))
        {
            args.PushMarkup(Loc.GetString("ninja-headset-examine-copied"));

            foreach (var frequency in ent.Comp.CopiedFrequencies)
            {
                var name = _prototype.TryIndex(frequency, out var channel) ? channel.LocalizedName : frequency.Id;
                args.PushMarkup(Loc.GetString("ninja-headset-examine-channel", ("channel", name)));
            }
        }
    }

    private void OnAfterInteract(Entity<NinjaHeadsetComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == args.User)
            return;

        if (!HasComp<SpaceNinjaComponent>(args.User) || !TryGetTargetHeadset(target, out _))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, ent.Comp.CopyFrequenciesTime, new NinjaHackDoAfterEvent(), ent, target: target, used: ent)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            Hidden = true,
        };

        args.Handled = _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnDoAfter(Entity<NinjaHeadsetComponent> ent, ref NinjaHackDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        if (!TryGetTargetHeadset(target, out var targetHolder) || !TryComp<EncryptionKeyHolderComponent>(ent, out var ninjaHolder))
            return;

        args.Handled = true;

        var copied = false;
        foreach (var channel in targetHolder.Channels)
        {
            if (ent.Comp.CopiedFrequencies.Contains(channel))
                continue;

            ent.Comp.CopiedFrequencies.Add(channel);
            ninjaHolder.Channels.Add(channel);
            copied = true;
        }

        if (copied)
        {
            Dirty(ent);
            RaiseLocalEvent(ent, new EncryptionChannelsChangedEvent(ninjaHolder));
        }

        var message = copied ? "ninja-headset-copy-success" : "ninja-headset-copy-none";
        _popup.PopupEntity(Loc.GetString(message), ent, args.User);
    }

    private bool TryGetTargetHeadset(EntityUid target, [NotNullWhen(true)] out EncryptionKeyHolderComponent? holder)
    {
        if (TryComp(target, out holder))
            return true;

        if (_inventory.TryGetSlotEntity(target, EarsSlot, out var ears) && TryComp(ears, out holder))
            return true;

        holder = null;
        return false;
    }
}
