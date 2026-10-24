using Content.Server.Chat.Managers;
using Content.Server.Electrocution;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Shared.Chat;
using Content.Shared.Electrocution;
using Content.Shared.GameTicking.Components;
using Content.Shared.Imperial.Voidwalker;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Voidwalker;

/// <summary>/datum/dynamic_ruleset/midround/from_ghosts/voidwalker (и гелиарх).</summary>
[RegisterComponent]
public sealed partial class ImperialVoidwalkerRuleComponent : Component
{
    [DataField]
    public EntProtoId Spawner = "ImperialSpawnPointVoidwalker";
}

/// <summary>Роль пустоходца на разуме.</summary>
[RegisterComponent]
public sealed partial class ImperialVoidwalkerRoleComponent : Component;

/// <summary>/datum/objective/voidwalker_objective: выполнена, пока пустоходец жив.</summary>
[RegisterComponent]
public sealed partial class ImperialVoidwalkerConditionComponent : Component;

/// <summary>Тело, которое при вселении получает роль, цель и приветствие.</summary>
[RegisterComponent]
public sealed partial class ImperialVoidwalkerAntagComponent : Component
{
    [DataField]
    public EntProtoId Objective = "ImperialVoidwalkerObjective";

    [DataField]
    public LocId Greeting = "imperial-voidwalker-greeting";

    /// <summary>Варианты текста цели (pick(explanation_texts), 5% — про стекло).</summary>
    [DataField]
    public List<LocId> ObjectiveTexts = new();

    [ViewVariables]
    public bool Given;
}

public sealed class ImperialVoidwalkerRuleSystem : GameRuleSystem<ImperialVoidwalkerRuleComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;

    private const string MindRole = "MindRoleImperialVoidwalker";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialVoidwalkerAntagComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<ImperialVoidwalkerConditionComponent, ObjectiveGetProgressEvent>(OnProgress);
    }

    /// <summary>find_space_spawn(): гост-роль в открытом космосе у станции.</summary>
    protected override void Started(EntityUid uid, ImperialVoidwalkerRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var stations = _station.GetStations();
        if (stations.Count == 0 || _station.GetLargestGrid(_random.Pick(stations)) is not { } grid
            || !TryComp<MapGridComponent>(grid, out var gridComp))
        {
            ForceEndSelf(uid, gameRule);
            return;
        }

        var xform = Transform(grid);
        var bounds = gridComp.LocalAABB;
        var radius = Math.Max(bounds.Width, bounds.Height) / 2f;
        var center = System.Numerics.Vector2.Transform(bounds.Center, _transform.GetWorldMatrix(xform));
        var position = center + _random.NextAngle().ToVec() * (radius + _random.NextFloat(15f, 30f));
        Spawn(component.Spawner, new Robust.Shared.Map.MapCoordinates(position, xform.MapID));
    }

    private void OnMindAdded(Entity<ImperialVoidwalkerAntagComponent> ent, ref MindAddedMessage args)
    {
        if (ent.Comp.Given)
            return;

        ent.Comp.Given = true;
        var mindId = args.Mind.Owner;
        var mind = args.Mind.Comp;
        if (!_role.MindHasRole<ImperialVoidwalkerRoleComponent>(mindId))
            _role.MindAddRole(mindId, MindRole, mind, silent: true);

        if (_objectives.TryCreateObjective(mindId, mind, ent.Comp.Objective) is { } objective)
        {
            if (ent.Comp.ObjectiveTexts.Count > 0)
            {
                var text = _random.Prob(0.05f) ? "imperial-voidwalker-objective-glass" : _random.Pick(ent.Comp.ObjectiveTexts).Id;
                _meta.SetEntityName(objective, Loc.GetString(text));
            }

            _mind.AddObjective(mindId, mind, objective);
        }

        if (mind.UserId is { } userId && _players.TryGetSessionById(userId, out var session))
        {
            var message = Loc.GetString(ent.Comp.Greeting);
            var plain = Robust.Shared.Utility.FormattedMessage.RemoveMarkupOrThrow(message);
            _chatManager.ChatMessageToOne(ChatChannel.Server, plain, message, default, false, session.Channel);
        }
    }

    private void OnProgress(Entity<ImperialVoidwalkerConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = args.Mind.OwnedEntity is { } body && !_mobs.IsDead(body) ? 1f : 0f;
    }
}

/// <summary>Серверная часть glass_passer: решётка под током (с питанием) не пропускает.</summary>
public sealed class ImperialGlassPasserSystem : SharedImperialGlassPasserSystem
{
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;

    private static readonly System.Reflection.MethodInfo? IsPoweredMethod = typeof(ElectrocutionSystem).GetMethod("IsPowered",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
        new[] { typeof(EntityUid), typeof(ElectrifiedComponent), typeof(TransformComponent) }, null);

    protected override bool IsShocked(EntityUid grille)
    {
        if (!TryComp<ElectrifiedComponent>(grille, out var electrified) || !electrified.Enabled)
            return false;

        // ElectrocutionSystem.IsPowered приватный: та же проверка, что и при ударе током.
        return IsPoweredMethod?.Invoke(_electrocution, new object[] { grille, electrified, Transform(grille) }) is true;
    }
}
