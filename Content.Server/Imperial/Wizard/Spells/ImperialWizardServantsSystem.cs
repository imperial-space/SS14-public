using System.Linq;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Station;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>/obj/item/antag_spawner/contract: призвать ученика выбранной школы.</summary>
[RegisterComponent]
public sealed partial class ImperialApprenticeContractComponent : Component
{
    [ViewVariables]
    public bool Used;

    [ViewVariables]
    public bool Polling;
}

/// <summary>Тело ученика ждёт призрака: школа и учитель.</summary>
[RegisterComponent]
public sealed partial class ImperialApprenticePendingComponent : Component
{
    [ViewVariables]
    public string School = "destruction";

    [ViewVariables]
    public EntityUid Master;

    [ViewVariables]
    public EntityUid? Contract;
}

/// <summary>/obj/item/highfrequencyblade/wizard: в двух руках — только у мага.</summary>
[RegisterComponent]
public sealed partial class ImperialWizardOnlyWieldComponent : Component;

/// <summary>Мёртвую цель удар может разорвать (gib с шансом force × 0,5 %).</summary>
[RegisterComponent]
public sealed partial class ImperialGibDeadOnHitComponent : Component
{
    [DataField]
    public float Chance = 0.1f;
}

/// <summary>/obj/item/frog_contract: создать древнюю лягушку-стража, запертую в статуэтке.</summary>
[RegisterComponent]
public sealed partial class ImperialFrogContractComponent : Component
{
    [DataField]
    public EntProtoId Frog = "ImperialMobWizardFrog";

    [DataField]
    public EntProtoId Statue = "ImperialFrogStatue";
}

/// <summary>/obj/item/frog_statue: выпустить или отозвать лягушку (раз в 30 секунд).</summary>
[RegisterComponent]
public sealed partial class ImperialFrogStatueComponent : Component
{
    [ViewVariables]
    public EntityUid? Frog;

    [ViewVariables]
    public TimeSpan NextUse;

    [DataField]
    public string Container = "imperial_frog";
}

/// <summary>Ученики, прислужники, стражи мага.</summary>
public sealed class ImperialWizardServantsSystem : EntitySystem
{
    private static readonly ProtoId<Content.Shared.NPC.Prototypes.NpcFactionPrototype> WizardFactionId = "Wizard";

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly Content.Server.NPC.Systems.NPCSystem _npc = default!;
    [Dependency] private readonly ImperialListInputSystem _input = default!;
    [Dependency] private readonly ImperialWizardRuleSystem _wizard = default!;
    [Dependency] private readonly ImperialSpellbookSystem _spellbook = default!;
    [Dependency] private readonly ImperialWizardSpellsSystem _spells = default!;
    [Dependency] private readonly Content.Shared.Gibbing.GibbingSystem _gibbing = default!;
    [Dependency] private readonly Content.Shared.Mobs.Systems.MobStateSystem _mobs = default!;

    /// <summary>ALL_APPRENTICE_TYPES: школа → заклинания и предметы ученика.</summary>
    private static readonly Dictionary<string, (string[] Spells, string[] Items)> Schools = new()
    {
        ["destruction"] = (new[] { "ImperialSpellMagicMissile", "ImperialSpellFireball" }, Array.Empty<string>()),
        ["bluespace"] = (new[] { "ImperialSpellTeleport", "ImperialSpellJaunt" }, Array.Empty<string>()),
        ["healing"] = (new[] { "ImperialSpellCharge", "ImperialSpellForcewall" }, new[] { "ImperialStaffHealing" }),
        ["robeless"] = (new[] { "ImperialSpellKnock", "ImperialSpellMindSwap" }, Array.Empty<string>()),
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialApprenticeContractComponent, UseInHandEvent>(OnContractUse);
        SubscribeLocalEvent<ImperialApprenticePendingComponent, MindAddedMessage>(OnApprenticeMind);
        SubscribeLocalEvent<ImperialWizardOnlyWieldComponent, WieldAttemptEvent>(OnWieldAttempt);
        SubscribeLocalEvent<ImperialGibDeadOnHitComponent, MeleeHitEvent>(OnGibHit);
        SubscribeLocalEvent<ImperialFrogContractComponent, UseInHandEvent>(OnFrogContract);
        SubscribeLocalEvent<ImperialFrogStatueComponent, UseInHandEvent>(OnFrogStatue);
        SubscribeLocalEvent<ImperialFrogStatueComponent, ExaminedEvent>(OnStatueExamine);
    }

    #region Контракт ученика

    /// <summary>ui_act("buy"): выбор школы, затем зов призрака.</summary>
    private void OnContractUse(Entity<ImperialApprenticeContractComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(args.User, out var actor))
            return;

        args.Handled = true;
        if (ent.Comp.Used)
            return;

        if (ent.Comp.Polling)
        {
            _popup.PopupEntity(Loc.GetString("imperial-contract-already-calling"), args.User, args.User);
            return;
        }

        var user = args.User;
        var options = Schools.Keys.ToDictionary(s => Loc.GetString($"imperial-contract-school-{s}"), s => s);
        _input.Open(actor.PlayerSession, Loc.GetString("imperial-contract-title"), Loc.GetString("imperial-contract-prompt"),
            options.Keys.ToList(), choice =>
            {
                if (!options.TryGetValue(choice, out var school) || TerminatingOrDeleted(ent) || ent.Comp.Used || ent.Comp.Polling)
                    return;

                ent.Comp.Polling = true;
                _popup.PopupEntity(Loc.GetString("imperial-contract-contacting"), user, user);
                var body = Spawn("MobHuman", Transform(ent).Coordinates);
                Spawn("WizardSmoke", Transform(ent).Coordinates);
                var pending = EnsureComp<ImperialApprenticePendingComponent>(body);
                pending.School = school;
                pending.Master = user;
                pending.Contract = ent;
                _meta.SetEntityName(body, Loc.GetString("imperial-contract-apprentice-name"));

                var role = EnsureComp<GhostRoleComponent>(body);
                role.RoleName = Loc.GetString("imperial-contract-role-name", ("school", choice));
                role.RoleDescription = Loc.GetString("imperial-contract-role-desc", ("master", Name(user)));
                role.RoleRules = Loc.GetString("imperial-contract-role-rules");
                EnsureComp<GhostTakeoverAvailableComponent>(body);
            });
    }

    /// <summary>spawn_antag(): ученик получает снаряжение, заклинания школы и роль.</summary>
    private void OnApprenticeMind(Entity<ImperialApprenticePendingComponent> ent, ref MindAddedMessage args)
    {
        var body = ent.Owner;
        var school = ent.Comp.School;
        var master = ent.Comp.Master;
        if (ent.Comp.Contract is { } contract && TryComp<ImperialApprenticeContractComponent>(contract, out var contractComp))
        {
            contractComp.Used = true;
            contractComp.Polling = false;
            if (TryComp<ImperialSpellbookRefundableItemComponent>(contract, out var refund))
                refund.Used = true;
        }

        RemComp<ImperialApprenticePendingComponent>(body);
        RemComp<GhostTakeoverAvailableComponent>(body);

        EntityManager.System<Content.Shared.Station.SharedStationSpawningSystem>().EquipStartingGear(body, "ImperialWizardApprenticeGear");
        _wizard.MakeMinion(body, master, Loc.GetString("imperial-contract-objective", ("master", Name(master))));

        if (Schools.TryGetValue(school, out var kit))
        {
            foreach (var spell in kit.Spells)
            {
                _spellbook.GrantSpell(body, spell);
            }

            foreach (var item in kit.Items)
            {
                var spawned = Spawn(item, Transform(body).Coordinates);
                _hands.TryPickupAnyHand(body, spawned);
            }
        }

        _popup.PopupEntity(Loc.GetString("imperial-contract-greet", ("master", Name(master))), body, body, PopupType.Large);
        _popup.PopupEntity(Loc.GetString($"imperial-contract-greet-{school}", ("master", Name(master))), body, body, PopupType.Medium);
    }

    #endregion

    #region Клинок

    private void OnWieldAttempt(Entity<ImperialWizardOnlyWieldComponent> ent, ref WieldAttemptEvent args)
    {
        if (HasComp<ImperialMagicallyGiftedComponent>(args.User))
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("imperial-hfblade-too-weak"), args.User, args.User);
    }

    /// <summary>slash(): мёртвую цель удар может разорвать на куски.</summary>
    private void OnGibHit(Entity<ImperialGibDeadOnHitComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || !TryComp<WieldableComponent>(ent, out var wield) || !wield.Wielded)
            return;

        foreach (var hit in args.HitEntities.ToList())
        {
            if (!HasComp<MobStateComponent>(hit) || !_mobs.IsDead(hit) || !_random.Prob(ent.Comp.Chance))
                continue;

            _popup.PopupEntity(Loc.GetString("imperial-hfblade-gore", ("target", hit)), hit, PopupType.LargeCaution);
            _gibbing.Gib(hit, user: args.User);
        }
    }

    #endregion

    #region Лягушка

    /// <summary>create_frog(): лягушка заперта в статуэтке, контракт исчезает.</summary>
    private void OnFrogContract(Entity<ImperialFrogContractComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var statue = Spawn(ent.Comp.Statue, Transform(args.User).Coordinates);
        var statueComp = EnsureComp<ImperialFrogStatueComponent>(statue);
        var frog = Spawn(ent.Comp.Frog, Transform(args.User).Coordinates);
        _faction.ClearFactions(frog);
        _faction.AddFaction(frog, WizardFactionId);
        statueComp.Frog = frog;
        var container = _containers.EnsureContainer<ContainerSlot>(statue, statueComp.Container);
        _npc.SleepNPC(frog);
        _containers.Insert(frog, container, force: true);
        _popup.PopupEntity(Loc.GetString("imperial-frog-contract-signed"), args.User, args.User);
        QueueDel(ent);
        _hands.TryPickupAnyHand(args.User, statue);
    }

    /// <summary>attack_self(): выпустить лягушку рядом или отозвать её обратно (перезарядка 30 секунд).</summary>
    private void OnFrogStatue(Entity<ImperialFrogStatueComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (_timing.CurTime < ent.Comp.NextUse)
        {
            _popup.PopupEntity(Loc.GetString("imperial-frog-recharging"), args.User, args.User);
            return;
        }

        ent.Comp.NextUse = _timing.CurTime + TimeSpan.FromSeconds(30);
        if (ent.Comp.Frog is not { } frog || TerminatingOrDeleted(frog))
        {
            _popup.PopupEntity(Loc.GetString("imperial-frog-none"), args.User, args.User);
            return;
        }

        var container = _containers.EnsureContainer<ContainerSlot>(ent, ent.Comp.Container);
        if (container.Contains(frog))
        {
            _containers.Remove(frog, container, force: true);
            _npc.WakeNPC(frog);
            var tiles = _spells.FreeTilesAround(Transform(args.User).Coordinates, 2f, true);
            if (tiles.Count > 0)
                _transform.SetCoordinates(frog, _random.Pick(tiles));
            _transform.AttachToGridOrMap(frog);
            return;
        }

        _npc.SleepNPC(frog);
        _containers.Insert(frog, container, force: true);
    }

    private void OnStatueExamine(Entity<ImperialFrogStatueComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<ImperialMagicallyGiftedComponent>(args.Examiner))
            return;

        if (ent.Comp.Frog is not { } frog || TerminatingOrDeleted(frog))
        {
            args.PushMarkup(Loc.GetString("imperial-frog-none"));
            return;
        }

        var inside = _containers.TryGetContainer(ent, ent.Comp.Container, out var container) && container.Contains(frog);
        args.PushMarkup(Loc.GetString(inside ? "imperial-frog-will-release" : "imperial-frog-will-recall"));
    }

    #endregion
}
