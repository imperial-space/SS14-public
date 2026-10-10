using Content.Shared.Containers.ItemSlots;
using Content.Shared.Light.Components;
using Content.Shared.Verbs;
//using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Content.Shared.Imperial.Attachments.Components;
//using Robust.Shared.GameObjects;
//using Content.Server.Imperial.Attachments.Components;
using Content.Shared.Actions;
namespace Content.Shared.Imperial.Attachments.Systems
{
    public abstract class SharedWeaponAttachmentsSystem : EntitySystem
    {
        //[Dependency] private readonly SharedContainerSystem _container = default!;
        //[Dependency] private readonly SharedAppearanceSystem _appearance = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<WeaponAttachmentsComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
            SubscribeLocalEvent<WeaponAttachmentsComponent, EntRemovedFromContainerMessage>(OnItemRemoved);
            SubscribeLocalEvent<WeaponAttachmentsComponent, GetVerbsEvent<ActivationVerb>>(AddToggleVerb);
            SubscribeLocalEvent<WeaponAttachmentsComponent, MapInitEvent>(OnMapInit);
            SubscribeLocalEvent<WeaponAttachmentsComponent, GetItemActionsEvent>(OnGetActions);
        }

        private void OnMapInit(EntityUid uid, WeaponAttachmentsComponent component, MapInitEvent args)
        {
            if (!TryComp<ItemSlotsComponent>(uid, out var itemSlots))
                return;

            foreach (var slot in itemSlots.Slots.Values)
            {
                if (slot.ContainerSlot?.ContainedEntity is not { } attachment)
                    continue;
                if (!TryComp<AttachmentComponent>(attachment, out var attachmentComp))
                    continue;
                if (slot.ContainerSlot.ID != attachmentComp.SlotId)
                    continue;
                if (component.Attachments.Contains(attachment))
                    continue;

                component.Attachments.Add(attachment);
                var evvent = new AttachmentInsertedEvent(attachment, uid);
                RaiseLocalEvent(attachment, evvent);
            }
        }
        private void OnGetActions(EntityUid uid, WeaponAttachmentsComponent component, GetItemActionsEvent args)
        {
            for (int i = 0; i < component.Attachments.Count; i++)
            {
                if (!TryComp<AttachmentComponent>(component.Attachments[i], out var attachmentComp))
                    continue;
                var evvent = new AttachmentGetItemActions(uid, args);
                RaiseLocalEvent(component.Attachments[i], evvent);
            }
        }
        private void OnItemInserted(EntityUid uid, WeaponAttachmentsComponent component, EntInsertedIntoContainerMessage args)
        {
            if (!TryComp<AttachmentComponent>(args.Entity, out var attachmentComp))
                return;
            if (args.Container.ID != attachmentComp.SlotId)
                return;
            if (!component.Attachments.Contains(args.Entity))
                component.Attachments.Add(args.Entity);
            var evvent = new AttachmentInsertedEvent(args.Entity, uid);
            RaiseLocalEvent(args.Entity, evvent);
        }

        private void OnItemRemoved(EntityUid uid, WeaponAttachmentsComponent component, EntRemovedFromContainerMessage args)
        {
            if (!TryComp<AttachmentComponent>(args.Entity, out var attachmentComp))
                return;
            if (args.Container.ID != attachmentComp.SlotId)
                return;
            component.Attachments.Remove(args.Entity);
            var evvent = new AttachmentRemovedEvent(args.Entity, uid);
            RaiseLocalEvent(args.Entity, evvent);
        }

        private void AddToggleVerb(EntityUid uid, WeaponAttachmentsComponent component, GetVerbsEvent<ActivationVerb> args)
        {
            if (!args.CanAccess || !args.CanInteract)
                return;
            for (int i = 0; i < component.Attachments.Count; i++)
            {
                if (!TryComp<AttachmentComponent>(component.Attachments[i], out var attachmentComp))
                    continue;
                var evvent = new AttachmentGetVerbs(uid, args);
                RaiseLocalEvent(component.Attachments[i], evvent);
            }
        }
    }
}

public sealed class AttachmentInsertedEvent : HandledEntityEventArgs
{
    public readonly EntityUid Attachment;
    public readonly EntityUid Weapon;
    public AttachmentInsertedEvent(EntityUid attachment, EntityUid weapon)
    {
        Attachment = attachment;
        Weapon = weapon;
    }
}

public sealed class AttachmentRemovedEvent : HandledEntityEventArgs
{
    public readonly EntityUid Attachment;
    public readonly EntityUid Weapon;

    public AttachmentRemovedEvent(EntityUid attachment, EntityUid weapon)
    {
        Attachment = attachment;
        Weapon = weapon;
    }
}


public sealed class AttachmentGetVerbs : HandledEntityEventArgs
{
    public readonly EntityUid Weapon;
    public readonly GetVerbsEvent<ActivationVerb> GetVerbsEvent;
    public AttachmentGetVerbs(EntityUid weapon, GetVerbsEvent<ActivationVerb> getVerbsEvent)
    {
        Weapon = weapon;
        GetVerbsEvent = getVerbsEvent;
    }
}

public sealed class AttachmentGetItemActions : HandledEntityEventArgs
{
    public readonly EntityUid Weapon;
    public readonly GetItemActionsEvent GetItemActions;
    public AttachmentGetItemActions(EntityUid weapon, GetItemActionsEvent getItemActionsEvent)
    {
        Weapon = weapon;
        GetItemActions = getItemActionsEvent;
    }
}
