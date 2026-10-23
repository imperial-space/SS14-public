using System.Linq;
using Content.Server.Administration;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Roles;
using Content.Shared.Administration;
using Content.Shared.Dataset;
using Content.Shared.GameTicking.Components;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.NPC.Systems;
using Content.Shared.Roles;
using Robust.Shared.Console;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard;

[RegisterComponent]
public sealed partial class ImperialWizardRuleComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Wizards = new();
}

/// <summary>Роль разума мага (/datum/antagonist/wizard).</summary>
[RegisterComponent]
public sealed partial class ImperialWizardRoleComponent : Component;

/// <summary>Роль разума ученика или прислужника (/datum/antagonist/wizard_minion, apprentice).</summary>
[RegisterComponent]
public sealed partial class ImperialWizardApprenticeRoleComponent : Component;

/// <summary>Данные мага на разуме: перки, покупки для итогов раунда, учитель ученика.</summary>
[RegisterComponent]
public sealed partial class ImperialWizardMindComponent : Component
{
    /// <summary>perks.</summary>
    [ViewVariables]
    public HashSet<string> Perks = new();

    /// <summary>GLOB.wizard_spellbook_purchases_by_key: запись → сколько раз куплена.</summary>
    [ViewVariables]
    public Dictionary<string, int> Purchases = new();

    [ViewVariables]
    public bool Apprentice;

    [ViewVariables]
    public EntityUid? Master;
}

/// <summary>Цель прислужника: хозяин должен дожить до конца раунда.</summary>
[RegisterComponent]
public sealed partial class ImperialWizardServeConditionComponent : Component;

/// <summary>Сетка логова мага (/area/centcom/wizard_station).</summary>
[RegisterComponent]
public sealed partial class ImperialWizardDenComponent : Component;

/// <summary>
/// /datum/antagonist/wizard: правило, цели (create_objectives), смена имени, логово, итоги раунда.
/// </summary>
public sealed class ImperialWizardRuleSystem : GameRuleSystem<ImperialWizardRuleComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly QuickDialogSystem _dialog = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly ImperialSpellbookSystem _spellbook = default!;
    [Dependency] private readonly Content.Shared.Station.SharedStationSpawningSystem _spawning = default!;

    private const string MindRole = "MindRoleImperialWizard";
    private const string ApprenticeRole = "MindRoleImperialWizardApprentice";
    private const string ServeObjective = "ImperialWizardServeObjective";
    private static readonly ProtoId<StartingGearPrototype> Gear = "ImperialWizardGear";
    private const string WizardFaction = "Wizard";
    private static readonly ProtoId<LocalizedDatasetPrototype> FirstNames = "NamesWizardFirst";
    private static readonly ProtoId<LocalizedDatasetPrototype> LastNames = "NamesWizardLast";

    private const string KillObjective = "ImperialWizardKillObjective";
    private const string EscapeObjective = "ImperialWizardEscapeObjective";
    private const string SurviveObjective = "ImperialWizardSurviveObjective";
    private const string HijackObjective = "ImperialWizardHijackObjective";
    private const string StealGroup = "ImperialWizardObjectiveGroupSteal";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialWizardRuleComponent, AfterAntagEntitySelectedEvent>(OnSelected);
        SubscribeLocalEvent<ImperialWizardRuleComponent, RuleLoadedGridsEvent>(OnGridsLoaded);
        SubscribeLocalEvent<ImperialWizardServeConditionComponent, Content.Shared.Objectives.Components.ObjectiveGetProgressEvent>(OnServeProgress);
    }

    private void OnServeProgress(Entity<ImperialWizardServeConditionComponent> ent, ref Content.Shared.Objectives.Components.ObjectiveGetProgressEvent args)
    {
        args.Progress = TryComp<ImperialWizardMindComponent>(args.MindId, out var minion) && minion.Master is { } master
                        && TryComp<MindComponent>(master, out var masterMind) && masterMind.OwnedEntity is { } body
                        && !EntityManager.System<Content.Shared.Mobs.Systems.MobStateSystem>().IsDead(body)
            ? 1f
            : 0f;
    }

    private void OnGridsLoaded(Entity<ImperialWizardRuleComponent> ent, ref RuleLoadedGridsEvent args)
    {
        foreach (var grid in args.Grids)
        {
            EnsureComp<ImperialWizardDenComponent>(grid);
        }
    }

    private void OnSelected(Entity<ImperialWizardRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        MakeWizard(args.EntityUid, ent.Comp, equip: false);
    }

    /// <summary>on_gain(): роль, фракция, книга привязана к магу, цели, предложение сменить имя.</summary>
    public bool MakeWizard(EntityUid body, ImperialWizardRuleComponent? rule = null, bool equip = true)
    {
        if (!_mind.TryGetMind(body, out var mindId, out var mind) || HasComp<ImperialWizardMindComponent>(mindId))
            return false;

        rule?.Wizards.Add(mindId);
        EnsureComp<ImperialWizardMindComponent>(mindId);
        if (!_role.MindHasRole<ImperialWizardRoleComponent>(mindId))
            _role.MindAddRole(mindId, MindRole, mind, silent: true);

        _faction.ClearFactions(body);
        _faction.AddFaction(body, WizardFaction);
        EnsureComp<ImperialMagicallyGiftedComponent>(body);

        if (equip)
            EquipWizard(body);

        BindSpellbooks(body, mindId, mind);
        CreateObjectives(mindId, mind);
        RenameWizard(body, mindId);
        return true;
    }

    /// <summary>equip_wizard(): /datum/outfit/wizard.</summary>
    public void EquipWizard(EntityUid body)
    {
        _spawning.EquipStartingGear(body, Gear);
    }

    /// <summary>post_equip(): new_spellbook.owner = wizard.mind.</summary>
    private void BindSpellbooks(EntityUid body, EntityUid mindId, MindComponent mind)
    {
        foreach (var item in AllItems(body))
        {
            if (TryComp<ImperialSpellbookComponent>(item, out var book) && book.Owner == null)
                _spellbook.SetOwner((item, book), mindId, mind);
        }
    }

    private IEnumerable<EntityUid> AllItems(EntityUid uid)
    {
        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            yield break;

        foreach (var container in _containers.GetAllContainers(uid, manager))
        {
            foreach (var contained in container.ContainedEntities)
            {
                yield return contained;
                foreach (var inner in AllItems(contained))
                {
                    yield return inner;
                }
            }
        }
    }

    /// <summary>create_objectives(): 30% убийство+побег, 30% кража+побег, 25% убийство+кража+выживание, 15% угон шаттла.</summary>
    private void CreateObjectives(EntityUid mindId, MindComponent mind)
    {
        var roll = _random.Next(1, 101);
        switch (roll)
        {
            case <= 30:
                _mind.TryAddObjective(mindId, mind, KillObjective);
                _mind.TryAddObjective(mindId, mind, EscapeObjective);
                break;
            case <= 60:
                AddSteal(mindId, mind);
                _mind.TryAddObjective(mindId, mind, EscapeObjective);
                break;
            case <= 85:
                _mind.TryAddObjective(mindId, mind, KillObjective);
                AddSteal(mindId, mind);
                _mind.TryAddObjective(mindId, mind, SurviveObjective);
                break;
            default:
                _mind.TryAddObjective(mindId, mind, HijackObjective);
                break;
        }
    }

    private void AddSteal(EntityUid mindId, MindComponent mind)
    {
        if (_objectives.GetRandomObjective(mindId, mind, StealGroup, float.MaxValue) is { } steal)
            _mind.AddObjective(mindId, mind, steal);
    }

    /// <summary>rename_wizard(): «Вы хотите изменить имя?» с предложением случайного имени мага.</summary>
    private void RenameWizard(EntityUid body, EntityUid mindId)
    {
        var random = $"{Loc.GetString(_random.Pick(_proto.Index(FirstNames).Values))} {Loc.GetString(_random.Pick(_proto.Index(LastNames).Values))}";
        if (!TryComp<ActorComponent>(body, out var actor))
        {
            SetName(body, mindId, random);
            return;
        }

        _dialog.OpenDialog<string>(actor.PlayerSession, Loc.GetString("imperial-wizard-rename-title"),
            Loc.GetString("imperial-wizard-rename-prompt", ("random", random)), name =>
            {
                name = name.Trim();
                SetName(body, mindId, name.Length is > 0 and <= 32 ? name : random);
            }, () => SetName(body, mindId, random));
    }

    private void SetName(EntityUid body, EntityUid mindId, string name)
    {
        if (TerminatingOrDeleted(body))
            return;

        _meta.SetEntityName(body, name);
        if (TryComp<MindComponent>(mindId, out var mind))
            mind.CharacterName = name;

        foreach (var item in AllItems(body))
        {
            if (TryComp<ImperialSpellbookComponent>(item, out var book) && book.Owner == mindId)
                book.OwnerName = name;
        }
    }

    /// <summary>
    /// /datum/antagonist/wizard_minion: прислужник мага (скелет, ученик) с целью «Служи мастеру».
    /// </summary>
    public void MakeMinion(EntityUid body, EntityUid master, string objectiveText)
    {
        if (!_mind.TryGetMind(body, out var mindId, out var mind))
            return;

        var minion = EnsureComp<ImperialWizardMindComponent>(mindId);
        minion.Apprentice = true;
        if (_mind.TryGetMind(master, out var masterMind, out _))
            minion.Master = masterMind;

        if (!_role.MindHasRole<ImperialWizardApprenticeRoleComponent>(mindId))
            _role.MindAddRole(mindId, ApprenticeRole, mind, silent: true);

        _faction.ClearFactions(body);
        _faction.AddFaction(body, WizardFaction);
        EnsureComp<ImperialMagicallyGiftedComponent>(body);

        if (_objectives.TryCreateObjective(mindId, mind, ServeObjective) is { } objective)
        {
            _meta.SetEntityName(objective, objectiveText);
            _mind.AddObjective(mindId, mind, objective);
        }
    }

    #region Для книги и заклинаний

    public bool IsApprentice(EntityUid mindId)
    {
        return TryComp<ImperialWizardMindComponent>(mindId, out var wizard) && wizard.Apprentice
               || _role.MindHasRole<ImperialWizardApprenticeRoleComponent>(mindId);
    }

    public bool HasPerk(EntityUid user, string perk)
    {
        return _mind.TryGetMind(user, out var mindId, out _)
               && TryComp<ImperialWizardMindComponent>(mindId, out var wizard) && wizard.Perks.Contains(perk);
    }

    public void AddPerk(EntityUid user, string perk)
    {
        if (_mind.TryGetMind(user, out var mindId, out _))
            EnsureComp<ImperialWizardMindComponent>(mindId).Perks.Add(perk);
    }

    /// <summary>log_purchase(): для итогов раунда.</summary>
    public void LogPurchase(EntityUid user, ImperialSpellbookEntryPrototype entry)
    {
        if (!_mind.TryGetMind(user, out var mindId, out _))
            return;

        var wizard = EnsureComp<ImperialWizardMindComponent>(mindId);
        wizard.Purchases[entry.ID] = wizard.Purchases.GetValueOrDefault(entry.ID) + 1;
        Log.Info($"{ToPrettyString(user)} купил в книге заклинаний {entry.ID}");
    }

    /// <summary>get_area(user) == /area/centcom/wizard_station.</summary>
    public bool InDen(EntityUid user)
    {
        return Transform(user).GridUid is { } grid && HasComp<ImperialWizardDenComponent>(grid);
    }

    #endregion

    /// <summary>roundend_report(): Великие ритуалы, цели, покупки книги.</summary>
    protected override void AppendRoundEndText(EntityUid uid, ImperialWizardRuleComponent component, GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        foreach (var mindId in component.Wizards)
        {
            if (!TryComp<MindComponent>(mindId, out var mind) || !TryComp<ImperialWizardMindComponent>(mindId, out var wizard))
                continue;

            var name = mind.CharacterName ?? "?";
            var rituals = TryComp<ImperialGrandRitualMindComponent>(mindId, out var ritual) ? ritual.TimesCompleted : 0;
            args.AddLine(Loc.GetString("imperial-wizard-roundend-name", ("name", name), ("rituals", rituals)));
            if (wizard.Purchases.Count == 0)
            {
                args.AddLine(Loc.GetString("imperial-wizard-roundend-none", ("name", name)));
                continue;
            }

            var purchases = wizard.Purchases.Select(p =>
            {
                var entryName = _proto.TryIndex<ImperialSpellbookEntryPrototype>(p.Key, out var entry) ? Loc.GetString(entry.Name) : p.Key;
                return p.Value > 1 ? $"{p.Value}x {entryName}" : entryName;
            });
            args.AddLine(Loc.GetString("imperial-wizard-roundend-used", ("name", name), ("spells", string.Join(", ", purchases))));
        }
    }
}

/// <summary>Число завершённых Великих ритуалов (заполняется на этапе ритуала).</summary>
[RegisterComponent]
public sealed partial class ImperialGrandRitualMindComponent : Component
{
    [ViewVariables]
    public int TimesCompleted;
}

/// <summary>makewizard [entityUid]: сделать персонажа магом Федерации (снаряжение, книга, цели).</summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class MakeImperialWizardCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "makewizard";
    public string Description => "Делает персонажа магом Федерации волшебников (TG): снаряжение, книга заклинаний, цели.";
    public string Help => $"Использование: {Command} [entityUid]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        EntityUid? target = shell.Player?.AttachedEntity;
        if (args.Length > 0)
        {
            if (!_entManager.TryParseNetEntity(args[0], out var net) || !_entManager.EntityExists(net))
            {
                shell.WriteLine($"Сущность не найдена: «{args[0]}».");
                return;
            }

            target = net.Value;
        }

        if (target == null)
        {
            shell.WriteLine(Help);
            return;
        }

        var ok = _entManager.System<ImperialWizardRuleSystem>().MakeWizard(target.Value);
        shell.WriteLine(ok ? "Готово." : "Не удалось: нет разума или уже маг.");
    }
}
