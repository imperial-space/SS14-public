using Content.Server.Imperial.Attachments.Components;
using Content.Shared.Imperial.Attachments.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Components;
namespace Content.Server.Imperial.Attachments.Systems
{
    public sealed class AttachmentMufflerSystem : EntitySystem
    {
        //[Dependency] private readonly SharedContainerSystem _container = default!;
        //[Dependency] private readonly SharedAppearanceSystem _appearance = default!;

        private readonly HashSet<Entity<AttachmentMufflerComponent>> _activeMufflers = new();
        [Dependency] private readonly SharedGunSystem _gunSystem = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<AttachmentMufflerComponent, AttachmentInsertedEvent>(OnItemInserted);
            SubscribeLocalEvent<AttachmentMufflerComponent, AttachmentRemovedEvent>(OnItemRemoved);
        }
        public override void Shutdown()
        {
            base.Shutdown();
            _activeMufflers.Clear();
        }
        private void OnItemInserted(EntityUid uid, AttachmentMufflerComponent component, AttachmentInsertedEvent args)
        {
            if (!TryComp<AttachmentComponent>(uid, out var attachmentComponent))
                return;
            if (!TryComp<GunComponent>(args.Weapon, out var gun))
                return;
            attachmentComponent.Weapon = args.Weapon;

            component.SoundGunshotOriginal = gun.SoundGunshot;
            _gunSystem.SetGunsound((args.Weapon, gun), component.SoundGunshot);

            _activeMufflers.Add((uid, component));
        }

        private void OnItemRemoved(EntityUid uid, AttachmentMufflerComponent component, AttachmentRemovedEvent args)
        {
            if (!TryComp<AttachmentComponent>(uid, out var attachmentComponent))
                return;
            if (!TryComp<GunComponent>(args.Weapon, out var gun))
                return;
            var ogsound = component.SoundGunshotOriginal ?? component.SoundGunshot;
            _gunSystem.SetGunsound((args.Weapon, gun), ogsound);
            component.SoundGunshotOriginal = null;
            attachmentComponent.Weapon = null;
        }
    }
}
