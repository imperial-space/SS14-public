using System.Linq;
using Content.Server.Arcade.SpaceVillain;
using Content.Server.Botany.Components;
using Content.Server.Radio.EntitySystems;
using Content.Server.Research.Components;
using Content.Server.Research.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Server.Construction.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Explosion.Components;
using Content.Shared.Fluids.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Imperial.Research.Experiments;
using Content.Shared.Imperial.Xenomorph;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Research.Components;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Research;

/// <summary>Прогресс экспериментов TG на сервере исследований.</summary>
[RegisterComponent]
public sealed partial class TgExperimentStateComponent : Component
{
    [ViewVariables]
    public Dictionary<string, int> Progress = new();

    [ViewVariables]
    public HashSet<string> Completed = new();

    /// <summary>Уже засчитанные объекты (каждый сканируется один раз на эксперимент).</summary>
    [ViewVariables]
    public Dictionary<string, HashSet<EntityUid>> Scanned = new();

    /// <summary>Привязанные аркадные автоматы и их запас призов на момент проверки.</summary>
    [ViewVariables]
    public Dictionary<EntityUid, int> Arcades = new();
}

/// <summary>
/// Эксперименты techweb TG (code/modules/experisci): сканер экспериментов, взрывы, аркады.
/// Обязательный эксперимент раскрывает узел, скидочный — возвращает серверу очки (discount × 75).
/// </summary>
public sealed class TgExperimentSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly Robust.Shared.Timing.IGameTiming _timing = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly Content.Shared.Damage.Systems.DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly ImperialTgResearchSystem _tg = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const int PointScale = 75;
    private const string ScienceChannel = "Science";

    private static readonly SoundSpecifier ScanSound = new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg");
    private static readonly SoundSpecifier FailSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");

    private static readonly ProtoId<TagPrototype> CyberOrganTag = "ImperialCyberOrgan";
    private static readonly ProtoId<TagPrototype> SkillchipTag = "ImperialSkillchip";

    private readonly List<EntityUid> _pendingExplosions = new();
    private TimeSpan _nextConsoleSync;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TgExperiScannerComponent, AfterInteractEvent>(OnScannerAfterInteract);
        SubscribeLocalEvent<ExplosionVisualsComponent, ComponentStartup>(OnExplosionStartup);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now >= _nextConsoleSync)
        {
            _nextConsoleSync = now + TimeSpan.FromSeconds(1);
            SyncAllConsoles();
        }

        // Данные взрыва заполняются после создания компонента — читаем на следующем тике.
        if (_pendingExplosions.Count > 0)
        {
            foreach (var boom in _pendingExplosions)
            {
                if (TryComp<ExplosionVisualsComponent>(boom, out var visuals))
                    OnExplosion(visuals.Epicenter.MapId, visuals.Intensity.Count);
            }

            _pendingExplosions.Clear();
        }

        var servers = EntityQueryEnumerator<TgExperimentStateComponent>();
        while (servers.MoveNext(out var server, out var state))
        {
            if (state.Arcades.Count == 0)
                continue;

            foreach (var (arcade, rewards) in state.Arcades.ToList())
            {
                if (!TryComp<SpaceVillainArcadeComponent>(arcade, out var comp))
                {
                    state.Arcades.Remove(arcade);
                    continue;
                }

                if (comp.RewardAmount >= rewards)
                    continue;

                // Приз выдан — значит, кто-то выиграл.
                state.Arcades[arcade] = comp.RewardAmount;
                foreach (var exp in Experiments<TgArcadeRequirement>())
                {
                    AddProgress((server, state), exp, 1, arcade);
                }
            }
        }
    }

    private IEnumerable<TgExperimentPrototype> Experiments<T>() where T : TgExperimentRequirement
    {
        return _proto.EnumeratePrototypes<TgExperimentPrototype>().Where(e => e.Requirement is T);
    }

    #region Сервер и консоли

    /// <summary>Сервер исследований станции, на которой находится объект.</summary>
    public EntityUid? FindServer(EntityUid near)
    {
        var station = _station.GetOwningStation(near);
        var map = Transform(near).MapID;
        EntityUid? fallback = null;
        var query = EntityQueryEnumerator<ResearchServerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (station != null && _station.GetOwningStation(uid, xform) == station)
                return uid;

            if (xform.MapID == map)
                fallback ??= uid;
        }

        return fallback;
    }

    /// <summary>Консоли подключаются и переключаются между серверами — раз в секунду сверяем копию прогресса.</summary>
    private void SyncAllConsoles()
    {
        var query = EntityQueryEnumerator<ResearchConsoleComponent, ResearchClientComponent>();
        while (query.MoveNext(out var uid, out _, out var client))
        {
            var view = EnsureComp<TgExperimentViewComponent>(uid);
            TryComp<TgExperimentStateComponent>(client.Server, out var state);
            var progress = state?.Progress ?? new Dictionary<string, int>();
            var completed = state?.Completed.Count ?? 0;
            if (view.Completed.Count == completed && view.Progress.Count == progress.Count && progress.All(p => view.Progress.GetValueOrDefault(p.Key) == p.Value))
                continue;

            view.Progress = new Dictionary<string, int>(progress);
            view.Completed = state?.Completed.ToList() ?? new List<string>();
            Dirty(uid, view);
        }
    }

    private void SyncConsoles(Entity<TgExperimentStateComponent> server)
    {
        var query = EntityQueryEnumerator<ResearchConsoleComponent, ResearchClientComponent>();
        while (query.MoveNext(out var uid, out _, out var client))
        {
            if (client.Server != server.Owner)
                continue;

            var view = EnsureComp<TgExperimentViewComponent>(uid);
            view.Progress = new Dictionary<string, int>(server.Comp.Progress);
            view.Completed = server.Comp.Completed.ToList();
            Dirty(uid, view);
        }
    }

    #endregion

    #region Прогресс

    private void AddProgress(Entity<TgExperimentStateComponent> server, TgExperimentPrototype exp, int amount, EntityUid? scanned)
    {
        if (server.Comp.Completed.Contains(exp.ID))
            return;

        if (scanned is { } target)
        {
            if (!server.Comp.Scanned.TryGetValue(exp.ID, out var set))
                server.Comp.Scanned[exp.ID] = set = new HashSet<EntityUid>();

            if (!set.Add(target))
                return;
        }

        var progress = server.Comp.Progress.GetValueOrDefault(exp.ID) + amount;
        server.Comp.Progress[exp.ID] = progress;

        if (progress >= exp.Requirement.Count)
            Complete(server, exp);

        SyncConsoles(server);
    }

    /// <summary>experiment.complete(): раскрыть узел или вернуть очки, сообщить в научный канал.</summary>
    private void Complete(Entity<TgExperimentStateComponent> server, TgExperimentPrototype exp)
    {
        server.Comp.Completed.Add(exp.ID);
        server.Comp.Scanned.Remove(exp.ID);

        string message;
        if (exp.Required)
        {
            _tg.RevealNode(server, ToCamel(exp.Node));
            message = Loc.GetString("tg-experiment-complete-required", ("name", Loc.GetString(exp.Name)));
        }
        else
        {
            // discount_experiments: снижают цену своего узла при изучении (см. ImperialTgTechwebConsoleSystem).
            var points = exp.Discount * PointScale;
            message = Loc.GetString("tg-experiment-complete-discount", ("name", Loc.GetString(exp.Name)), ("points", points));
        }

        Log.Info($"Эксперимент {exp.ID} завершён на {ToPrettyString(server)}");
        _radio.SendRadioMessage(server, message, ScienceChannel, server);
    }

    private static string ToCamel(string node)
    {
        return string.Concat(node.Split('_').Select(p => p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..]));
    }

    private Entity<TgExperimentStateComponent>? GetState(EntityUid? server)
    {
        if (server is not { } uid || !HasComp<ResearchServerComponent>(uid))
            return null;

        return (uid, EnsureComp<TgExperimentStateComponent>(uid));
    }

    #endregion

    #region Взрывы

    private void OnExplosionStartup(Entity<ExplosionVisualsComponent> ent, ref ComponentStartup args)
    {
        _pendingExplosions.Add(ent);
    }

    /// <summary>ordnance/explosive: доплеровская матрица заменена регистрацией взрыва на карте сервера.</summary>
    private void OnExplosion(Robust.Shared.Map.MapId map, int radius)
    {
        var query = EntityQueryEnumerator<ResearchServerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != map || GetState(uid) is not { } state)
                continue;

            foreach (var exp in Experiments<TgExplosionRequirement>())
            {
                if (radius >= ((TgExplosionRequirement) exp.Requirement).MinRadius)
                    AddProgress(state, exp, exp.Requirement.Count, null);
            }
        }
    }

    #endregion

    #region Сканер

    /// <summary>handheld_scanner: сканирует объект для всех подходящих экспериментов сервера.</summary>
    private void OnScannerAfterInteract(Entity<TgExperiScannerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = true;
        var user = args.User;

        if (GetState(FindServer(ent)) is not { } state)
        {
            _popup.PopupEntity(Loc.GetString("tg-experiscanner-no-server"), user, user);
            return;
        }

        // physical/arcade_winner: привязка аркады.
        if (TryComp<SpaceVillainArcadeComponent>(target, out var arcade) && Experiments<TgArcadeRequirement>().Any())
        {
            state.Comp.Arcades[target] = arcade.RewardAmount;
            _popup.PopupEntity(Loc.GetString("tg-experiscanner-arcade-linked"), user, user);
            _audio.PlayPvs(ScanSound, ent);
            return;
        }

        var any = false;
        foreach (var exp in _proto.EnumeratePrototypes<TgExperimentPrototype>())
        {
            if (state.Comp.Completed.Contains(exp.ID))
                continue;

            var points = Evaluate(exp.Requirement, target);
            if (points <= 0)
                continue;

            if (state.Comp.Scanned.TryGetValue(exp.ID, out var set) && set.Contains(target))
            {
                _popup.PopupEntity(Loc.GetString("tg-experiscanner-already", ("name", Loc.GetString(exp.Name))), user, user);
                continue;
            }

            any = true;
            AddProgress(state, exp, points, target);
            _popup.PopupEntity(Loc.GetString("tg-experiscanner-scanned", ("name", Loc.GetString(exp.Name)),
                ("progress", Math.Min(state.Comp.Progress.GetValueOrDefault(exp.ID), exp.Requirement.Count)),
                ("count", exp.Requirement.Count)), user, user);
        }

        if (!any)
            _popup.PopupEntity(Loc.GetString("tg-experiscanner-nothing"), user, user);

        _audio.PlayPvs(any ? ScanSound : FailSound, ent);
    }

    /// <summary>
    /// Сканирование особым прибором (experiment_handler с allowed_experiments и EXPERIMENT_CONFIG_SILENT_FAIL):
    /// только перечисленные эксперименты, без сообщения о неудаче.
    /// </summary>
    public void ScanFor(EntityUid scanner, EntityUid user, EntityUid target, IReadOnlyCollection<string> allowed)
    {
        if (GetState(FindServer(scanner)) is not { } state)
            return;

        foreach (var id in allowed)
        {
            if (state.Comp.Completed.Contains(id) || !_proto.TryIndex<TgExperimentPrototype>(id, out var exp))
                continue;

            var points = Evaluate(exp.Requirement, target);
            if (points <= 0 || state.Comp.Scanned.TryGetValue(exp.ID, out var set) && set.Contains(target))
                continue;

            AddProgress(state, exp, points, target);
            _popup.PopupEntity(Loc.GetString("tg-experiscanner-scanned", ("name", Loc.GetString(exp.Name)),
                ("progress", Math.Min(state.Comp.Progress.GetValueOrDefault(exp.ID), exp.Requirement.Count)),
                ("count", exp.Requirement.Count)), user, user);
        }
    }

    /// <summary>Сколько засчитать объекту для эксперимента (0 — не подходит).</summary>
    private int Evaluate(TgExperimentRequirement requirement, EntityUid target)
    {
        switch (requirement)
        {
            case TgScanRequirement scan:
            {
                var proto = MetaData(target).EntityPrototype?.ID;
                foreach (var t in scan.Targets)
                {
                    if (proto != null && t.Prototypes.Any(p => p.Id == proto) || t.Whitelist != null && _whitelist.IsValid(t.Whitelist, target))
                        return t.Points;
                }

                return 0;
            }
            case TgReagentRequirement reagent:
                return ContainsReagent(target, reagent.Reagent, reagent.MinAmount) ? 1 : 0;
            case TgPuddleRequirement puddle:
                return HasComp<PuddleComponent>(target) && puddle.Reagents.Any(r => ContainsReagent(target, r, 1f)) ? 1 : 0;
            case TgGasRequirement gas:
                return GasMoles(target, gas.Gas) >= gas.Moles ? 1 : 0;
            case TgConditionRequirement condition:
                return CheckCondition(condition.Condition, target) ? 1 : 0;
            default:
                return 0;
        }
    }

    private bool ContainsReagent(EntityUid target, string reagent, float min)
    {
        if (!TryComp<SolutionContainerManagerComponent>(target, out var manager))
            return false;

        foreach (var (_, solution) in _solution.EnumerateSolutions(manager))
        {
            if (solution.GetTotalPrototypeQuantity(reagent) >= min)
                return true;
        }

        return false;
    }

    private float GasMoles(EntityUid target, string gasName)
    {
        if (!Enum.TryParse<Gas>(gasName, out var gas))
            return 0f;

        if (TryComp<GasTankComponent>(target, out var tank))
            return tank.Air.GetMoles(gas);

        if (TryComp<GasCanisterComponent>(target, out var canister))
            return canister.Air.GetMoles(gas);

        return 0f;
    }

    private IEnumerable<EntityUid> Organs(EntityUid body)
    {
        return _container.TryGetContainer(body, BodyComponent.ContainerID, out var organs)
            ? organs.ContainedEntities
            : Enumerable.Empty<EntityUid>();
    }

    private bool CheckCondition(TgScanCondition condition, EntityUid target)
    {
        switch (condition)
        {
            case TgScanCondition.Mutant:
                return HasComp<HumanoidProfileComponent>(target) && TryComp<GenomeComponent>(target, out var genome) && genome.Active.Count > 0;
            case TgScanCondition.AugmentedOrgans:
                return HasComp<HumanoidProfileComponent>(target) && Organs(target).Any(o => _tag.HasTag(o, CyberOrganTag));
            case TgScanCondition.Android:
            {
                if (!HasComp<HumanoidProfileComponent>(target))
                    return false;

                var organs = Organs(target).ToList();
                return organs.Count >= 4 && organs.All(o => _tag.HasTag(o, CyberOrganTag));
            }
            case TgScanCondition.Skillchip:
                return HasComp<HumanoidProfileComponent>(target) && Organs(target).Any(o => _tag.HasTag(o, SkillchipTag));
            case TgScanCondition.MechEquipped:
                return TryComp<MechComponent>(target, out var mech) && mech.EquipmentContainer.ContainedEntities.Count > 0;
            case TgScanCondition.MechDamaged:
                return HasComp<MechComponent>(target) && HasComp<DamageableComponent>(target) && _damageable.GetTotalDamage(target) > 0;
            case TgScanCondition.PlantMutated:
                return TryComp<PlantHolderComponent>(target, out var holder) && holder.Seed is { Unique: true };
            case TgScanCondition.PlantTrait:
                return TryComp<PlantHolderComponent>(target, out var traits) && traits.Seed is { } seed
                       && (seed.Ligneous || seed.Seedless || seed.CanScream || seed.TurnIntoKudzu || seed.Mutations.Count > 0);
            case TgScanCondition.AutopsyHuman:
                return _mobState.IsDead(target) && TryComp<HumanoidProfileComponent>(target, out var human) && human.Species == "Human";
            case TgScanCondition.AutopsyNonHuman:
                return _mobState.IsDead(target) && TryComp<HumanoidProfileComponent>(target, out var nonhuman) && nonhuman.Species != "Human";
            case TgScanCondition.AutopsyXeno:
                return _mobState.IsDead(target) && HasComp<XenomorphComponent>(target);
            case TgScanCondition.Machine:
                return HasComp<MachineComponent>(target);
            default:
                return false;
        }
    }

    #endregion
}
