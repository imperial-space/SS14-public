using Content.Shared.Forged;
using Content.Shared.Imperial.Medieval.Forged;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Content.Shared.Verbs;
using Content.Shared.Imperial.Medieval.MagicRunes.Components;
using Robust.Shared.Utility;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Content.Shared.Examine;

namespace Content.Server.Imperial.Medieval.Forged;

public sealed class ForgedAssemblySystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ForgedAssemblyComponent, ComponentInit>(OnCompInit);

        SubscribeLocalEvent<ForgedAssemblyComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ForgedAssemblyComponent, ForgedAssemblyDoAfterEvent>(OnDoAfter);

        SubscribeLocalEvent<ForgedAssemblyComponent, GetVerbsEvent<EquipmentVerb>>(OnGetVerbs);

        SubscribeLocalEvent<ForgedModuleComponent, ExaminedEvent>(OnExamined);
    }

    private void OnCompInit(EntityUid uid, ForgedAssemblyComponent component, ComponentInit args)
    {
        foreach (string layer in component.AvaibleSlots)
        {
            _containerSystem.EnsureContainer<ContainerSlot>(uid, layer);
            component.FittedModules.Add(layer, EntityUid.Invalid);
        }
    }

    private void OnInteractUsing(EntityUid uid, ForgedAssemblyComponent component, InteractUsingEvent args)
    {
        if (TryComp<MagicScrollComponent>(args.Used, out var scroll))
        {
            TransferToMob(uid, component, args);
            return;
        }
        if (!TryComp<ForgedModuleComponent>(args.Used, out var module)) return;

        string slotName = module.ModuleSlot;

        if (!_containerSystem.TryGetContainer(uid, slotName, out var container))
        {
            _popup.PopupEntity("Не подходит", uid, args.User);
            return;
        }

        if (container.Count > 0)
        {
            _popup.PopupEntity("Этот слот уже занят", uid, args.User);
            return;
        }

        if (module.RequiredModule != string.Empty && _containerSystem.TryGetContainer(uid, module.RequiredModule, out var reqContainer) && reqContainer?.Count == 0)
        {
            _popup.PopupEntity("Нужно сначала вставить " + module.RequiredModule, uid, args.User);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, 2.0f, new ForgedAssemblyDoAfterEvent { Inserting = true, SlotId = slotName }, uid, target: uid, used: args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };
        _doAfter.TryStartDoAfter(doAfterArgs);

        args.Handled = true;
    }

    private void OnGetVerbs(EntityUid uid, ForgedAssemblyComponent component, GetVerbsEvent<EquipmentVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var removeCategory = new VerbCategory("Извлечь модуль", null);

        foreach (var (slotId, moduleEntity) in component.FittedModules)
        {
            if (!moduleEntity.IsValid())
                continue;

            EquipmentVerb verb = new()
            {
                Text = $"Снять {Name(moduleEntity)}",
                Category = removeCategory,
                Act = () =>
                {
                    var doAfterArgs = new DoAfterArgs(EntityManager, args.User, 1.5f, new ForgedAssemblyDoAfterEvent { Inserting = false, SlotId = slotId }, uid, target: uid)
                    {
                        BreakOnMove = true,
                        BreakOnDamage = true,
                        NeedHand = true
                    };
                    _doAfter.TryStartDoAfter(doAfterArgs);
                }
            };
            args.Verbs.Add(verb);
        }
    }

    private void OnDoAfter(EntityUid uid, ForgedAssemblyComponent component, ForgedAssemblyDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled) return;

        if (args.Inserting)
        {
            if (args.Args.Used == null || !TryComp<ForgedModuleComponent>(args.Args.Used, out var module)) return;

            if (!_containerSystem.TryGetContainer(uid, module.ModuleSlot, out var container) || container.Count > 0) return;

            if (_containerSystem.Insert(args.Args.Used.Value, container))
            {
                _popup.PopupEntity($"Вы успешно закрепили {Name(args.Args.Used.Value)}", uid, args.User);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/buckle.ogg"), uid);
                FinalizeUpdate(uid, component);
            }
        }
        else
        {
            if (!_containerSystem.TryGetContainer(uid, args.SlotId, out var container) || container.Count == 0)
                return;

            var module = container.ContainedEntities[0];
            if (_containerSystem.TryRemoveFromContainer(module))
            {
                _popup.PopupEntity($"Вы извлекли {Name(module)}", uid, args.User);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/unbuckle.ogg"), uid);

                DropDependentModules(uid, args.SlotId, args.User);

                FinalizeUpdate(uid, component);
            }
        }

        args.Handled = true;
    }

    private void DropDependentModules(EntityUid uid, string parentSlotId, EntityUid user)
    {
        var dependencies = new List<string>();

        foreach (var container in _containerSystem.GetAllContainers(uid))
        {
            if (container.Count == 0) continue;

            var entity = container.ContainedEntities[0];
            if (TryComp<ForgedModuleComponent>(entity, out var module) && module.RequiredModule == parentSlotId)
            {
                dependencies.Add(container.ID);
            }
        }

        foreach (var slotId in dependencies)
        {
            if (_containerSystem.TryGetContainer(uid, slotId, out var container) && container.Count > 0)
            {
                var dependentEntity = container.ContainedEntities[0];
                if (_containerSystem.TryRemoveFromContainer(dependentEntity))
                {
                    _popup.PopupEntity($"Извлечено вместе с основой: {Name(dependentEntity)}", uid, user);

                    DropDependentModules(uid, slotId, user);
                }
            }
        }
    }

    private void FinalizeUpdate(EntityUid uid, ForgedAssemblyComponent component)
    {
        UpdateFittedModules(uid, component);
        if (TryComp<AppearanceComponent>(uid, out var appearance)) UpdateAppearance((uid, component, appearance));
    }

    private void UpdateFittedModules(EntityUid uid, ForgedAssemblyComponent component)
    {
        component.FittedModules.Clear();
        foreach (string layer in component.AvaibleSlots)
        {
            component.FittedModules.Add(layer, EntityUid.Invalid);
        }

        foreach (var container in _containerSystem.GetAllContainers(uid))
        {
            if (container.Count > 0)
            {
                component.FittedModules[container.ID] = container.ContainedEntities[0];
            }
        }
    }

    private void UpdateAppearance(Entity<ForgedAssemblyComponent?, AppearanceComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2, logMissing: false)) return;

        foreach (ForgedVisuals visualKey in Enum.GetValues(typeof(ForgedVisuals)))
        {
            string key = visualKey.ToString();
            if (key == "torso") continue;
            if (ent.Comp1.FittedModules.TryGetValue(key, out var moduleUid) && moduleUid.IsValid() && TryComp<ForgedModuleComponent>(moduleUid, out var module))
            {
                ForgedVisualsPacket packet = new ForgedVisualsPacket(module.LayerState, module.RsiPath);
                _appearanceSystem.SetData(ent, visualKey, packet, ent.Comp2);
            }
            else
            {
                ForgedVisualsPacket packet = new ForgedVisualsPacket("blank", new ResPath("Imperial/Medieval/Forged/wooden.rsi"));
                _appearanceSystem.SetData(ent, visualKey, packet, ent.Comp2);
            }
        }
    }

    private void TransferToMob(EntityUid uid, ForgedAssemblyComponent component, InteractUsingEvent args)
    {
        foreach (var slot in component.RequireSlots)
        {
            if (!component.FittedModules.TryGetValue(slot, out var moduleUid) || !EntityManager.EntityExists(moduleUid))
            {
                _popup.PopupEntity("Сборка не завершена!", uid, args.User);
                return;
            }
        }

        var xform = Transform(uid);
        var coordinates = xform.Coordinates;

        var mobUid = EntityManager.CreateEntityUninitialized("MedievalForgedMob", coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Medieval/scroll_use.ogg"), mobUid);

        if (TryComp<ForgedComponent>(mobUid, out var forgedComponent))
        {
            var newModulesDict = new Dictionary<string, EntityUid>();

            var torso = Spawn(component.TorsoID, coordinates);
            newModulesDict["torso"] = torso;

            foreach (var (slotId, moduleUid) in component.FittedModules)
            {
                if (slotId == "torso") continue;

                if (_containerSystem.TryGetContainer(uid, slotId, out var container))
                {
                    _containerSystem.Remove(moduleUid, container, force: true);
                    newModulesDict[slotId] = moduleUid;
                }
            }

            forgedComponent.FittedModules = newModulesDict;
            Dirty(mobUid, forgedComponent);
        }

        EntityManager.InitializeAndStartEntity(mobUid);

        args.Handled = true;
        QueueDel(args.Used);
        QueueDel(uid);
    }

    private void OnExamined(EntityUid uid, ForgedModuleComponent component, ExaminedEvent args)
    {
        // информация о сопротивлении
        var resBonus = component.ResistanceModifier * 100;
        args.PushMarkup(Loc.GetString("forged-module-examine-resistance", ("res", Math.Round(resBonus))));

        // информация о скорости
        var speedBonus = component.SpeedModifier * 100;
        args.PushMarkup(Loc.GetString("forged-module-examine-speed", ("speed", Math.Round(speedBonus))));
    }
}
