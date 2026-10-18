using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Imperial.Genetics.Prototypes;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Genetics.Mutations;

/// <summary>Глухота (deaf): носитель не слышит чужую речь.</summary>
[RegisterComponent]
public sealed partial class GeneticDeafComponent : Component;

/// <summary>Неграмотность (illiterate): носитель не может читать и писать.</summary>
[RegisterComponent]
public sealed partial class GeneticIlliterateComponent : Component;

/// <summary>Акромегалия: носитель иногда бьётся головой о притолоку шлюза.</summary>
[RegisterComponent]
public sealed partial class GeneticHeadBonkComponent : Component
{
    [DataField]
    public float Chance = 0.08f;

    [DataField]
    public DamageSpecifier Damage = new() { DamageDict = new() { ["Blunt"] = 5 } };

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/bang.ogg");

    [ViewVariables]
    public Vector2i? LastTile;
}

/// <summary>Пространственная нестабильность (badblink): шанс случайного телепорта растёт со временем.</summary>
[RegisterComponent]
public sealed partial class GeneticBadBlinkComponent : Component
{
    /// <summary>Шанс в процентах за секунду; растёт на 0.0625 каждую секунду, после прыжка сбрасывается.</summary>
    [ViewVariables]
    public float WarpChance;

    [DataField]
    public int MinDistance = 10;

    [DataField]
    public int MaxDistance = 15;

    [ViewVariables]
    public TimeSpan NextCheck;
}

/// <summary>Внутреннее мученичество (martyrdom): при критическом состоянии тело взрывается кровавым душем.</summary>
[RegisterComponent]
public sealed partial class GeneticMartyrdomComponent : Component
{
    [DataField]
    public float Range = 2f;

    [DataField]
    public TimeSpan StunTime = TimeSpan.FromSeconds(2);
}

/// <summary>Рентгеновское зрение (xray): носитель видит сквозь стены.</summary>
[RegisterComponent]
public sealed partial class GeneticXrayComponent : Component;

/// <summary>Пожиратель камней (rock eater): может есть руду.</summary>
[RegisterComponent]
public sealed partial class GeneticRockEaterComponent : Component
{
    [DataField]
    public float Nutrition = 15f;
}

/// <summary>Магнит пустоты (void): способность исчезнуть из реальности и проклятие, иногда срабатывающее само.</summary>
[RegisterComponent]
public sealed partial class GeneticVoidMagnetComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

    [DataField]
    public EntProtoId Hole = "ImperialVoidHole";

    [ViewVariables]
    public TimeSpan NextCheck;
}

/// <summary>Дыра в реальности, в которой носитель магнита пустоты пережидает призыв.</summary>
[RegisterComponent]
public sealed partial class GeneticVoidHoleComponent : Component
{
    public const string ContainerId = "void";

    [ViewVariables]
    public EntityUid? Victim;

    [ViewVariables]
    public TimeSpan EndTime;
}

/// <summary>
/// Мутации SS13, для которых в SS14 нет готовых механик: глухота, неграмотность, акромегалия,
/// пространственная нестабильность, мученичество, рентген, пожиратель камней, магнит пустоты.
/// </summary>
public sealed class GeneticExtraMutationsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedGodmodeSystem _godmode = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private static readonly ProtoId<GeneticMutationPrototype> BadBlinkMutation = "MutationBadBlink";
    private static readonly ProtoId<GeneticMutationPrototype> HeadBonkMutation = "MutationAcromegaly";
    private static readonly ProtoId<GeneticMutationPrototype> VoidMutation = "MutationVoid";
    private static readonly ProtoId<TagPrototype> OreTag = "Ore";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnExpandRecipients);
        SubscribeLocalEvent<GeneticIlliterateComponent, UserOpenActivatableUIAttemptEvent>(OnIlliterateOpenUi);
        SubscribeLocalEvent<GeneticHeadBonkComponent, MoveEvent>(OnHeadBonkMove);
        SubscribeLocalEvent<GeneticMartyrdomComponent, MobStateChangedEvent>(OnMartyrdomState);
        SubscribeLocalEvent<GeneticXrayComponent, ComponentStartup>(OnXrayStartup);
        SubscribeLocalEvent<GeneticXrayComponent, ComponentShutdown>(OnXrayShutdown);
        SubscribeLocalEvent<GetVerbsEvent<AlternativeVerb>>(OnGetRockVerbs);
        SubscribeLocalEvent<GenomeComponent, GeneticVoidActionEvent>(OnVoidAction);
        SubscribeLocalEvent<GeneticVoidHoleComponent, ComponentShutdown>(OnVoidHoleShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var blinks = EntityQueryEnumerator<GeneticBadBlinkComponent>();
        while (blinks.MoveNext(out var uid, out var blink))
        {
            if (now < blink.NextCheck)
                continue;

            blink.NextCheck = now + TimeSpan.FromSeconds(1);
            UpdateBadBlink((uid, blink));
        }

        var magnets = EntityQueryEnumerator<GeneticVoidMagnetComponent>();
        while (magnets.MoveNext(out var uid, out var magnet))
        {
            if (now < magnet.NextCheck)
                continue;

            magnet.NextCheck = now + TimeSpan.FromSeconds(1);
            UpdateVoidCurse((uid, magnet));
        }

        var holes = EntityQueryEnumerator<GeneticVoidHoleComponent>();
        while (holes.MoveNext(out var uid, out var hole))
        {
            if (now < hole.EndTime)
                continue;

            Release((uid, hole));
            QueueDel(uid);
        }
    }

    private ChromosomeKind GetChromosome(EntityUid uid, ProtoId<GeneticMutationPrototype> mutation)
    {
        return TryComp<GenomeComponent>(uid, out var genome) && genome.Active.TryGetValue(mutation, out var active)
            ? active.Chromosome
            : ChromosomeKind.None;
    }

    /// <summary>GET_MUTATION_SYNCHRONIZER: синхронизатор вдвое ослабляет побочные эффекты.</summary>
    private float Synchronizer(EntityUid uid, ProtoId<GeneticMutationPrototype> mutation)
    {
        return (GetChromosome(uid, mutation) & ChromosomeKind.Synchronizer) != 0 ? 0.5f : 1f;
    }

    private float PowerCoeff(EntityUid uid, ProtoId<GeneticMutationPrototype> mutation)
    {
        return (GetChromosome(uid, mutation) & ChromosomeKind.Power) != 0 ? 1.5f : 1f;
    }

    private float EnergyCoeff(EntityUid uid, ProtoId<GeneticMutationPrototype> mutation)
    {
        return (GetChromosome(uid, mutation) & ChromosomeKind.Energy) != 0 ? 0.5f : 1f;
    }

    #region Глухота и неграмотность

    private void OnExpandRecipients(ExpandICChatRecipientsEvent args)
    {
        var deaf = new List<Robust.Shared.Player.ICommonSession>();
        foreach (var (session, data) in args.Recipients)
        {
            if (data.Observer || session.AttachedEntity is not { } listener || listener == args.Source)
                continue;

            if (HasComp<GeneticDeafComponent>(listener))
                deaf.Add(session);
        }

        foreach (var session in deaf)
        {
            args.Recipients.Remove(session);
        }
    }

    private void OnIlliterateOpenUi(Entity<GeneticIlliterateComponent> ent, ref UserOpenActivatableUIAttemptEvent args)
    {
        if (args.Cancelled || !HasComp<PaperComponent>(args.Target))
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("genetics-illiterate-cant-read"), ent, ent);
    }

    #endregion

    #region Акромегалия

    private void OnHeadBonkMove(Entity<GeneticHeadBonkComponent> ent, ref MoveEvent args)
    {
        if (_xform.GetGrid(args.NewPosition) is not { } grid
            || !TryComp<MapGridComponent>(grid, out var gridComp))
        {
            return;
        }

        var tile = _map.TileIndicesFor(grid, gridComp, args.NewPosition);
        if (ent.Comp.LastTile == tile)
            return;

        ent.Comp.LastTile = tile;
        EntityUid? door = null;
        foreach (var anchored in _map.GetAnchoredEntities(grid, gridComp, tile))
        {
            if (HasComp<DoorComponent>(anchored))
            {
                door = anchored;
                break;
            }
        }

        if (door == null || !_random.Prob(ent.Comp.Chance * Synchronizer(ent, HeadBonkMutation)))
            return;

        _damageable.TryChangeDamage(ent.Owner, ent.Comp.Damage * _random.NextFloat(0.4f, 1.8f), origin: door);
        _audio.PlayPvs(ent.Comp.Sound, door.Value, AudioParams.Default.WithVolume(-6f));
        _popup.PopupEntity(Loc.GetString("genetics-acromegaly-bonk", ("door", door.Value)), ent, ent, PopupType.SmallCaution);
    }

    #endregion

    #region Пространственная нестабильность

    private void UpdateBadBlink(Entity<GeneticBadBlinkComponent> ent)
    {
        if (_mobState.IsDead(ent) || !_random.Prob(Math.Clamp(ent.Comp.WarpChance / 100f, 0f, 1f)))
        {
            ent.Comp.WarpChance += 0.0625f / EnergyCoeff(ent, BadBlinkMutation);
            return;
        }

        ent.Comp.WarpChance = 0;
        var distance = _random.Next(ent.Comp.MinDistance, ent.Comp.MaxDistance + 1) * PowerCoeff(ent, BadBlinkMutation);
        if (!TryFindFreeTile(ent, distance, out var target))
            return;

        _popup.PopupEntity(Loc.GetString("genetics-badblink-vanish", ("user", ent.Owner)), ent, Filter.PvsExcept(ent), true, PopupType.MediumCaution);
        _xform.SetCoordinates(ent, target);
        _xform.AttachToGridOrMap(ent);
        _popup.PopupEntity(Loc.GetString("genetics-badblink-self"), ent, ent, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("genetics-badblink-appear", ("user", ent.Owner)), ent, Filter.PvsExcept(ent), true, PopupType.MediumCaution);
    }

    private bool TryFindFreeTile(EntityUid uid, float distance, out EntityCoordinates coordinates)
    {
        coordinates = default;
        var origin = _xform.GetMapCoordinates(uid);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var offset = _random.NextAngle().ToVec() * _random.NextFloat(distance * 0.5f, distance);
            var target = new MapCoordinates(origin.Position + offset, origin.MapId);
            if (!_mapManager.TryFindGridAt(target, out var grid, out var gridComp))
                continue;

            var tile = _map.GetTileRef(grid, gridComp, _map.CoordinatesToTile(grid, gridComp, target));
            if (_turf.IsSpace(tile) || _turf.IsTileBlocked(tile, CollisionGroup.Impassable))
                continue;

            coordinates = _turf.GetTileCenter(tile);
            return true;
        }

        return false;
    }

    #endregion

    #region Мученичество

    private void OnMartyrdomState(Entity<GeneticMartyrdomComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical)
            return;

        var coords = _xform.GetMapCoordinates(ent);
        _popup.PopupEntity(Loc.GetString("genetics-martyrdom-burst", ("user", ent.Owner)), ent, PopupType.LargeCaution);
        _explosion.QueueExplosion(coords, "Default", 8f, 4f, 2f, ent);

        foreach (var splashed in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(coords, ent.Comp.Range))
        {
            if (splashed.Owner == ent.Owner)
                continue;

            _popup.PopupEntity(Loc.GetString("genetics-martyrdom-splashed"), splashed, splashed, PopupType.LargeCaution);
            _stun.TryUpdateParalyzeDuration(splashed, ent.Comp.StunTime);
        }
    }

    #endregion

    #region Рентген

    private void OnXrayStartup(Entity<GeneticXrayComponent> ent, ref ComponentStartup args)
    {
        _eye.SetDrawFov(ent, false);
    }

    private void OnXrayShutdown(Entity<GeneticXrayComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent))
            _eye.SetDrawFov(ent, true);
    }

    #endregion

    #region Пожиратель камней

    private void OnGetRockVerbs(GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract
            || !TryComp<GeneticRockEaterComponent>(args.User, out var eater)
            || !_tag.HasTag(args.Target, OreTag)
            || !HasComp<HungerComponent>(args.User))
        {
            return;
        }

        var user = args.User;
        var target = args.Target;
        var nutrition = eater.Nutrition;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("genetics-rock-eater-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/cutlery.svg.192dpi.png")),
            Act = () => EatRock(user, target, nutrition),
        });
    }

    private void EatRock(EntityUid user, EntityUid rock, float nutrition)
    {
        if (TryComp<StackComponent>(rock, out var stack) && stack.Count > 1)
            _stack.TryUse((rock, stack), 1);
        else
            QueueDel(rock);

        _hunger.ModifyHunger(user, nutrition);
        _audio.PlayPvs(new SoundCollectionSpecifier("eating"), user);
        _popup.PopupEntity(Loc.GetString("genetics-rock-eater-eat", ("user", user)), user, PopupType.Small);
    }

    #endregion

    #region Магнит пустоты

    private void OnVoidAction(Entity<GenomeComponent> ent, ref GeneticVoidActionEvent args)
    {
        if (args.Handled)
            return;

        if (TryVanish(ent))
            args.Handled = true;
    }

    private void UpdateVoidCurse(Entity<GeneticVoidMagnetComponent> ent)
    {
        if (_mobState.IsDead(ent) || _container.IsEntityInContainer(ent))
            return;

        // prob_of_curse = 0.25 + (100 - stability) / 40, умноженный на синхронизатор.
        var chance = 0.25f;
        if (TryComp<GenomeComponent>(ent, out var genome))
            chance += (100 - genome.Stability) / 40f;

        chance *= Synchronizer(ent, VoidMutation);
        if (_random.Prob(Math.Clamp(chance / 100f, 0f, 1f)))
            TryVanish(ent);
    }

    private bool TryVanish(EntityUid uid)
    {
        if (!TryComp<GeneticVoidMagnetComponent>(uid, out var magnet) || _container.IsEntityInContainer(uid))
            return false;

        var hole = Spawn(magnet.Hole, Transform(uid).Coordinates);
        var comp = EnsureComp<GeneticVoidHoleComponent>(hole);
        var container = _container.EnsureContainer<ContainerSlot>(hole, GeneticVoidHoleComponent.ContainerId);
        _popup.PopupEntity(Loc.GetString("genetics-void-vanish", ("user", uid)), uid, PopupType.MediumCaution);

        if (!_container.Insert(uid, container))
        {
            QueueDel(hole);
            return false;
        }

        comp.Victim = uid;
        comp.EndTime = _timing.CurTime + magnet.Duration;
        _godmode.EnableGodmode(uid);
        return true;
    }

    private void OnVoidHoleShutdown(Entity<GeneticVoidHoleComponent> ent, ref ComponentShutdown args)
    {
        Release(ent);
    }

    private void Release(Entity<GeneticVoidHoleComponent> ent)
    {
        if (ent.Comp.Victim is not { } victim || TerminatingOrDeleted(victim))
            return;

        ent.Comp.Victim = null;
        _godmode.DisableGodmode(victim);
        if (_container.TryGetContainer(ent, GeneticVoidHoleComponent.ContainerId, out var container))
            _container.Remove(victim, container, destination: Transform(ent).Coordinates);

        _popup.PopupEntity(Loc.GetString("genetics-void-return", ("user", victim)), victim, PopupType.MediumCaution);
    }

    #endregion
}
