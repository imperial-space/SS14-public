using System.Linq;
using Content.Server.Imperial.MachineParts;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Construction.NodeEntities;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Construction.Steps;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Imperial.MachineParts;
using Content.Shared.Imperial.Manufacturing;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Kitchen;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Research.Prototypes;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Manufacturing;

public sealed partial class ImperialManufacturingSystem
{
    [Dependency] private readonly SharedEntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly Content.Shared.Tools.Systems.WeldableSystem _weldable = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    private static readonly EntProtoId OreBox = "OreBox";
    private static readonly EntProtoId Autolathe = "Autolathe";
    private static readonly EntProtoId Steel = "SheetSteel1";
    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> Heat = "Heat";
    private static readonly SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    /// <summary>Единиц материала в одном листе (SHEET_MATERIAL_AMOUNT).</summary>
    private const int SheetAmount = 100;

    /// <summary>Сырой материал руды → лист после переплавки (refined_type).</summary>
    private readonly Dictionary<string, EntProtoId> _refined = new();

    private static readonly TimeSpan FastProcess = TimeSpan.FromSeconds(0.2);
    private static readonly TimeSpan SlowProcess = TimeSpan.FromSeconds(1);

    private void InitializeMachines()
    {
        SubscribeLocalEvent<ImperialManufacturingComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ImperialManufacturingComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<ImperialManufacturingComponent, GetVerbsEvent<AlternativeVerb>>(OnAltVerbs);
        SubscribeLocalEvent<ImperialManufacturingComponent, ImperialMachinePartsRefreshEvent>(OnPartsRefresh);
        SubscribeLocalEvent<ImperialManufacturingComponent, ImperialStorageBoxDisassembleDoAfterEvent>(OnBoxDisassemble);
        SubscribeLocalEvent<ImperialManufacturingComponent, ImperialStorageBoxDumpDoAfterEvent>(OnBoxDump);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        BuildRefined();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<LatheRecipePrototype>())
            BuildRefined();
    }

    /// <summary>Переплавка руды: рецепты листов из одного сырого материала (Recipes/Lathes/sheet.yml).</summary>
    private void BuildRefined()
    {
        _refined.Clear();
        // Добавки вроде угля в сталь плавильне TG не нужны: учитывается только единственное сырьё.
        foreach (var recipe in _proto.EnumeratePrototypes<LatheRecipePrototype>().OrderBy(r => r.Materials.Count).ThenBy(r => r.ID))
        {
            var raws = recipe.Materials.Where(m => m.Key.Id.StartsWith("Raw")).ToList();
            if (recipe.Result is not { } result || raws.Count != 1)
                continue;

            var (material, amount) = raws[0];
            if (amount != SheetAmount || _refined.ContainsKey(material.Id))
                continue;

            _refined[material.Id] = result;
        }
    }

    private void OnPartsRefresh(Entity<ImperialManufacturingComponent> ent, ref ImperialMachinePartsRefreshEvent args)
    {
        // lathe/RefreshParts(): max_amount = bin.tier * SHEET_MATERIAL_AMOUNT * MAX_STACK_SIZE
        if (ent.Comp.Kind == ImperialManufacturingKind.Lathe)
            ent.Comp.MaxMaterials = args.Ratings.Max(ImperialStockPartType.MatterBin) * SheetAmount * 50;
    }

    #region Приём

    private bool FromBack(Entity<ImperialManufacturingComponent> ent, Direction receiveDir)
    {
        return receiveDir == Dir(ent).GetOpposite();
    }

    private bool ReceiveUnloader(Entity<ImperialManufacturingComponent> ent, EntityUid item, Direction receiveDir)
    {
        if (!Powered(ent) || !FromBack(ent, receiveDir) || Contents(ent).ContainedEntities.Count > 0)
            return false;

        if (TryComp<EntityStorageComponent>(item, out var storage))
        {
            // can_open(): у машины нет рук, поэтому без проверки HandsComponent.
            if (storage.Open || _weldable.IsWelded(item))
                return false;
            var attempt = new StorageOpenAttemptEvent(ent, true);
            RaiseLocalEvent(item, ref attempt, true);
            if (attempt.Cancelled)
                return false;
        }
        else if (!IsOreBox(item))
            return false;

        if (!Store(ent, item))
            return false;

        ent.Comp.NextProcess = _timing.CurTime;
        return true;
    }

    private bool IsOreBox(EntityUid uid)
    {
        return MetaData(uid).EntityPrototype is { } proto
               && (proto.ID == OreBox || _proto.EnumerateParents<EntityPrototype>(proto.ID).Any(p => p.ID == OreBox));
    }

    private bool ReceiveCrusher(Entity<ImperialManufacturingComponent> ent, EntityUid item, Direction receiveDir)
    {
        if (_tag.HasTag(item, OreTag) || !HasComp<ItemComponent>(item) || !Powered(ent) || !FromBack(ent, receiveDir))
            return false;

        if (Contents(ent).ContainedEntities.Count >= ent.Comp.Capacity)
            return MergeIntoContents(ent, item);

        return Store(ent, item);
    }

    private bool ReceiveSmelter(Entity<ImperialManufacturingComponent> ent, EntityUid item, Direction receiveDir)
    {
        if (!HasComp<ItemComponent>(item) || !Powered(ent) || !FromBack(ent, receiveDir))
            return false;

        if (Contents(ent).ContainedEntities.Count >= 5)
            return MergeIntoContents(ent, item);

        return Store(ent, item);
    }

    private bool ReceiveLathe(Entity<ImperialManufacturingComponent> ent, EntityUid item, Direction receiveDir)
    {
        if (!TryComp<StackComponent>(item, out var stack) || _tag.HasTag(item, OreTag) || !FromBack(ent, receiveDir))
            return false;
        if (!TryComp<PhysicalCompositionComponent>(item, out var composition) || composition.MaterialComposition.Count == 0)
            return false;

        // material_container/insert_item(): берётся столько листов, сколько влезет.
        var perSheet = composition.MaterialComposition.Values.Sum();
        var free = ent.Comp.MaxMaterials - ent.Comp.Materials.Values.Sum();
        var sheets = Math.Min(stack.Count, perSheet > 0 ? free / perSheet : 0);
        if (sheets <= 0)
            return false;

        foreach (var (material, amount) in composition.MaterialComposition)
        {
            ent.Comp.Materials[material] = ent.Comp.Materials.GetValueOrDefault(material) + amount * sheets;
        }

        if (sheets >= stack.Count)
            QueueDel(item);
        else
            _stack.ReduceCount((item, stack), sheets);

        return true;
    }

    private bool ReceiveStorage(Entity<ImperialManufacturingComponent> ent, EntityUid item)
    {
        if (TryComp<EntityStorageComponent>(item, out var storage) && storage.Contents.ContainedEntities.Count > 0)
            return false;

        if (Contents(ent).ContainedEntities.Count >= ent.Comp.MaxStuff)
            return MergeIntoContents(ent, item);

        return Store(ent, item);
    }

    private bool ReceiveRouter(Entity<ImperialManufacturingComponent> ent, EntityUid item, EntityUid? from, Direction receiveDir)
    {
        if (from is { } source && TryComp<ImperialManufacturingComponent>(source, out var sourceComp) && sourceComp.Kind == ImperialManufacturingKind.Router)
            return false;

        var filtered = ent.Comp.Directions.Where(d => d != receiveDir && !ent.Comp.DisabledDirs.Contains(d)).ToList();
        if (filtered.Count == 0)
            ent.Comp.Directions = new List<Direction> { Direction.North, Direction.South, Direction.East, Direction.West };

        foreach (var target in filtered)
        {
            ent.Comp.Directions.Remove(target);

            // handle_stack(): стопки делятся по одному.
            var sending = item;
            if (TryComp<StackComponent>(item, out var stack) && stack.Count > 1
                && _stack.Split((item, stack), 1, Transform(item).Coordinates) is { } split)
                sending = split;

            if (Send(ent, sending, target))
            {
                ent.Comp.InDir = receiveDir;
                UpdateVisuals(ent);
                return true;
            }

            if (sending != item)
                _stack.TryMergeStacks(sending, item, out _);
        }

        return false;
    }

    #endregion

    #region Работа

    private void UpdateMachines()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialManufacturingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = (uid, comp);
            switch (comp.Kind)
            {
                case ImperialManufacturingKind.Lathe:
                    if (comp.BusyUntil is { } until && now >= until)
                        FinishLathe(ent);
                    break;
                case ImperialManufacturingKind.Crafter:
                    if (comp.CraftUntil is { } craft && now >= craft)
                        FinishCraft(ent);
                    break;
            }

            if (now < comp.NextProcess)
                continue;

            var fast = comp.Kind is ImperialManufacturingKind.Unloader or ImperialManufacturingKind.Sorter;
            comp.NextProcess = now + (fast ? FastProcess : SlowProcess);

            if (!Transform(uid).Anchored && comp.Kind == ImperialManufacturingKind.Sorter)
                continue;

            switch (comp.Kind)
            {
                case ImperialManufacturingKind.Unloader:
                    ProcessUnloader(ent);
                    break;
                case ImperialManufacturingKind.Crusher:
                    ProcessCrusher(ent);
                    break;
                case ImperialManufacturingKind.Smelter:
                    ProcessSmelter(ent);
                    break;
                case ImperialManufacturingKind.Lathe:
                    ProcessLathe(ent);
                    break;
                case ImperialManufacturingKind.Crafter:
                    ProcessCrafter(ent);
                    break;
                case ImperialManufacturingKind.Sorter:
                    ProcessSorter(ent);
                    break;
            }
        }
    }

    /// <summary>Выдать всё удержанное; false — что-то осталось.</summary>
    private bool SendWithheld(Entity<ImperialManufacturingComponent> ent)
    {
        foreach (var item in Output(ent).ContainedEntities.ToList())
        {
            if (!Send(ent, item, Dir(ent)))
                return false;
        }

        return true;
    }

    /// <summary>Положить результат в удержание и сразу попытаться выдать.</summary>
    private void Emit(Entity<ImperialManufacturingComponent> ent, EntityUid item)
    {
        _container.Insert(item, Output(ent));
        SendWithheld(ent);
    }

    private void ProcessUnloader(Entity<ImperialManufacturingComponent> ent)
    {
        var contents = Contents(ent).ContainedEntities;
        if (contents.Count == 0)
        {
            Idle(ent);
            return;
        }

        if (!Powered(ent))
            return;

        AddLoad(ent, ent.Comp.PowerCost);
        var box = contents[0];
        var dir = Dir(ent);
        var side = Turn(dir, ent.Comp.FlipSide ? 90 : -90);

        BaseContainer? inner = null;
        if (TryComp<EntityStorageComponent>(box, out var storage))
            inner = storage.Contents;
        else if (TryComp<StorageComponent>(box, out var oreStorage))
            inner = oreStorage.Container;

        if (inner != null)
        {
            foreach (var thing in inner.ContainedEntities.ToList())
            {
                if (HasComp<MobStateComponent>(thing))
                    continue;
                Send(ent, thing, dir);
            }

            if (inner.ContainedEntities.Count > 0)
                return;
        }

        if (!Send(ent, box, side))
            return;

        if (storage != null)
            _entityStorage.OpenStorage(box, storage);
        Idle(ent);
    }

    private void ProcessCrusher(Entity<ImperialManufacturingComponent> ent)
    {
        if (!SendWithheld(ent))
            return;

        foreach (var (material, amount) in ent.Comp.HeldMats.ToList())
        {
            if (amount < 1)
                continue;

            var whole = (int) MathF.Floor(amount);
            ent.Comp.HeldMats[material] -= whole;
            if (ent.Comp.HeldMats[material] <= 0)
                ent.Comp.HeldMats.Remove(material);

            foreach (var sheet in SpawnMaterial(ent, material, whole))
            {
                _container.Insert(sheet, Output(ent));
            }

            return;
        }

        var victims = Contents(ent).ContainedEntities;
        if (victims.Count == 0)
        {
            Idle(ent);
            return;
        }

        if (!Powered(ent))
            return;

        var victim = victims[^1];
        AddLoad(ent, ent.Comp.PowerCost);

        if (HasComp<MobStateComponent>(victim))
        {
            _damageable.TryChangeDamage(victim, new DamageSpecifier(_proto.Index(Blunt), 95), true);
            Emit(ent, victim);
            return;
        }

        var materials = new Dictionary<string, int>();
        CollectMaterials(victim, materials);
        QueueDel(victim);

        foreach (var (material, quantity) in materials)
        {
            // Меньше листа копится в held_mats, остаток листа тоже не пропадает.
            var sheets = quantity / SheetAmount;
            var rest = quantity % SheetAmount;
            if (rest > 0)
                ent.Comp.HeldMats[material] = ent.Comp.HeldMats.GetValueOrDefault(material) + rest / (float) SheetAmount;

            if (sheets <= 0)
                continue;

            foreach (var sheet in SpawnMaterial(ent, material, sheets))
            {
                _container.Insert(sheet, Output(ent));
            }
        }

        SendWithheld(ent);
    }

    /// <summary>custom_materials предмета и всего, что в нём лежит.</summary>
    private void CollectMaterials(EntityUid uid, Dictionary<string, int> materials)
    {
        if (TryComp<PhysicalCompositionComponent>(uid, out var composition))
        {
            var count = TryComp<StackComponent>(uid, out var stack) ? stack.Count : 1;
            foreach (var (material, amount) in composition.MaterialComposition)
            {
                materials[material] = materials.GetValueOrDefault(material) + amount * count;
            }
        }

        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            return;

        foreach (var container in _container.GetAllContainers(uid, manager))
        {
            foreach (var inner in container.ContainedEntities)
            {
                CollectMaterials(inner, materials);
            }
        }
    }

    private List<EntityUid> SpawnMaterial(EntityUid machine, string material, int sheets)
    {
        if (!_proto.TryIndex<MaterialPrototype>(material, out var proto) || proto.StackEntity is not { } stackEntity)
            return new List<EntityUid>();

        return _stack.SpawnMultipleAtPosition(stackEntity, sheets, Transform(machine).Coordinates);
    }

    private void ProcessSmelter(Entity<ImperialManufacturingComponent> ent)
    {
        var contents = Contents(ent).ContainedEntities;
        if (contents.Count == 0 && Output(ent).ContainedEntities.Count == 0)
        {
            SetState(ent, "smelter");
            Idle(ent);
            return;
        }

        if (Output(ent).ContainedEntities.Count == 0 && contents.Count > 0 && Powered(ent))
        {
            SetState(ent, "smelter_on");
            AddLoad(ent, ent.Comp.PowerCost);

            var ore = contents[^1];
            if (Refined(ore) is { } refined && TryComp<StackComponent>(ore, out var stack))
            {
                var amount = Math.Min(5, stack.Count);
                foreach (var sheet in _stack.SpawnMultipleAtPosition(refined, amount, Transform(ent).Coordinates))
                {
                    _container.Insert(sheet, Output(ent));
                }

                if (amount >= stack.Count)
                    QueueDel(ore);
                else
                    _stack.ReduceCount((ore, stack), amount);
            }
            else
            {
                // fire_act(1400)
                _damageable.TryChangeDamage(ore, new DamageSpecifier(_proto.Index(Heat), 50));
                if (!TerminatingOrDeleted(ore))
                    _container.Insert(ore, Output(ent));
            }
        }
        else if (!Powered(ent))
        {
            SetState(ent, "smelter");
        }

        SendWithheld(ent);
    }

    /// <summary>stack/ore/refined_type.</summary>
    private EntProtoId? Refined(EntityUid ore)
    {
        if (!_tag.HasTag(ore, OreTag) || !TryComp<PhysicalCompositionComponent>(ore, out var composition))
            return null;

        foreach (var material in composition.MaterialComposition.Keys)
        {
            if (_refined.TryGetValue(material, out var result))
                return result;
        }

        return null;
    }

    private void SetState(EntityUid uid, string state)
    {
        _appearance.SetData(uid, ImperialManufacturingVisuals.State, state);
    }

    #region Лат

    /// <summary>Чертежи автолата (techweb/autounlocking/autolathe).</summary>
    public List<LatheRecipePrototype> LatheDesigns()
    {
        var result = new List<LatheRecipePrototype>();
        if (!_proto.TryIndex(Autolathe, out var autolathe)
            || !autolathe.TryGetComponent<LatheComponent>(out var lathe, _factory))
            return result;

        foreach (var pack in lathe.StaticPacks)
        {
            if (!_proto.TryIndex(pack, out var packProto))
                continue;

            foreach (var id in packProto.Recipes)
            {
                if (_proto.TryIndex(id, out var recipe) && recipe.Result != null && !result.Contains(recipe))
                    result.Add(recipe);
            }
        }

        return result;
    }

    public string DesignName(LatheRecipePrototype recipe)
    {
        if (recipe.Name is { } name)
            return Loc.GetString(name);

        return recipe.Result is { } result && _proto.TryIndex(result, out var proto) ? proto.Name : recipe.ID;
    }

    private void ProcessLathe(Entity<ImperialManufacturingComponent> ent)
    {
        if (!SendWithheld(ent))
            return;

        if (ent.Comp.DesignId is not { } designId || !_proto.TryIndex<LatheRecipePrototype>(designId, out var design))
            return;

        if (!Powered(ent))
        {
            ent.Comp.BusyUntil = null;
            Idle(ent);
            return;
        }

        if (ent.Comp.BusyUntil != null || !HasMaterials(ent, design))
            return;

        // craft_time = (construction_time * lathe_time_factor) ** 0.8, в децисекундах.
        var craftTime = TimeSpan.FromSeconds(Math.Pow(design.CompleteTime.TotalSeconds * 10, 0.8) / 10);
        ent.Comp.BusyUntil = _timing.CurTime + craftTime;
        Flick(ent, "lathe_printing", craftTime);
        _audio.PlayPvs(PrintSound, ent);
        AddLoad(ent, ent.Comp.PowerCost);
    }

    private bool HasMaterials(Entity<ImperialManufacturingComponent> ent, LatheRecipePrototype design)
    {
        return design.Materials.All(m => ent.Comp.Materials.GetValueOrDefault(m.Key.Id) >= m.Value);
    }

    /// <summary>do_make_item().</summary>
    private void FinishLathe(Entity<ImperialManufacturingComponent> ent)
    {
        ent.Comp.BusyUntil = null;
        Idle(ent);
        if (!Powered(ent) || ent.Comp.DesignId is not { } designId || !_proto.TryIndex<LatheRecipePrototype>(designId, out var design))
            return;
        if (!HasMaterials(ent, design) || design.Result is not { } result)
            return;

        foreach (var (material, amount) in design.Materials)
        {
            ent.Comp.Materials[material.Id] -= amount;
            if (ent.Comp.Materials[material.Id] <= 0)
                ent.Comp.Materials.Remove(material.Id);
        }

        var created = Spawn(result, Transform(ent).Coordinates);
        Emit(ent, created);
    }

    /// <summary>click_ctrl_shift(): materials.retrieve_all().</summary>
    private void DumpMaterials(Entity<ImperialManufacturingComponent> ent)
    {
        foreach (var (material, amount) in ent.Comp.Materials.ToList())
        {
            var sheets = amount / SheetAmount;
            if (sheets <= 0)
                continue;

            if (SpawnMaterial(ent, material, sheets).Count == 0)
                continue;

            ent.Comp.Materials[material] -= sheets * SheetAmount;
            if (ent.Comp.Materials[material] <= 0)
                ent.Comp.Materials.Remove(material);
        }

        _popup.PopupEntity(Loc.GetString("imperial-manufacturing-lathe-dumped"), ent);
    }

    #endregion

    #region Сборщик

    private sealed record CraftRecipe(string Id, string Name, EntProtoId Result, List<ConstructionGraphStep> Steps, TimeSpan Time);

    private List<CraftRecipe>? _craftRecipes;

    /// <summary>GLOB.crafting_recipes: предметные рецепты конструирования, которые можно собрать без инструментов.</summary>
    private List<CraftRecipe> CraftRecipes()
    {
        if (_craftRecipes != null)
            return _craftRecipes;

        _craftRecipes = new List<CraftRecipe>();
        foreach (var construction in _proto.EnumeratePrototypes<ConstructionPrototype>())
        {
            if (construction.Type != ConstructionType.Item || !_proto.TryIndex(construction.Graph, out var graph))
                continue;
            if (graph.Path(construction.StartNode, construction.TargetNode) is not { } path || path.Length == 0)
                continue;
            if (path[^1].Entity is not StaticNodeEntity { Id: { } resultId } || !_proto.TryIndex<EntityPrototype>(resultId, out var resultProto))
                continue;

            var steps = new List<ConstructionGraphStep>();
            var valid = true;
            var node = graph.Nodes[construction.StartNode];
            foreach (var next in path)
            {
                if (node.GetEdge(next.Name) is not { } edge || edge.Steps.Count == 0)
                {
                    valid = false;
                    break;
                }

                foreach (var step in edge.Steps)
                {
                    if (step is not (MaterialConstructionGraphStep or ArbitraryInsertConstructionGraphStep))
                        valid = false;
                    steps.Add(step);
                }

                node = next;
            }

            if (!valid)
                continue;

            var doAfter = steps.Sum(s => s.DoAfter);
            var name = construction.SetName is { } setName ? Loc.GetString(setName) : resultProto.Name;
            _craftRecipes.Add(new CraftRecipe(construction.ID, name, resultId, steps, TimeSpan.FromSeconds(doAfter > 0 ? doAfter : 3)));
        }

        _craftRecipes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        return _craftRecipes;
    }

    private List<FoodRecipePrototype> CookRecipes()
    {
        return _proto.EnumeratePrototypes<FoodRecipePrototype>()
            .Where(r => !r.SecretRecipe)
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private void ProcessCrafter(Entity<ImperialManufacturingComponent> ent)
    {
        SendCraftWithheld(ent);

        if (ent.Comp.CraftUntil != null)
        {
            if (Powered(ent))
            {
                AddLoad(ent, ent.Comp.PowerCost);
            }
            else
            {
                ent.Comp.CraftUntil = null;
                Idle(ent);
                _popup.PopupEntity(Loc.GetString("imperial-manufacturing-power-failure"), ent);
            }

            return;
        }

        if (ent.Comp.Recipe is not { } recipeId || !Powered(ent))
            return;

        TimeSpan time;
        if (ent.Comp.Cooking)
        {
            if (!_proto.TryIndex<FoodRecipePrototype>(recipeId, out var food) || !TryCook(ent, food, false))
                return;
            time = TimeSpan.FromSeconds(food.CookTime);
        }
        else
        {
            if (CraftRecipes().FirstOrDefault(r => r.Id == recipeId) is not { } recipe || !TryCraft(ent, recipe, false))
                return;
            time = recipe.Time;
        }

        ent.Comp.CraftUntil = _timing.CurTime + time;
        Flick(ent, "crafter_printing", time);
        AddLoad(ent, ent.Comp.PowerCost);
    }

    /// <summary>send_withheld(): результаты, всё ещё лежащие на машине.</summary>
    private void SendCraftWithheld(Entity<ImperialManufacturingComponent> ent)
    {
        if (!TryGetTile(ent, out var grid, out var gridComp, out var tile))
            return;

        foreach (var item in ent.Comp.WithheldList.ToList())
        {
            if (TerminatingOrDeleted(item) || _map.TileIndicesFor(grid, gridComp, Transform(item).Coordinates) != tile
                || _container.IsEntityInContainer(item) || Send(ent, item, Dir(ent)))
                ent.Comp.WithheldList.Remove(item);
        }
    }

    /// <summary>craft().</summary>
    private void FinishCraft(Entity<ImperialManufacturingComponent> ent)
    {
        ent.Comp.CraftUntil = null;
        Idle(ent);
        if (ent.Comp.Recipe is not { } recipeId)
            return;

        var ok = ent.Comp.Cooking
            ? _proto.TryIndex<FoodRecipePrototype>(recipeId, out var food) && TryCook(ent, food, true)
            : CraftRecipes().FirstOrDefault(r => r.Id == recipeId) is { } recipe && TryCraft(ent, recipe, true);

        if (!ok)
            _popup.PopupEntity(Loc.GetString("imperial-manufacturing-craft-failed"), ent);
        SendCraftWithheld(ent);
    }

    /// <summary>Ресурсы на клетке сборщика, кроме уже собранного.</summary>
    private List<EntityUid> Surroundings(Entity<ImperialManufacturingComponent> ent)
    {
        if (!TryGetTile(ent, out var grid, out var gridComp, out var tile))
            return new List<EntityUid>();

        return OverfloorObjects(grid, gridComp, tile, ent).Where(e => !ent.Comp.WithheldList.Contains(e)).ToList();
    }

    /// <summary>check_contents() / construct_item() для рецепта конструирования.</summary>
    private bool TryCraft(Entity<ImperialManufacturingComponent> ent, CraftRecipe recipe, bool consume)
    {
        var items = Surroundings(ent);
        var stacks = new Dictionary<EntityUid, int>();
        var used = new HashSet<EntityUid>();

        foreach (var step in recipe.Steps)
        {
            switch (step)
            {
                case MaterialConstructionGraphStep material:
                {
                    var need = material.Amount;
                    foreach (var item in items)
                    {
                        if (need <= 0)
                            break;
                        if (used.Contains(item) || !TryComp<StackComponent>(item, out var stack) || stack.StackTypeId != material.MaterialPrototypeId)
                            continue;

                        var left = stack.Count - stacks.GetValueOrDefault(item);
                        var take = Math.Min(left, need);
                        if (take <= 0)
                            continue;
                        stacks[item] = stacks.GetValueOrDefault(item) + take;
                        need -= take;
                    }

                    if (need > 0)
                        return false;
                    break;
                }
                case ArbitraryInsertConstructionGraphStep insert:
                {
                    var found = items.FirstOrDefault(i => !used.Contains(i) && !stacks.ContainsKey(i) && insert.EntityValid(i, EntityManager, _factory));
                    if (found == default)
                        return false;
                    used.Add(found);
                    break;
                }
                default:
                    return false;
            }
        }

        if (!consume)
            return true;

        foreach (var (item, amount) in stacks)
        {
            var stack = Comp<StackComponent>(item);
            if (amount >= stack.Count)
                QueueDel(item);
            else
                _stack.ReduceCount((item, stack), amount);
        }

        foreach (var item in used)
        {
            QueueDel(item);
        }

        AddResult(ent, Spawn(recipe.Result, Transform(ent).Coordinates));
        return true;
    }

    /// <summary>Рецепт готовки: твёрдые ингредиенты по прототипу, реагенты из открытых ёмкостей на клетке.</summary>
    private bool TryCook(Entity<ImperialManufacturingComponent> ent, FoodRecipePrototype recipe, bool consume)
    {
        var items = Surroundings(ent);
        var used = new List<EntityUid>();

        foreach (var (solid, amount) in recipe.IngredientsSolids)
        {
            var found = items.Where(i => !used.Contains(i) && MetaData(i).EntityPrototype?.ID == solid).Take(amount.Int()).ToList();
            if (found.Count < amount.Int())
                return false;
            used.AddRange(found);
        }

        foreach (var (reagent, amount) in recipe.IngredientsReagents)
        {
            var total = Content.Shared.FixedPoint.FixedPoint2.Zero;
            foreach (var item in items)
            {
                if (_solution.TryGetDrainableSolution(item, out _, out var solution))
                    total += solution.GetTotalPrototypeQuantity(reagent);
            }

            if (total < amount)
                return false;
        }

        if (!consume)
            return true;

        foreach (var (reagent, amount) in recipe.IngredientsReagents)
        {
            var need = amount;
            foreach (var item in items)
            {
                if (need <= 0)
                    break;
                if (_solution.TryGetDrainableSolution(item, out var soln, out _))
                    need -= _solution.RemoveReagent(soln.Value, reagent, need);
            }
        }

        foreach (var item in used)
        {
            QueueDel(item);
        }

        AddResult(ent, Spawn(recipe.Result, Transform(ent).Coordinates));
        return true;
    }

    private void AddResult(Entity<ImperialManufacturingComponent> ent, EntityUid result)
    {
        _transform.SetLocalPosition(result, Transform(result).LocalPosition + new System.Numerics.Vector2(_random.NextFloat(-0.125f, 0.125f), _random.NextFloat(-0.125f, 0.125f)));
        ent.Comp.WithheldList.Add(result);
    }

    #endregion

    #region Сортировщик

    private void ProcessSorter(Entity<ImperialManufacturingComponent> ent)
    {
        if (!TryGetTile(ent, out var grid, out var gridComp, out var tile))
            return;

        foreach (var mover in _lookup.GetLocalEntitiesIntersecting(grid, tile, 0f, gridComp: gridComp))
        {
            if (!CanBeLaunched(ent, mover))
                continue;

            foreach (var filter in ent.Comp.Filters)
            {
                if (MeetsConditions(filter, mover) == filter.Inverted)
                    continue;

                SendNoMobs(ent, mover, filter.Dir);
                return;
            }

            SendNoMobs(ent, mover, ent.Comp.DirIfNotMet);
            return;
        }
    }

    /// <summary>can_be_launched().</summary>
    private bool CanBeLaunched(EntityUid sorter, EntityUid target)
    {
        if (target == sorter || Transform(target).Anchored || _container.IsEntityInContainer(target))
            return false;
        if (!HasComp<ItemComponent>(target) && !HasComp<MobStateComponent>(target))
            return false;

        return !_mobState.IsDead(target);
    }

    /// <summary>send_nomobs(): мобов просто сталкивает на соседнюю клетку.</summary>
    private void SendNoMobs(Entity<ImperialManufacturingComponent> ent, EntityUid mover, Direction dir)
    {
        Flick(ent, "router_operate", TimeSpan.FromSeconds(1), dir);
        if (!HasComp<MobStateComponent>(mover))
        {
            Send(ent, mover, dir);
            return;
        }

        if (Send(ent, null, dir) && TryGetTile(ent, out var grid, out var gridComp, out var tile))
            _transform.SetCoordinates(mover, _map.GridTileToLocal(grid, gridComp, tile + dir.ToIntVec()));
    }

    /// <summary>meets_conditions() фильтров sortrouter_filter.</summary>
    private bool MeetsConditions(ImperialSorterFilter filter, EntityUid checking)
    {
        switch (filter.Type)
        {
            case ImperialSorterFilterType.IsStack:
                return HasComp<StackComponent>(checking);
            case ImperialSorterFilterType.IsOre:
                return _tag.HasTag(checking, OreTag);
            case ImperialSorterFilterType.IsMail:
                return HasComp<Content.Shared.Delivery.DeliveryComponent>(checking);
            case ImperialSorterFilterType.NameContains:
                return Name(checking).ToLowerInvariant().Contains(filter.Value.ToLowerInvariant());
            case ImperialSorterFilterType.SpecificItem:
                return filter.Value != string.Empty && MetaData(checking).EntityPrototype?.ID == filter.Value;
            case ImperialSorterFilterType.SpecificKind:
            {
                if (filter.Value == string.Empty || MetaData(checking).EntityPrototype is not { } proto)
                    return false;
                return proto.ID == filter.Value || _proto.EnumerateParents<EntityPrototype>(proto.ID).Any(p => p.ID == filter.Value);
            }
            default:
                return false;
        }
    }

    #endregion

    #endregion

    #region Взаимодействие

    private void OnInteractUsing(Entity<ImperialManufacturingComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // is_path_specific/sorter_hit(): ждущий фильтр запоминает предмет.
        if (ent.Comp.Kind == ImperialManufacturingKind.Sorter && ent.Comp.Filters.FirstOrDefault(f => f.Listening) is { } listening)
        {
            listening.Listening = false;
            listening.Value = MetaData(args.Used).EntityPrototype?.ID ?? string.Empty;
            listening.ValueName = Name(args.Used);
            _popup.PopupEntity(Loc.GetString("imperial-manufacturing-sorter-filter-set"), ent, args.User);
            UpdateSorterUi(ent);
            args.Handled = true;
            return;
        }

        if (ent.Comp.Kind == ImperialManufacturingKind.StorageBox && _tool.HasQuality(args.Used, ScrewingQuality))
        {
            _popup.PopupEntity(Loc.GetString("imperial-manufacturing-box-disassembling"), ent, args.User);
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(5), new ImperialStorageBoxDisassembleDoAfterEvent(), ent, ent, args.Used)
            {
                BreakOnMove = true,
                NeedHand = true,
            });
            args.Handled = true;
            return;
        }

        if (!_tool.HasQuality(args.Used, Pulsing))
            return;

        args.Handled = MultitoolAct(ent, args.User);
    }

    private static readonly string ScrewingQuality = "Screwing";

    private bool MultitoolAct(Entity<ImperialManufacturingComponent> ent, EntityUid user)
    {
        switch (ent.Comp.Kind)
        {
            case ImperialManufacturingKind.Unloader:
                ent.Comp.FlipSide = !ent.Comp.FlipSide;
                _popup.PopupEntity(Loc.GetString("imperial-manufacturing-unloader-flipped"), ent, user);
                UpdateVisuals(ent);
                return true;
            case ImperialManufacturingKind.Lathe:
                OpenSelect(ent, user, SelectDesign, Loc.GetString("imperial-manufacturing-select-design"),
                    LatheDesigns().Select(d => (d.ID, DesignName(d))));
                return true;
            case ImperialManufacturingKind.Crafter:
                if (ent.Comp.Cooking)
                {
                    OpenSelect(ent, user, SelectRecipe, Loc.GetString("imperial-manufacturing-select-recipe"),
                        CookRecipes().Select(r => (r.ID, r.Name)));
                }
                else
                {
                    OpenSelect(ent, user, SelectRecipe, Loc.GetString("imperial-manufacturing-select-recipe"),
                        CraftRecipes().Select(r => (r.Id, r.Name)));
                }

                return true;
            case ImperialManufacturingKind.Router:
                ToggleRouterDir(ent, user);
                return true;
            case ImperialManufacturingKind.Sorter:
                OpenSorter(ent, user);
                return true;
            default:
                return false;
        }
    }

    /// <summary>router/multitool_act(): переключает выход со стороны игрока.</summary>
    private void ToggleRouterDir(Entity<ImperialManufacturingComponent> ent, EntityUid user)
    {
        if (!TryGetTile(ent, out var grid, out var gridComp, out var tile))
            return;

        var userTile = _map.TileIndicesFor(grid, gridComp, Transform(user).Coordinates);
        var delta = userTile - tile;
        if ((delta.X != 0) == (delta.Y != 0))
        {
            _popup.PopupEntity(Loc.GetString("imperial-manufacturing-router-inline"), ent, user);
            return;
        }

        var dir = delta.X > 0 ? Direction.East : delta.X < 0 ? Direction.West : delta.Y > 0 ? Direction.North : Direction.South;
        if (!ent.Comp.DisabledDirs.Remove(dir))
            ent.Comp.DisabledDirs.Add(dir);

        UpdateVisuals(ent);
        _popup.PopupEntity(Loc.GetString("imperial-manufacturing-router-toggled"), ent, user);
    }

    private void OnInteractHand(Entity<ImperialManufacturingComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || ent.Comp.Kind != ImperialManufacturingKind.StorageBox || Contents(ent).ContainedEntities.Count == 0)
            return;

        // storagebox/attack_hand()
        _popup.PopupEntity(Loc.GetString("imperial-manufacturing-box-dumping"), ent, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(1.25), new ImperialStorageBoxDumpDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
        });
        args.Handled = true;
    }

    private void OnBoxDump(Entity<ImperialManufacturingComponent> ent, ref ImperialStorageBoxDumpDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        _container.EmptyContainer(Contents(ent), true);
    }

    private void OnBoxDisassemble(Entity<ImperialManufacturingComponent> ent, ref ImperialStorageBoxDisassembleDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        // storagebox/atom_destruction()
        args.Handled = true;
        _stack.SpawnMultipleAtPosition(Steel, 10, Transform(ent).Coordinates);
        QueueDel(ent);
    }

    private void OnAltVerbs(Entity<ImperialManufacturingComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Kind != ImperialManufacturingKind.Lathe || ent.Comp.Materials.Count == 0)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-manufacturing-lathe-dump-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
            Act = () => DumpMaterials(ent),
        });
    }

    #endregion

    #region Осмотр

    private void ExamineMachine(Entity<ImperialManufacturingComponent> ent, ExaminedEvent args)
    {
        switch (ent.Comp.Kind)
        {
            case ImperialManufacturingKind.Lathe:
            {
                LatheRecipePrototype? design = null;
                if (ent.Comp.DesignId is { } id)
                    _proto.TryIndex(id, out design);

                args.PushMarkup(design == null
                    ? Loc.GetString("imperial-manufacturing-lathe-examine-none")
                    : Loc.GetString("imperial-manufacturing-lathe-examine", ("design", FormattedMessage.EscapeText(DesignName(design)))));
                if (design == null)
                    break;

                args.PushMarkup(Loc.GetString("imperial-manufacturing-examine-needs"));
                foreach (var (material, amount) in design.Materials)
                {
                    var name = _proto.TryIndex(material, out var mat) ? Loc.GetString(mat.Name) : material.Id;
                    args.PushMarkup(Loc.GetString("imperial-manufacturing-lathe-examine-material", ("amount", amount / (float) SheetAmount), ("material", name)));
                }

                foreach (var (material, amount) in ent.Comp.Materials)
                {
                    var name = _proto.TryIndex<MaterialPrototype>(material, out var mat) ? Loc.GetString(mat.Name) : material;
                    args.PushMarkup(Loc.GetString("imperial-manufacturing-lathe-examine-stored", ("amount", amount / (float) SheetAmount), ("material", name)));
                }

                break;
            }
            case ImperialManufacturingKind.Crafter:
            {
                string? name = null;
                var needs = new List<string>();
                if (ent.Comp.Recipe is { } id)
                {
                    if (ent.Comp.Cooking && _proto.TryIndex<FoodRecipePrototype>(id, out var food))
                    {
                        name = food.Name;
                        foreach (var (solid, amount) in food.IngredientsSolids)
                        {
                            needs.Add(Loc.GetString("imperial-manufacturing-crafter-examine-item", ("amount", amount.Int()),
                                ("item", _proto.TryIndex<EntityPrototype>(solid, out var p) ? p.Name : solid)));
                        }

                        foreach (var (reagent, amount) in food.IngredientsReagents)
                        {
                            needs.Add(Loc.GetString("imperial-manufacturing-crafter-examine-reagent", ("amount", amount),
                                ("reagent", _proto.TryIndex<Content.Shared.Chemistry.Reagent.ReagentPrototype>(reagent, out var r) ? r.LocalizedName : reagent)));
                        }
                    }
                    else if (CraftRecipes().FirstOrDefault(r => r.Id == id) is { } recipe)
                    {
                        name = recipe.Name;
                        foreach (var step in recipe.Steps)
                        {
                            needs.Add(step is MaterialConstructionGraphStep material
                                ? Loc.GetString("imperial-manufacturing-crafter-examine-item", ("amount", material.Amount),
                                    ("item", _proto.TryIndex(material.MaterialPrototypeId, out var stack) ? Loc.GetString(stack.Name) : material.MaterialPrototypeId.Id))
                                : Loc.GetString("imperial-manufacturing-crafter-examine-item", ("amount", 1),
                                    ("item", step is ArbitraryInsertConstructionGraphStep insert && !string.IsNullOrEmpty(insert.Name) ? Loc.GetString(insert.Name) : "?")));
                        }
                    }
                }

                args.PushMarkup(name == null
                    ? Loc.GetString("imperial-manufacturing-crafter-examine-none")
                    : Loc.GetString("imperial-manufacturing-crafter-examine", ("recipe", FormattedMessage.EscapeText(name))));
                if (name == null)
                    break;

                args.PushMarkup(Loc.GetString("imperial-manufacturing-examine-needs"));
                foreach (var need in needs)
                {
                    args.PushMarkup(FormattedMessage.EscapeText(need));
                }

                break;
            }
        }
    }

    #endregion
}
