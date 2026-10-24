using System.Linq;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>/obj/item/soulstone/mystic: камень душ мага — пленит душу павшего в тень.</summary>
[RegisterComponent]
public sealed partial class ImperialSoulstoneComponent : Component
{
    [DataField]
    public string Container = "imperial_soulstone";

    /// <summary>required_role: камнем пользуются только маги и их прислужники.</summary>
    [DataField]
    public bool WizardOnly = true;

    [DataField]
    public EntProtoId Shade = "ImperialWizardShade";
}

/// <summary>/mob/living/basic/shade: тень в камне, служит хозяину камня.</summary>
[RegisterComponent]
public sealed partial class ImperialWizardShadeComponent : Component
{
    [ViewVariables]
    public EntityUid? Master;
}

/// <summary>/obj/structure/constructshell: оболочка, в которую вкладывают камень с тенью.</summary>
[RegisterComponent]
public sealed partial class ImperialConstructShellComponent : Component;

/// <summary>
/// Камни душ мага (soulstone.dm): захват души в тень, выпуск тени, превращение в конструкт мага
/// (джаггернаут, призрак, мастеровой).
/// </summary>
public sealed class ImperialWizardSoulstoneSystem : EntitySystem
{
    private static readonly ProtoId<Content.Shared.NPC.Prototypes.NpcFactionPrototype> WizardFactionId = "Wizard";

    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly ImperialListInputSystem _input = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ImperialWizardRuleSystem _wizard = default!;

    /// <summary>GLOB.construct_radial_images: класс → прототип конструкта мага.</summary>
    private static readonly (string Loc, EntProtoId Proto)[] Constructs =
    {
        ("imperial-construct-juggernaut", "ImperialMobConstructJuggernaut"),
        ("imperial-construct-wraith", "ImperialMobConstructWraith"),
        ("imperial-construct-artificer", "ImperialMobConstructArtificer"),
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialSoulstoneComponent, AfterInteractEvent>(OnStoneInteract);
        SubscribeLocalEvent<ImperialSoulstoneComponent, UseInHandEvent>(OnStoneUse);
        SubscribeLocalEvent<ImperialSoulstoneComponent, ExaminedEvent>(OnStoneExamine);
        SubscribeLocalEvent<ImperialSoulstoneComponent, EntityTerminatingEvent>(OnStoneTerminating);
        SubscribeLocalEvent<ImperialConstructShellComponent, ExaminedEvent>(OnShellExamine);
    }

    private bool CanUse(Entity<ImperialSoulstoneComponent> stone, EntityUid user)
    {
        if (!stone.Comp.WizardOnly || HasComp<ImperialMagicallyGiftedComponent>(user))
            return true;

        // role_check(): «вас скручивает изнурительная боль».
        _stun.TryUpdateParalyzeDuration(user, TimeSpan.FromSeconds(10));
        _popup.PopupEntity(Loc.GetString("imperial-soulstone-wracked"), user, user, PopupType.LargeCaution);
        return false;
    }

    private EntityUid? StoneShade(EntityUid stone, ImperialSoulstoneComponent comp)
    {
        return _containers.TryGetContainer(stone, comp.Container, out var container) && container.ContainedEntities.Count > 0
            ? container.ContainedEntities[0]
            : null;
    }

    /// <summary>attack(): тень — в камень; оболочка — конструкт; тело без сознания — захват души.</summary>
    private void OnStoneInteract(Entity<ImperialSoulstoneComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        var user = args.User;
        if (HasComp<ImperialConstructShellComponent>(target))
        {
            args.Handled = true;
            if (CanUse(ent, user))
                TransferToConstruct(ent, target, user);
            return;
        }

        if (HasComp<ImperialWizardShadeComponent>(target))
        {
            args.Handled = true;
            if (CanUse(ent, user))
                CaptureShade(ent, target, user);
            return;
        }

        if (!HasComp<HumanoidProfileComponent>(target) || target == user)
            return;

        args.Handled = true;
        if (CanUse(ent, user))
            CaptureSoul(ent, target, user);
    }

    /// <summary>capture_soul(): только мёртвых или без сознания (grab_sleeping); тело рассыпается в прах.</summary>
    private void CaptureSoul(Entity<ImperialSoulstoneComponent> ent, EntityUid victim, EntityUid user)
    {
        if (StoneShade(ent, ent.Comp) != null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-soulstone-full", ("stone", ent.Owner)), user, user);
            return;
        }

        if (_mobs.IsAlive(victim))
        {
            _popup.PopupEntity(Loc.GetString("imperial-soulstone-kill-first"), user, user, PopupType.MediumCaution);
            return;
        }

        var shade = Spawn(ent.Comp.Shade, Transform(ent).Coordinates);
        _meta.SetEntityName(shade, Loc.GetString("imperial-soulstone-shade-name", ("name", Name(victim))));
        var container = _containers.EnsureContainer<ContainerSlot>(ent, ent.Comp.Container);
        _containers.Insert(shade, container, force: true);
        AssignMaster(shade, user);

        if (_mind.TryGetMind(victim, out var mindId, out var mind) && mind.UserId != null)
        {
            _mind.TransferTo(mindId, shade, mind: mind);
            _popup.PopupEntity(Loc.GetString("imperial-soulstone-captured", ("master", user)), shade, shade, PopupType.Large);
            _popup.PopupEntity(Loc.GetString("imperial-soulstone-success", ("target", victim), ("stone", ent.Owner)), user, user);
        }
        else
        {
            // Душа уже ушла: зовём призрака.
            _popup.PopupEntity(Loc.GetString("imperial-soulstone-fled"), user, user, PopupType.MediumCaution);
            var ghostRole = EnsureComp<GhostRoleComponent>(shade);
            ghostRole.RoleName = Name(shade);
            ghostRole.RoleDescription = Loc.GetString("imperial-soulstone-ghost-role", ("master", Name(user)));
            EnsureComp<GhostTakeoverAvailableComponent>(shade);
        }

        UpdateStone(ent);

        // victim.dust(drop_items = TRUE)
        Spawn("Ash", Transform(victim).Coordinates);
        EntityManager.System<ImperialWizardSpellsSystem>().DropEverything(victim);
        QueueDel(victim);
    }

    private void CaptureShade(Entity<ImperialSoulstoneComponent> ent, EntityUid shade, EntityUid user)
    {
        if (StoneShade(ent, ent.Comp) != null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-soulstone-full", ("stone", ent.Owner)), user, user);
            return;
        }

        var container = _containers.EnsureContainer<ContainerSlot>(ent, ent.Comp.Container);
        _containers.Insert(shade, container, force: true);
        if (user != shade)
            AssignMaster(shade, user);
        _popup.PopupEntity(Loc.GetString("imperial-soulstone-shade-captured", ("stone", ent.Owner)), shade, shade);
        UpdateStone(ent);
    }

    /// <summary>assign_master(): тень служит тому, кто держит камень.</summary>
    private void AssignMaster(EntityUid shade, EntityUid master)
    {
        EnsureComp<ImperialWizardShadeComponent>(shade).Master = master;
        _faction.ClearFactions(shade);
        _faction.AddFaction(shade, WizardFactionId);
        if (_mind.TryGetMind(shade, out _, out _))
            _wizard.MakeMinion(shade, master, Loc.GetString("imperial-soulstone-objective", ("master", Name(master))));
    }

    /// <summary>attack_self(): выпустить тень рядом.</summary>
    private void OnStoneUse(Entity<ImperialSoulstoneComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!CanUse(ent, args.User) || StoneShade(ent, ent.Comp) is not { } shade)
            return;

        _containers.Remove(shade, _containers.GetContainer(ent, ent.Comp.Container), force: true);
        _popup.PopupEntity(Loc.GetString("imperial-soulstone-released", ("shade", shade)), args.User, args.User);
        UpdateStone(ent);
    }

    /// <summary>transfer_to_construct(): выбор класса, тень вселяется в конструкт, камень и оболочка исчезают.</summary>
    private void TransferToConstruct(Entity<ImperialSoulstoneComponent> ent, EntityUid shell, EntityUid user)
    {
        if (StoneShade(ent, ent.Comp) is not { } shade)
        {
            _popup.PopupEntity(Loc.GetString("imperial-soulstone-empty", ("stone", ent.Owner)), user, user);
            return;
        }

        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        var options = Constructs.ToDictionary(c => Loc.GetString(c.Loc), c => c.Proto);
        _input.Open(actor.PlayerSession, Loc.GetString("imperial-construct-title"), Loc.GetString("imperial-construct-prompt"),
            options.Keys.ToList(), choice =>
            {
                if (!options.TryGetValue(choice, out var proto) || TerminatingOrDeleted(shell) || TerminatingOrDeleted(ent)
                    || StoneShade(ent, ent.Comp) != shade)
                    return;

                var construct = Spawn(proto, Transform(shell).Coordinates);
                _faction.ClearFactions(construct);
                _faction.AddFaction(construct, WizardFactionId);
                if (_mind.TryGetMind(shade, out var mindId, out var mind))
                    _mind.TransferTo(mindId, construct, mind: mind);
                else if (TryComp<GhostRoleComponent>(shade, out var role))
                {
                    var newRole = EnsureComp<GhostRoleComponent>(construct);
                    newRole.RoleName = Name(construct);
                    newRole.RoleDescription = role.RoleDescription;
                    EnsureComp<GhostTakeoverAvailableComponent>(construct);
                }

                QueueDel(shade);
                QueueDel(shell);
                QueueDel(ent);
            });
    }

    private void UpdateStone(Entity<ImperialSoulstoneComponent> ent)
    {
        _appearance.SetData(ent, ImperialSoulstoneVisuals.Full, StoneShade(ent, ent.Comp) != null);
    }

    private void OnStoneExamine(Entity<ImperialSoulstoneComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<ImperialMagicallyGiftedComponent>(args.Examiner))
            return;

        args.PushMarkup(Loc.GetString("imperial-soulstone-examine"));
        if (StoneShade(ent, ent.Comp) is { } shade)
            args.PushMarkup(Loc.GetString("imperial-soulstone-examine-shade", ("shade", shade)));
    }

    private void OnStoneTerminating(Entity<ImperialSoulstoneComponent> ent, ref EntityTerminatingEvent args)
    {
        if (StoneShade(ent, ent.Comp) is { } shade)
            _containers.Remove(shade, _containers.GetContainer(ent, ent.Comp.Container), force: true);
    }

    private void OnShellExamine(Entity<ImperialConstructShellComponent> ent, ref ExaminedEvent args)
    {
        if (HasComp<ImperialMagicallyGiftedComponent>(args.Examiner))
            args.PushMarkup(Loc.GetString("imperial-construct-shell-examine"));
    }
}
