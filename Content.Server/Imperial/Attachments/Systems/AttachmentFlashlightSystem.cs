using Content.Shared.Light.Components;
using Content.Shared.Verbs;
//using Robust.Server.GameObjects;
using Robust.Shared.Containers;
//using Robust.Shared.GameObjects;
using Content.Server.Imperial.Attachments.Components;
using Robust.Server.GameObjects;
using Content.Shared.Imperial.Attachments.Components;
using Content.Server.Light.EntitySystems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Robust.Shared.Utility;
namespace Content.Server.Imperial.Attachments.Systems
{
    public sealed class AttachmentFlashlightSystem : EntitySystem
    {
        //[Dependency] private readonly SharedContainerSystem _container = default!;
        //[Dependency] private readonly SharedAppearanceSystem _appearance = default!;

        private readonly HashSet<Entity<AttachmentFlashlightComponent>> _activeLights = new();
        [Dependency] private readonly SharedPointLightSystem _lights = default!;
        [Dependency] private readonly HandheldLightSystem _handheldLight = default!;
        [Dependency] private readonly PowerCellSystem _powerCell = default!;
        [Dependency] private readonly SharedBatterySystem _battery = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<AttachmentFlashlightComponent, AttachmentInsertedEvent>(OnItemInserted);
            SubscribeLocalEvent<AttachmentFlashlightComponent, AttachmentRemovedEvent>(OnItemRemoved);
            SubscribeLocalEvent<AttachmentFlashlightComponent, AttachmentGetVerbs>(AddToggleVerb);
            SubscribeLocalEvent<AttachmentFlashlightComponent, AttachmentGetItemActions>(AddItemActions);
        }
        public override void Shutdown()
        {
            base.Shutdown();
            _activeLights.Clear();
        }
        public override void Update(float frameTime)
        {
            var toRemove = new RemQueue<Entity<AttachmentFlashlightComponent>>();
            foreach (var attachment in _activeLights)
            {
                if (!_lights.TryGetLight(attachment.Owner, out var light))
                {
                    continue;
                }
                if (attachment.Comp.Deleted)
                {
                    toRemove.Add(attachment);
                    continue;
                }

                TryUpdate(attachment, frameTime);
            }

            foreach (var light in toRemove)
            {
                _activeLights.Remove(light);
            }
        }
        private void AddItemActions(EntityUid uid, AttachmentFlashlightComponent component, AttachmentGetItemActions args)
        {
            //args.GetItemActions.AddAction(ref component.ToggleActionEntity, component.ToggleAction);
        }
        private void OnItemInserted(EntityUid uid, AttachmentFlashlightComponent component, AttachmentInsertedEvent args)
        {
            if (!TryComp<AttachmentComponent>(uid, out var attachmentComponent))
                return;
            attachmentComponent.Weapon = args.Weapon;
            var pointLightComponent = EnsureComp<PointLightComponent>(args.Weapon);
            var pointLightComponent2 = EnsureComp<PointLightComponent>(uid);
            component.LightOn = pointLightComponent2.Enabled;
            _lights.SetRadius(args.Weapon, component.Radius, pointLightComponent);
            _lights.SetEnabled(args.Weapon, component.LightOn, pointLightComponent);
            if (component.LightOn == true)
            {
                _activeLights.Add((uid, component));
            }
        }

        private void OnItemRemoved(EntityUid uid, AttachmentFlashlightComponent component, AttachmentRemovedEvent args)
        {
            if (!TryComp<AttachmentComponent>(uid, out var attachmentComponent))
                return;
            attachmentComponent.Weapon = null;
            _lights.SetEnabled(args.Weapon, false);
            _lights.SetEnabled(uid, component.LightOn);
        }

        private void AddToggleVerb(EntityUid uid, AttachmentFlashlightComponent component, AttachmentGetVerbs args)
        {
            Log.Error("Jesus christ");
            var verb = new ActivationVerb
            {
                Text = component.LightOn ? Loc.GetString("attachment-hammaggotson-flashlight-off") : Loc.GetString("attachment-hammaggotson-flashlight-on"),
                Act = () => ToggleFlashlight(uid, component, args.Weapon, args.GetVerbsEvent.User),
                Priority = 1
            };
            args.GetVerbsEvent.Verbs.Add(verb);
        }

        private void ToggleFlashlight(EntityUid uid, AttachmentFlashlightComponent component, EntityUid weapon, EntityUid user)
        {
            component.LightOn = !component.LightOn;
            if (!TryComp<AttachmentComponent>(uid, out var attachmentComp))
            {
                return;
            }
            if (attachmentComp.Weapon == null || attachmentComp.Weapon != weapon)
            {
                return;
            }
            if (!_lights.TryGetLight(weapon, out var pointLightComponent))
            {
                return;
            }
            _lights.SetEnabled(weapon, component.LightOn, pointLightComponent);
            if (component.LightOn == true)
            {
                _activeLights.Add((uid, component));
            }
            else
            {
                _activeLights.Remove((uid, component));
            }
            if (TryComp<HandheldLightComponent>(uid, out var handheldComp))
            {
                if (component.LightOn == true)
                {
                    _handheldLight.TurnOn(user, (uid, handheldComp));
                }
                else
                {
                    _handheldLight.TurnOff((uid, handheldComp));
                }
            }
        }

        public void TryUpdate(Entity<AttachmentFlashlightComponent> uid, float frameTime)
        {
            var component = uid.Comp;
            if (!TryComp<AttachmentComponent>(uid.Owner, out var attach))
            {
                return;
            }
            var weapon = attach.Weapon ?? EntityUid.Invalid;
            if (!_powerCell.TryGetBatteryFromSlotOrEntity(uid.Owner, out var battery) && uid.Comp.LightOn == true)
            {
                ToggleFlashlight(uid.Owner, uid.Comp, weapon, EntityUid.Invalid);
                return;
            }
            if (battery == null)
            {
                return;
            }
            var chargeFraction = _battery.GetChargeLevel(battery.Value.AsNullable());
            if (!TryComp<HandheldLightComponent>(uid.Owner, out var handheld))
            {
                return;
            }
            if (component.LightOn == true && _battery.GetCharge(battery.Value.AsNullable()) < handheld.Wattage * frameTime)
            {
                ToggleFlashlight(uid.Owner, uid.Comp, weapon, EntityUid.Invalid);
            }
        }
    }
}
