using System.Linq;
using Content.Server.Materials;
using Content.Server.Power.Components;
using Content.Shared.Construction;
using Content.Shared.Imperial.Fission;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Content.Shared.Wires;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Fission;

/// <summary>
/// Фабрикатор ядерных стержней (nuclear_rod_fabricator): чертежи берутся из прототипов стержней с craftable,
/// продвинутые открываются научным диском; материалы списываются сразу, стержень готов через 10 секунд.
/// </summary>
public sealed partial class FissionSystem
{
    [Dependency] private readonly MaterialStorageSystem _materials = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;

    private const int SheetUnits = 100;

    private void InitializeFabricator()
    {
        SubscribeLocalEvent<FissionFabricatorComponent, MapInitEvent>((uid, comp, _) => UpdateFabricatorVisuals((uid, comp)));
        SubscribeLocalEvent<FissionFabricatorComponent, InteractUsingEvent>(OnFabricatorInteractUsing);
        SubscribeLocalEvent<FissionFabricatorComponent, MachineDeconstructedEvent>(OnFabricatorDeconstructed);
        SubscribeLocalEvent<FissionFabricatorComponent, ActivatableUIOpenAttemptEvent>(OnFabricatorOpenAttempt);
        SubscribeLocalEvent<FissionFabricatorComponent, BoundUIOpenedEvent>((uid, comp, _) => UpdateFabricatorUi((uid, comp)));
        SubscribeLocalEvent<FissionFabricatorComponent, MaterialAmountChangedEvent>(OnFabricatorMaterials);
        SubscribeLocalEvent<FissionFabricatorComponent, PanelChangedEvent>((uid, comp, _) => UpdateFabricatorVisuals((uid, comp)));
        SubscribeLocalEvent<FissionFabricatorComponent, FissionFabricateMessage>(OnFabricate);
        SubscribeLocalEvent<FissionFabricatorComponent, FissionEjectMaterialMessage>(OnEjectMaterial);
    }

    private void OnFabricatorMaterials(Entity<FissionFabricatorComponent> ent, ref MaterialAmountChangedEvent args)
    {
        UpdateFabricatorUi(ent);
    }

    /// <summary>interact: с открытой панелью к фабрикатору не подойти.</summary>
    private void OnFabricatorOpenAttempt(Entity<FissionFabricatorComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!TryComp<WiresPanelComponent>(ent, out var panel) || !panel.Open)
            return;

        args.Cancel();
        if (!args.Silent)
            _popup.PopupEntity(Loc.GetString("fission-fabricator-panel-open-access"), ent, args.User);
    }

    private void OnFabricatorInteractUsing(Entity<FissionFabricatorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<FissionFabricatorUpgradeComponent>(args.Used))
            return;

        args.Handled = true;
        if (TryComp<WiresPanelComponent>(ent, out var panel) && panel.Open)
        {
            _popup.PopupEntity(Loc.GetString("fission-fabricator-panel-open"), ent, args.User);
            return;
        }

        ent.Comp.Upgraded = true;
        QueueDel(args.Used);
        _popup.PopupEntity(Loc.GetString("fission-fabricator-upgraded"), ent, args.User);
        UpdateFabricatorUi(ent);
    }

    private void OnFabricatorDeconstructed(Entity<FissionFabricatorComponent> ent, ref MachineDeconstructedEvent args)
    {
        if (ent.Comp.Upgraded)
            Spawn(ent.Comp.UpgradeDisk, Transform(ent).Coordinates);
    }

    /// <summary>create_designs: craftable-стержни, продвинутые — только с диском.</summary>
    private IEnumerable<(EntityPrototype Proto, FissionRodComponent Rod)> Designs(bool upgraded)
    {
        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || !proto.TryGetComponent<FissionRodComponent>(out var rod, _factory))
                continue;
            if (!rod.Craftable || rod.UpgradeRequired && !upgraded)
                continue;

            yield return (proto, rod);
        }
    }

    private string MaterialName(string id)
    {
        return _proto.TryIndex<MaterialPrototype>(id, out var material) ? Loc.GetString(material.Name) : id;
    }

    private void UpdateFabricatorUi(Entity<FissionFabricatorComponent> ent)
    {
        var state = new FissionFabricatorUiState();
        foreach (var (proto, rod) in Designs(ent.Comp.Upgraded))
        {
            var design = new FissionRodDesign
            {
                Id = proto.ID,
                Category = rod.Category,
                Name = proto.Name,
                Description = proto.Description,
                PowerAmount = rod.PowerAmount,
                PowerAmpMod = rod.PowerAmpMod,
                HeatAmount = rod.HeatAmount,
                HeatAmpMod = rod.HeatAmpMod,
                MaxDurability = rod.MaxDurability,
                HeatEnrichmentRequirement = rod.HeatEnrichThreshold,
                PowerEnrichmentRequirement = rod.PowerEnrichThreshold,
            };

            if (rod.HeatEnrichResult is { } heat && _proto.TryIndex(heat, out var heatProto))
                design.HeatEnrichment = heatProto.Name;
            if (rod.PowerEnrichResult is { } power && _proto.TryIndex(power, out var powerProto))
                design.PowerEnrichment = powerProto.Name;

            // «[count]x [name]» по требованиям к соседям.
            foreach (var group in rod.Requirements.GroupBy(r => r))
                design.NeighborRequirements.Add($"{group.Count()}x {RequirementName(group.Key)}");

            foreach (var (material, amount) in rod.Materials)
            {
                design.Materials.Add(new FissionMaterialEntry
                {
                    Id = material,
                    Name = MaterialName(material),
                    Amount = amount,
                    Sheets = (int) MathF.Round(amount / (float) SheetUnits, MidpointRounding.AwayFromZero),
                });
            }

            state.Designs.Add(design);
        }

        if (TryComp<MaterialStorageComponent>(ent, out var storage))
        {
            foreach (var (material, amount) in storage.Storage)
            {
                if (amount <= 0)
                    continue;

                state.Resources.Add(new FissionMaterialEntry
                {
                    Id = material,
                    Name = MaterialName(material),
                    Amount = amount,
                    Sheets = amount / SheetUnits,
                });
            }
        }

        _ui.SetUiState(ent.Owner, FissionFabricatorUiKey.Key, state);
    }

    /// <summary>ui_act fabricate_rod: проверка и списание материалов, затем begin_fabrication.</summary>
    private void OnFabricate(Entity<FissionFabricatorComponent> ent, ref FissionFabricateMessage args)
    {
        var user = args.Actor;
        if (!_proto.TryIndex<EntityPrototype>(args.Design, out var proto) ||
            !Designs(ent.Comp.Upgraded).Any(d => d.Proto.ID == proto.ID) ||
            !proto.TryGetComponent<FissionRodComponent>(out var rod, _factory))
        {
            return;
        }

        if (ent.Comp.Active)
        {
            _popup.PopupEntity(Loc.GetString("fission-fabricator-busy"), ent, user);
            return;
        }

        if (rod.Materials.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("fission-fabricator-no-materials-defined"), ent, user);
            return;
        }

        foreach (var (material, amount) in rod.Materials)
        {
            if (_materials.GetMaterialAmount(ent, material) >= amount)
                continue;

            _popup.PopupEntity(Loc.GetString("fission-fabricator-not-enough",
                ("amount", amount), ("material", MaterialName(material))), ent, user);
            return;
        }

        foreach (var (material, amount) in rod.Materials)
            _materials.TryChangeMaterialAmount(ent, material, -amount);

        ent.Comp.Active = true;
        ent.Comp.EndTime = _timing.CurTime + ent.Comp.WorkTime;
        ent.Comp.Schematic = proto.ID;
        SetFabricatorLoad(ent, true);
        UpdateFabricatorVisuals(ent);
        UpdateFabricatorUi(ent);
    }

    /// <summary>ui_act eject_material: листы в руки не выдаются — падают у машины.</summary>
    private void OnEjectMaterial(Entity<FissionFabricatorComponent> ent, ref FissionEjectMaterialMessage args)
    {
        var sheets = Math.Max(0, args.Sheets);
        if (sheets == 0 || _materials.GetMaterialAmount(ent, args.Material) < SheetUnits)
            return;

        _materials.EjectMaterial(ent, args.Material, sheets * SheetUnits);
        _popup.PopupEntity(Loc.GetString("fission-fabricator-ejected", ("sheets", sheets)), ent, args.Actor);
        UpdateFabricatorUi(ent);
    }

    private void SetFabricatorLoad(Entity<FissionFabricatorComponent> ent, bool active)
    {
        if (TryComp<ApcPowerReceiverComponent>(ent, out var receiver))
            receiver.Load = active ? ent.Comp.ActiveLoad : ent.Comp.IdleLoad;
    }

    private void UpdateFabricators(TimeSpan now)
    {
        var query = EntityQueryEnumerator<FissionFabricatorComponent>();
        while (query.MoveNext(out var uid, out var fabricator))
        {
            if (!fabricator.Active)
                continue;

            var ent = (uid, fabricator);
            if (!_power.IsPowered(uid))
            {
                // abort_fabrication: материалы уже потрачены.
                fabricator.Active = false;
                SetFabricatorLoad(ent, false);
                _audio.PlayPvs(fabricator.BuzzSound, uid);
                UpdateFabricatorVisuals(ent);
                continue;
            }

            if (now < fabricator.EndTime)
                continue;

            fabricator.Active = false;
            SetFabricatorLoad(ent, false);
            UpdateFabricatorVisuals(ent);
            if (fabricator.Schematic is not { } schematic)
                continue;

            var rod = Spawn(schematic, Transform(uid).Coordinates);
            _popup.PopupEntity(Loc.GetString("fission-fabricator-fabricated", ("machine", uid), ("rod", rod)), uid);
            _audio.PlayPvs(fabricator.PingSound, uid);
        }
    }

    private void UpdateFabricatorVisuals(Entity<FissionFabricatorComponent> ent)
    {
        string state;
        if (TryComp<WiresPanelComponent>(ent, out var panel) && panel.Open)
            state = "rod_fab_maint";
        else
            state = ent.Comp.Active ? "rod_fab_on" : "rod_fab";

        _appearance.SetData(ent, FissionMachineVisuals.State, state);
    }
}
