using System.Linq;
using System.Text.Json;
using Content.Shared.CombatMode;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Подставка для трофейной рыбы: рыбу вешают на стену (3 с), снимают пустой рукой (3 с, трофей прошлых раундов — 6 с
/// и может рассыпаться в прах). Подставки с persistence id хранят трофей между раундами в UserData.
/// </summary>
public sealed class ImperialFishMountSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IResourceManager _resources = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly ImperialFishSystem _fish = default!;
    [Dependency] private readonly ImperialFishingSkillSystem _skill = default!;

    private static readonly ResPath TrophyPath = new("/imperial_trophy_fish.json");

    private static readonly string[] FluffCatchers =
    {
        "Джон Трейзен III", "безымянный стажёр", "Пун-Пун", "Аквариумы Лакса", "Неизвестный", "Центральное Командование",
    };

    private sealed class SavedTrophy
    {
        public string Proto { get; set; } = string.Empty;
        public float Size { get; set; }
        public float Weight { get; set; }
        public string? Name { get; set; }
        public string? Catcher { get; set; }
        public string? Date { get; set; }
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialFishMountComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ImperialFishMountComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ImperialFishMountComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<ImperialFishMountComponent, GetVerbsEvent<AlternativeVerb>>(OnAltVerbs);
        SubscribeLocalEvent<ImperialFishMountComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ImperialFishMountComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<ImperialFishMountComponent, ImperialFishMountAddDoAfterEvent>(OnAddDoAfter);
        SubscribeLocalEvent<ImperialFishMountComponent, ImperialFishMountRemoveDoAfterEvent>(OnRemoveDoAfter);
    }

    private EntityUid? GetMounted(EntityUid mount)
    {
        return _container.TryGetContainer(mount, ImperialFishMountComponent.ContainerId, out var container)
            ? container.ContainedEntities.FirstOrDefault(e => HasComp<ImperialFishComponent>(e)) is { Valid: true } fish ? fish : null
            : null;
    }

    private void OnMapInit(Entity<ImperialFishMountComponent> ent, ref MapInitEvent args)
    {
        _container.EnsureContainer<Container>(ent, ImperialFishMountComponent.ContainerId);
        UpdateVisuals(ent);
        if (ent.Comp.PersistenceId == null || ent.Comp.Loaded)
            return;

        ent.Comp.Loaded = true;
        LoadTrophy(ent);
    }

    #region Установка и снятие

    private void OnInteractUsing(Entity<ImperialFishMountComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<ImperialFishComponent>(args.Used) || _combat.IsInCombatMode(args.User))
            return;

        args.Handled = true;
        if (GetMounted(ent) != null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fish-mount-occupied"), ent, args.User);
            return;
        }

        if (Prototype(args.Used)?.ID.StartsWith("ImperialFishHolo") == true)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fish-mount-not-mountable"), ent, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-fish-mount-mounting"), ent, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(3), new ImperialFishMountAddDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnAddDoAfter(Entity<ImperialFishMountComponent> ent, ref ImperialFishMountAddDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } fish || GetMounted(ent) != null)
            return;

        args.Handled = true;
        AddFish(ent, fish, false, Name(args.User));
        _popup.PopupEntity(Loc.GetString("imperial-fish-mount-mounted"), ent);
        _audio.PlayPvs("/Audio/Machines/click.ogg", ent);
    }

    /// <summary>add_fish().</summary>
    private void AddFish(Entity<ImperialFishMountComponent> ent, EntityUid fish, bool fromPersistence, string catcher)
    {
        if (!TryComp<ImperialFishComponent>(fish, out var comp))
            return;

        var container = _container.EnsureContainer<Container>(ent, ImperialFishMountComponent.ContainerId);
        if (!_container.Insert(fish, container))
            return;

        ent.Comp.StoredFillet = comp.FilletType;
        comp.FilletType = null;
        comp.CatcherName ??= catcher;
        comp.CatchDate ??= DateTime.Now.ToString("dd.MM.yyyy");
        Dirty(fish, comp);

        if (fromPersistence)
        {
            ent.Comp.PersistenceLoaded = true;
            comp.LowPrice = true;
        }
        else if (ent.Comp.PersistenceId != null)
        {
            SaveTrophy(ent);
        }

        UpdateVisuals(ent);
    }

    private void OnInteractHand(Entity<ImperialFishMountComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || GetMounted(ent) == null)
            return;

        args.Handled = true;
        StartRemove(ent, args.User);
    }

    private void OnAltVerbs(Entity<ImperialFishMountComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || GetMounted(ent) == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-fish-mount-verb-remove"),
            Act = () => StartRemove(ent, user),
        });
    }

    private void StartRemove(Entity<ImperialFishMountComponent> ent, EntityUid user)
    {
        _popup.PopupEntity(Loc.GetString("imperial-fish-mount-removing"), ent, user);
        var time = TimeSpan.FromSeconds(ent.Comp.PersistenceLoaded ? 6 : 3);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, time, new ImperialFishMountRemoveDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
        });
    }

    /// <summary>remove_fish().</summary>
    private void OnRemoveDoAfter(Entity<ImperialFishMountComponent> ent, ref ImperialFishMountRemoveDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || GetMounted(ent) is not { } fish)
            return;

        args.Handled = true;
        if (ent.Comp.PersistenceLoaded && !RollSafeRemoval(ent, fish, args.User))
        {
            var name = Name(fish);
            QueueDel(fish);
            Spawn("Ash", Transform(ent).Coordinates);
            _popup.PopupEntity(Loc.GetString("imperial-fish-mount-dust", ("fish", name)), ent, PopupType.SmallCaution);
            ent.Comp.PersistenceLoaded = false;
            UpdateVisuals(ent);
            return;
        }

        _container.TryRemoveFromContainer(fish);
        _hands.TryPickupAnyHand(args.User, fish);
        _popup.PopupEntity(Loc.GetString("imperial-fish-mount-removed"), ent);
    }

    /// <summary>roll_for_safe_removal(): 100 / (размер предмета + ранг веса) % плюс 4% за уровень навыка.</summary>
    private bool RollSafeRemoval(Entity<ImperialFishMountComponent> ent, EntityUid fish, EntityUid? user)
    {
        if (!TryComp<ImperialFishComponent>(fish, out var comp))
            return false;

        var chance = 100f / (ImperialFishSystem.GetWeightClass(comp.Size) + ImperialFishing.GetWeightRank(comp.Weight));
        if (user != null)
            chance += (_skill.GetLevel(user.Value) - 1) * 4;

        if (!_random.Prob(Math.Clamp(chance / 100f, 0, 1)))
            return false;

        ent.Comp.PersistenceLoaded = false;
        return true;
    }

    private void OnRemoved(Entity<ImperialFishMountComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ImperialFishMountComponent.ContainerId || !TryComp<ImperialFishComponent>(args.Entity, out var comp))
            return;

        comp.FilletType ??= ent.Comp.StoredFillet;
        ent.Comp.StoredFillet = null;
        ent.Comp.PersistenceLoaded = false;
        UpdateVisuals(ent);
    }

    private void UpdateVisuals(Entity<ImperialFishMountComponent> ent)
    {
        var fish = GetMounted(ent);
        _appearance.SetData(ent, ImperialFishMountVisuals.Fish, fish != null ? Prototype(fish.Value)?.ID ?? string.Empty : string.Empty);
    }

    /// <summary>TRAIT_EXAMINE_FISH: размер и вес видны всем.</summary>
    private void OnExamined(Entity<ImperialFishMountComponent> ent, ref ExaminedEvent args)
    {
        if (GetMounted(ent) is not { } fish || !TryComp<ImperialFishComponent>(fish, out var comp))
        {
            args.PushMarkup(Loc.GetString("imperial-fish-mount-empty"));
            return;
        }

        args.PushMarkup(Loc.GetString("imperial-fish-mount-examine", ("fish", fish), ("size", comp.Size), ("weight", comp.Weight)));
        if (comp.CatcherName != null)
            args.PushMarkup(Loc.GetString("imperial-fish-mount-catcher", ("catcher", comp.CatcherName), ("date", comp.CatchDate ?? "?")));
    }

    #endregion

    #region Сохранение трофеев

    private void LoadTrophy(Entity<ImperialFishMountComponent> ent)
    {
        var saved = LoadAll().GetValueOrDefault(ent.Comp.PersistenceId!);
        if (saved == null || !_proto.HasIndex<EntityPrototype>(saved.Proto))
        {
            AddFirstFish(ent);
            return;
        }

        var fish = Spawn(saved.Proto, Transform(ent).Coordinates);
        if (!TryComp<ImperialFishComponent>(fish, out var comp))
        {
            QueueDel(fish);
            AddFirstFish(ent);
            return;
        }

        comp.CatcherName = saved.Catcher;
        comp.CatchDate = saved.Date;
        _fish.UpdateSizeAndWeight((fish, comp), saved.Size, saved.Weight);
        _fish.SetDead((fish, comp), true, silent: true);
        if (!string.IsNullOrWhiteSpace(saved.Name))
            _meta.SetEntityName(fish, saved.Name);

        AddFish(ent, fish, true, saved.Catcher ?? FluffCatchers[^2]);
    }

    /// <summary>add_first_fish(): случайный трофей от выдуманного рыбака.</summary>
    private void AddFirstFish(Entity<ImperialFishMountComponent> ent)
    {
        var picks = _proto.EnumeratePrototypes<EntityPrototype>()
            .Where(p => !p.Abstract && p.Components.ContainsKey("ImperialFish")
                        && !p.ID.StartsWith("ImperialFishHolo") && p.ID != "ImperialFishStarfishChrystarfish")
            .ToList();
        if (picks.Count == 0)
            return;

        var proto = _random.Pick(picks);
        var id = proto.TryGetComponent<ImperialFishComponent>(out var data, EntityManager.ComponentFactory) && data.FishIdRedirect is { } redirect
            ? redirect.Id
            : proto.ID;

        var fish = Spawn(id, Transform(ent).Coordinates);
        if (!TryComp<ImperialFishComponent>(fish, out var comp))
        {
            QueueDel(fish);
            return;
        }

        AddFish(ent, fish, true, _random.Pick(FluffCatchers));
        _fish.RandomizeSizeAndWeight((fish, comp));
        _fish.SetDead((fish, comp), true, silent: true);
        SaveTrophy(ent);
    }

    private void SaveTrophy(Entity<ImperialFishMountComponent> ent)
    {
        if (ent.Comp.PersistenceId is not { } id || GetMounted(ent) is not { } fish || !TryComp<ImperialFishComponent>(fish, out var comp))
            return;

        var all = LoadAll();
        all[id] = new SavedTrophy
        {
            Proto = Prototype(fish)?.ID ?? string.Empty,
            Size = comp.Size,
            Weight = comp.Weight,
            Name = Name(fish),
            Catcher = comp.CatcherName,
            Date = comp.CatchDate,
        };

        try
        {
            using var writer = _resources.UserData.OpenWriteText(TrophyPath);
            writer.Write(JsonSerializer.Serialize(all));
        }
        catch (Exception e)
        {
            Log.Error($"Не удалось сохранить трофейных рыб: {e}");
        }
    }

    private Dictionary<string, SavedTrophy> LoadAll()
    {
        try
        {
            if (!_resources.UserData.Exists(TrophyPath))
                return new Dictionary<string, SavedTrophy>();

            using var reader = _resources.UserData.OpenText(TrophyPath);
            return JsonSerializer.Deserialize<Dictionary<string, SavedTrophy>>(reader.ReadToEnd()) ?? new Dictionary<string, SavedTrophy>();
        }
        catch (Exception e)
        {
            Log.Error($"Не удалось прочитать трофейных рыб: {e}");
            return new Dictionary<string, SavedTrophy>();
        }
    }

    #endregion
}
