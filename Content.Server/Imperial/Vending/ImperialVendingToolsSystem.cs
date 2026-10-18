using System.Linq;
using System.Reflection;
using Content.Server.Administration;
using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Shared.Body;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Imperial.Vending;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Vending;

/// <summary>
/// Мелкие предметы торгоматов TG: набор ухода за оружием, механический язык, краска для волос,
/// книжка pH, плёнка для камеры и странные семена.
/// </summary>
public sealed class ImperialVendingToolsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly SharedVisualBodySystem _visualBody = default!;
    [Dependency] private readonly MarkingManager _marking = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedChargesSystem _charges = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    private static readonly SoundSpecifier PageSound = new SoundPathSpecifier("/Audio/Effects/poster_broken.ogg");
    private static readonly SoundSpecifier SpraySound = new SoundPathSpecifier("/Audio/Effects/spray2.ogg");

    /// <summary>SeedComponent.Seed закрыт атрибутом Access(BotanySystem).</summary>
    private static readonly FieldInfo SeedField = typeof(SeedComponent).GetField(nameof(SeedComponent.Seed))!;
    private static readonly PropertyInfo NameProp = typeof(SeedData).GetProperty(nameof(SeedData.Name))!;
    private static readonly PropertyInfo DisplayNameProp = typeof(SeedData).GetProperty(nameof(SeedData.DisplayName))!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialGunMaintenanceComponent, AfterInteractEvent>(OnMaintenance);
        SubscribeLocalEvent<ImperialGunMaintenanceComponent, ExaminedEvent>(OnMaintenanceExamined);

        SubscribeLocalEvent<ImperialRobotTongueComponent, OrganGotInsertedEvent>(OnTongueInserted);
        SubscribeLocalEvent<ImperialRobotTongueComponent, OrganGotRemovedEvent>(OnTongueRemoved);

        SubscribeLocalEvent<ImperialHairDyeComponent, UseInHandEvent>(OnDyeUse);
        SubscribeLocalEvent<ImperialHairDyeComponent, AfterInteractEvent>(OnDyeInteract);
        SubscribeLocalEvent<ImperialHairDyeComponent, ImperialHairDyeDoAfterEvent>(OnDyeDoAfter);

        SubscribeLocalEvent<ImperialPhBookletComponent, UseInHandEvent>(OnBookletUse);
        SubscribeLocalEvent<ImperialPhBookletComponent, ExaminedEvent>(OnBookletExamined);
        SubscribeLocalEvent<ImperialPhPaperComponent, AfterInteractEvent>(OnPhPaper);


        SubscribeLocalEvent<ImperialStrangeSeedComponent, MapInitEvent>(OnStrangeSeed);
    }

    #region Набор ухода за оружием

    /// <summary>interact_with_atom(): чинит повреждённое оружие; огнестрел обслуживается всегда.</summary>
    private void OnMaintenance(Entity<ImperialGunMaintenanceComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = true;
        if (!HasComp<GunComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-gun-maintenance-not-gun"), args.User, args.User);
            return;
        }

        var used = false;
        if (TryComp<DamageableComponent>(target, out var damageable) && _damageable.GetTotalDamage((target, damageable)) > 0)
        {
            _damageable.ClearAllDamage((target, damageable));
            used = true;
        }

        if (HasComp<BallisticAmmoProviderComponent>(target) || HasComp<ChamberMagazineAmmoProviderComponent>(target))
            used = true;

        if (!used)
        {
            _popup.PopupEntity(Loc.GetString("imperial-gun-maintenance-no-need"), args.User, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-gun-maintenance-done"), args.User, args.User);
        if (--ent.Comp.Uses <= 0)
            QueueDel(ent);
    }

    private void OnMaintenanceExamined(Entity<ImperialGunMaintenanceComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("imperial-gun-maintenance-uses", ("uses", ent.Comp.Uses), ("max", ent.Comp.MaxUses)));
    }

    #endregion

    #region Механический язык

    private void OnTongueInserted(Entity<ImperialRobotTongueComponent> ent, ref OrganGotInsertedEvent args)
    {
        if (!TryComp<SpeechComponent>(args.Target, out var speech))
            return;

        var speaker = EnsureComp<ImperialRobotTongueSpeakerComponent>(args.Target);
        speaker.OldVerb = speech.SpeechVerb;
        speaker.OldSounds = speech.SpeechSounds;
        speech.SpeechVerb = ent.Comp.SpeechVerb;
        speech.SpeechSounds = ent.Comp.SpeechSounds;
        Dirty(args.Target, speech);
    }

    private void OnTongueRemoved(Entity<ImperialRobotTongueComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (!TryComp<ImperialRobotTongueSpeakerComponent>(args.Target, out var speaker))
            return;

        if (TryComp<SpeechComponent>(args.Target, out var speech))
        {
            speech.SpeechVerb = speaker.OldVerb;
            speech.SpeechSounds = speaker.OldSounds;
            Dirty(args.Target, speech);
        }

        RemComp<ImperialRobotTongueSpeakerComponent>(args.Target);
    }

    #endregion

    #region Краска для волос

    private void OnDyeUse(Entity<ImperialHairDyeComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Facial = !ent.Comp.Facial;
        _popup.PopupEntity(Loc.GetString(ent.Comp.Facial ? "imperial-hair-dye-mode-facial" : "imperial-hair-dye-mode-hair"), args.User, args.User);
    }

    /// <summary>dye(): выбор цвета, затем 3 секунды покраски.</summary>
    private void OnDyeInteract(Entity<ImperialHairDyeComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<VisualBodyComponent>(target))
            return;

        args.Handled = true;
        if (!TryComp<ActorComponent>(args.User, out var actor))
            return;

        var user = args.User;
        _quickDialog.OpenDialog(actor.PlayerSession, Loc.GetString("imperial-hair-dye-title"), Loc.GetString("imperial-hair-dye-prompt"), (string hex) =>
        {
            if (Deleted(ent) || Deleted(target) || Color.TryFromHex(hex.Trim()) is not { } color)
                return;

            ent.Comp.PendingColor = color;
            _popup.PopupEntity(Loc.GetString("imperial-hair-dye-start"), user, user);
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.Delay, new ImperialHairDyeDoAfterEvent(), ent, target, ent)
            {
                BreakOnMove = true,
                NeedHand = true,
            });
        });
    }

    private void OnDyeDoAfter(Entity<ImperialHairDyeComponent> ent, ref ImperialHairDyeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || ent.Comp.PendingColor is not { } color)
            return;

        args.Handled = true;
        var layer = ent.Comp.Facial ? HumanoidVisualLayers.FacialHair : HumanoidVisualLayers.Hair;
        if (!_visualBody.TryGatherMarkingsData(target, null, out _, out _, out var applied) || applied == null)
            return;

        var updated = new Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>();
        foreach (var (category, organMarkings) in applied)
        {
            if (!organMarkings.TryGetValue(layer, out var markings) || markings.Count == 0)
                continue;

            var clone = organMarkings.ToDictionary(kvp => kvp.Key, kvp => new List<Marking>(kvp.Value));
            var marking = clone[layer][0];
            if (_marking.TryGetMarking(marking, out var proto) && marking.MarkingColors.Count != proto.Sprites.Count)
                marking = new Marking(marking.MarkingId, proto.Sprites.Count);

            clone[layer][0] = marking.WithColor(color);
            _marking.EnsureValidColors(clone);
            updated[category] = clone;
        }

        if (updated.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-hair-dye-nothing"), args.User, args.User);
            return;
        }

        _visualBody.ApplyMarkings(target, updated);
        _audio.PlayPvs(SpraySound, target);
        _popup.PopupEntity(Loc.GetString("imperial-hair-dye-done"), args.User, args.User);
    }

    #endregion

    #region Индикатор pH

    /// <summary>attack_hand(): вырвать страницу-полоску.</summary>
    private void OnBookletUse(Entity<ImperialPhBookletComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.Pages <= 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-ph-booklet-empty"), args.User, args.User);
            return;
        }

        ent.Comp.Pages--;
        var strip = Spawn(ent.Comp.Strip, Transform(args.User).Coordinates);
        _hands.PickupOrDrop(args.User, strip);
        _audio.PlayPvs(PageSound, ent);
        _appearance.SetData(ent, ImperialPhBookletVisuals.State, ent.Comp.Pages <= 0 ? "empty" : "open");
    }

    private void OnBookletExamined(Entity<ImperialPhBookletComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("imperial-ph-booklet-pages", ("pages", ent.Comp.Pages)));
    }

    /// <summary>ph_paper/interact_with_atom(): окраска по pH раствора.</summary>
    private void OnPhPaper(Entity<ImperialPhPaperComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !TryComp<SolutionContainerManagerComponent>(target, out var manager))
            return;

        float total = 0, weighted = 0;
        _proto.TryIndex<ImperialReagentPhPrototype>("Default", out var table);
        foreach (var (_, soln) in _solutions.EnumerateSolutions((target, manager)))
        {
            foreach (var reagent in soln.Comp.Solution.Contents)
            {
                var ph = table != null && table.Ph.TryGetValue(reagent.Reagent.Prototype, out var value) ? value : 7f;
                total += reagent.Quantity.Float();
                weighted += reagent.Quantity.Float() * ph;
            }
        }

        if (total <= 0)
            return;

        args.Handled = true;
        if (ent.Comp.Used)
        {
            _popup.PopupEntity(Loc.GetString("imperial-ph-paper-used"), args.User, args.User);
            return;
        }

        var result = (int) Math.Round(weighted / total);
        ent.Comp.Used = true;
        _appearance.SetData(ent, ImperialPhBookletVisuals.State, PhColor(result).ToHex());
        _metaData.SetEntityName(ent, Loc.GetString("imperial-ph-paper-used-name"));
        _metaData.SetEntityDescription(ent, Loc.GetString("imperial-ph-paper-used-desc", ("ph", result)));
        _popup.PopupEntity(Loc.GetString("imperial-ph-paper-result", ("ph", result)), args.User, args.User);
    }

    /// <summary>CONVERT_PH_TO_COLOR TG.</summary>
    private static Color PhColor(int ph)
    {
        return Math.Clamp(ph, 0, 14) switch
        {
            0 => Color.FromHex("#ffa5a1"),
            1 => Color.FromHex("#ffc0bd"),
            2 => Color.FromHex("#ffa585"),
            3 => Color.FromHex("#ffb385"),
            4 => Color.FromHex("#ffcb85"),
            5 => Color.FromHex("#ffe285"),
            6 => Color.FromHex("#e4ff85"),
            7 => Color.FromHex("#a5ff85"),
            8 => Color.FromHex("#85ffb2"),
            9 => Color.FromHex("#85ffe8"),
            10 => Color.FromHex("#85e2ff"),
            11 => Color.FromHex("#85b9ff"),
            12 => Color.FromHex("#8597ff"),
            13 => Color.FromHex("#a385ff"),
            _ => Color.FromHex("#c785ff"),
        };
    }

    #endregion

    #region Странные семена

    /// <summary>seeds/random: случайное растение, мощность, урожай и 1–4 случайных реагента.</summary>
    private void OnStrangeSeed(Entity<ImperialStrangeSeedComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<SeedComponent>(ent, out var seedComp))
            return;

        var seeds = _proto.EnumeratePrototypes<SeedPrototype>().Where(s => s.ProductPrototypes.Count > 0).ToList();
        if (seeds.Count == 0)
            return;

        var seed = _random.Pick(seeds).Clone();
        NameProp.SetValue(seed, Loc.GetString("imperial-strange-seed-name"));
        DisplayNameProp.SetValue(seed, Loc.GetString("imperial-strange-seed-name"));
        seed.Potency = _random.Next(10, 101);
        seed.Yield = _random.Next(1, 6);
        seed.Lifespan = _random.Next(20, 61);

        var count = _random.Next(1, 5);
        for (var i = 0; i < count && ent.Comp.Reagents.Count > 0; i++)
        {
            var reagent = _random.Pick(ent.Comp.Reagents);
            seed.Chemicals[reagent] = new SeedChemQuantity
            {
                Min = 1,
                Max = _random.Next(5, 21),
                PotencyDivisor = _random.Next(5, 21),
            };
        }

        seed.Unique = true;
        SeedField.SetValue(seedComp, seed);
    }

    #endregion
}
