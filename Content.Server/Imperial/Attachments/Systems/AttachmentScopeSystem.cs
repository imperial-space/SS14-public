using Content.Server.Imperial.Attachments.Components;
using Content.Shared.Imperial.Attachments.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Movement.Components;
using Content.Server.Movement.Components;
namespace Content.Server.Imperial.Attachments.Systems
{
    public sealed class AttachmentScopeSystem : EntitySystem
    {
        //[Dependency] private readonly SharedContainerSystem _container = default!;
        //[Dependency] private readonly SharedAppearanceSystem _appearance = default!;

        private readonly HashSet<Entity<AttachmentScopeComponent>> _activeMufflers = new();
        [Dependency] private readonly SharedGunSystem _gunSystem = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<AttachmentScopeComponent, AttachmentInsertedEvent>(OnItemInserted);
            SubscribeLocalEvent<AttachmentScopeComponent, AttachmentRemovedEvent>(OnItemRemoved);
        }
        public override void Shutdown()
        {
            base.Shutdown();
            _activeMufflers.Clear();
        }
        private void OnItemInserted(EntityUid uid, AttachmentScopeComponent component, AttachmentInsertedEvent args)
        {
            if (!TryComp<AttachmentComponent>(uid, out var attachmentComponent))
                return;
            attachmentComponent.Weapon = args.Weapon;
            var eyeoff = EnsureComp<EyeCursorOffsetComponent>(args.Weapon);
            var wield = EnsureComp<CursorOffsetRequiresWieldComponent>(args.Weapon);
            eyeoff.MaxOffset = component.MaxOffset;
            eyeoff.PvsIncrease = component.PvsIncrease;
            eyeoff.OffsetSpeed = component.OffsetSpeed;
            _activeMufflers.Add((uid, component));
        }

        private void OnItemRemoved(EntityUid uid, AttachmentScopeComponent component, AttachmentRemovedEvent args)
        {
            if (!TryComp<AttachmentComponent>(uid, out var attachmentComponent))
                return;
            RemComp<EyeCursorOffsetComponent>(args.Weapon);
            RemComp<CursorOffsetRequiresWieldComponent>(args.Weapon);
            attachmentComponent.Weapon = null;
        }
    }
}
