using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Imperial.Disease;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Disease;

/// <summary>
/// Админ-команда вирусологии: заразить, вылечить, собрать болезнь из симптомов.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class DiseaseCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override string Command => "disease";

    public override string Description => "Infect, cure or craft diseases.";

    public override string Help =>
        "disease infect <ckey> <diseaseId> | disease random <ckey> [maxSymptoms] | " +
        "disease make <ckey> <symptomId>... | disease cure <ckey> | disease list";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var system = _entityManager.System<DiseaseSystem>();

        if (args.Length == 0)
        {
            shell.WriteError(Help);
            return;
        }

        if (args[0] == "list")
        {
            shell.WriteLine(string.Join(", ", _proto.EnumeratePrototypes<DiseaseSymptomPrototype>()
                .OrderBy(p => p.Level).Select(p => $"{p.ID}({p.Level})")));
            return;
        }

        if (args.Length < 2)
        {
            shell.WriteError(Help);
            return;
        }

        // Целью может быть и ник игрока, и номер сущности — так удобнее тестировать.
        EntityUid target;
        string targetName;
        if (_players.TryGetSessionByUsername(args[1], out var player) && player.AttachedEntity is { } body)
        {
            target = body;
            targetName = player.Name;
        }
        else if (args[1] == "first" && TryGetFirstBody(out var firstBody))
        {
            target = firstBody;
            targetName = _entityManager.ToPrettyString(target);
        }
        else if (NetEntity.TryParse(args[1], out var net) && _entityManager.TryGetEntity(net, out var entity))
        {
            target = entity.Value;
            targetName = _entityManager.ToPrettyString(target);
        }
        else
        {
            shell.WriteError("Target not found: give a connected player's name or an entity id.");
            return;
        }

        switch (args[0])
        {
            case "infect":
            {
                if (args.Length < 3 || !_proto.TryIndex<DiseasePrototype>(args[2], out var proto))
                {
                    shell.WriteError("Unknown disease prototype.");
                    return;
                }

                var disease = system.CreateFromPrototype(proto);
                if (!system.TryInfect(target, disease))
                {
                    shell.WriteError("Target cannot be infected.");
                    return;
                }

                shell.WriteLine($"Infected {targetName} with {proto.ID}.");
                break;
            }

            case "random":
            {
                var count = args.Length > 2 && int.TryParse(args[2], out var parsed) ? parsed : 0;
                var disease = system.CreateRandomDisease(count);
                if (!system.TryInfect(target, disease))
                {
                    shell.WriteError("Target cannot be infected.");
                    return;
                }

                shell.WriteLine($"Infected {targetName} with {disease.Name}.");
                break;
            }

            case "make":
            {
                var symptoms = new List<ProtoId<DiseaseSymptomPrototype>>();
                foreach (var id in args.Skip(2))
                {
                    if (!_proto.HasIndex<DiseaseSymptomPrototype>(id))
                    {
                        shell.WriteError($"Unknown symptom: {id}");
                        return;
                    }

                    symptoms.Add(id);
                }

                if (symptoms.Count == 0)
                {
                    shell.WriteError("No symptoms given.");
                    return;
                }

                var disease = system.CreateAdvanceDisease(symptoms);
                if (!system.TryInfect(target, disease))
                {
                    shell.WriteError("Target cannot be infected.");
                    return;
                }

                shell.WriteLine($"Infected {targetName} with a custom disease ({disease.GetId()}).");
                break;
            }

            case "status":
            {
                shell.WriteLine($"Lungs: {system.HasOrgan(target, "Lungs")}");
                if (!_entityManager.TryGetComponent<DiseasedComponent>(target, out var sick))
                {
                    shell.WriteLine("Not sick.");
                    return;
                }

                foreach (var disease in sick.Diseases)
                {
                    shell.WriteLine($"{disease.Name} [{disease.GetId()}] stage {disease.Stage}/{disease.MaxStages} " +
                                    $"spread={disease.Spread} res={disease.Resistance} stealth={disease.Stealth} " +
                                    $"speed={disease.StageRate} trans={disease.Transmittable} sev={disease.SeverityValue} " +
                                    $"infectivity={disease.Infectivity} mod={disease.SpreadingModifier}");
                }

                break;
            }

            case "cure":
            {
                if (!_entityManager.TryGetComponent<DiseasedComponent>(target, out var diseased))
                {
                    shell.WriteLine("Target is not sick.");
                    return;
                }

                foreach (var disease in diseased.Diseases.ToArray())
                {
                    system.Cure(target, disease, false);
                }

                shell.WriteLine($"Cured {targetName}.");
                break;
            }

            default:
                shell.WriteError(Help);
                break;
        }
    }

    /// <summary>Последнее появившееся тело: обычно это тот, кого админ только что заспавнил.</summary>
    private bool TryGetFirstBody(out EntityUid body)
    {
        body = default;
        var found = false;
        var query = _entityManager.EntityQueryEnumerator<Content.Shared.Humanoid.HumanoidProfileComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (found && uid.Id <= body.Id)
                continue;

            body = uid;
            found = true;
        }

        return found;
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromOptions(new[] { "infect", "random", "make", "cure", "status", "list" });

        if (args.Length == 2)
            return CompletionResult.FromHintOptions(CompletionHelper.SessionNames(players: _players), "<ckey>");

        if (args.Length >= 3 && args[0] == "make")
        {
            return CompletionResult.FromHintOptions(
                _proto.EnumeratePrototypes<DiseaseSymptomPrototype>().Select(p => p.ID), "<symptom>");
        }

        if (args.Length == 3 && args[0] == "infect")
        {
            return CompletionResult.FromHintOptions(
                _proto.EnumeratePrototypes<DiseasePrototype>().Select(p => p.ID), "<disease>");
        }

        return CompletionResult.Empty;
    }
}
