using System.Linq;
using System.Numerics;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Lock;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard.Spells;

#region Компоненты

/// <summary>pointed/mind_transfer: обмен телами с целью рядом, оба теряют сознание на 40 секунд.</summary>
[RegisterComponent]
public sealed partial class ImperialMindSwapSpellComponent : Component
{
    [DataField]
    public TimeSpan Unconscious = TimeSpan.FromSeconds(40);
}

/// <summary>aoe/knock: открыть двери и шкафы вокруг.</summary>
[RegisterComponent]
public sealed partial class ImperialKnockSpellComponent : Component
{
    [DataField]
    public float Radius = 3f;
}

/// <summary>teleport/radius_turf/blink: случайный телепорт на 0–6 клеток.</summary>
[RegisterComponent]
public sealed partial class ImperialBlinkSpellComponent : Component
{
    [DataField]
    public float InnerRadius;

    [DataField]
    public float OuterRadius = 6f;

    [DataField]
    public SoundSpecifier? ArriveSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/blink.ogg");
}

/// <summary>jaunt/ethereal_jaunt: эфирная форма на 5 секунд.</summary>
[RegisterComponent]
public sealed partial class ImperialJauntSpellComponent : Component
{
    [DataField]
    public ProtoId<PolymorphPrototype> Polymorph = "ImperialEtherealJaunt";
}

/// <summary>Эфирная форма запоминает последние свободные клетки, чтобы не выйти внутри стены.</summary>
[RegisterComponent]
public sealed partial class ImperialJauntTrackerComponent : Component
{
    [ViewVariables]
    public EntityCoordinates? LastFree;

    [ViewVariables]
    public EntityCoordinates? Start;

    [ViewVariables]
    public TimeSpan NextCheck;
}

/// <summary>pointed/swap: поменяться местами с целью в 9 клетках.</summary>
[RegisterComponent]
public sealed partial class ImperialSwapSpellComponent : Component
{
    [DataField]
    public float Range = 9f;
}

/// <summary>ghostliness: душа покидает тело и может становиться бестелесной.</summary>
[RegisterComponent]
public sealed partial class ImperialGhostlinessSpellComponent : Component
{
    [DataField]
    public EntProtoId PhaseAction = "ActionImperialSpiritPhase";
}

/// <summary>Дух (/datum/species/spirit/ghost): может сбросить материю и проходить сквозь стены.</summary>
[RegisterComponent]
public sealed partial class ImperialSpiritComponent : Component
{
    [ViewVariables]
    public EntityUid? PhaseAction;
}

#endregion

public sealed partial class ImperialWizardSpellsSystem
{
    [Dependency] private readonly SharedDoorSystem _doors = default!;
    [Dependency] private readonly LockSystem _lock = default!;
    [Dependency] private readonly SharedEntityStorageSystem _storage = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly Content.Shared.StatusEffectNew.StatusEffectsSystem _statusNew = default!;
    [Dependency] private readonly ImperialWizardTeleportSystem _teleport = default!;

    private static readonly SoundSpecifier MindSwapSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/mandswap.ogg");
    private static readonly SoundSpecifier SwapSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/swap.ogg");
    private static readonly ProtoId<PolymorphPrototype> SpiritPhase = "ImperialSpiritPhase";

    private void InitializeMobility()
    {
        SubscribeLocalEvent<ImperialMindSwapSpellComponent, ImperialSpellEffectEvent>(OnMindSwap);
        SubscribeLocalEvent<ImperialKnockSpellComponent, ImperialSpellEffectEvent>(OnKnock);
        SubscribeLocalEvent<ImperialBlinkSpellComponent, ImperialSpellEffectEvent>(OnBlink);
        SubscribeLocalEvent<ImperialJauntSpellComponent, ImperialSpellEffectEvent>(OnJaunt);
        SubscribeLocalEvent<ImperialJauntTrackerComponent, PolymorphedEvent>(OnJauntReverted);
        SubscribeLocalEvent<ImperialSwapSpellComponent, ImperialSpellEffectEvent>(OnSwap);
        SubscribeLocalEvent<ImperialGhostlinessSpellComponent, ImperialSpellEffectEvent>(OnGhostliness);
        SubscribeLocalEvent<ImperialGhostlinessSpellComponent, ImperialSpellChannelDoAfterEvent>(OnGhostlinessDone);
        SubscribeLocalEvent<ImperialSpiritComponent, ImperialSpiritPhaseActionEvent>(OnSpiritPhase);
        SubscribeLocalEvent<PolymorphedEntityComponent, ImperialSpiritPhaseActionEvent>(OnSpiritUnphase);
    }

    #region Обмен разумом

    /// <summary>swap_minds(): разумы меняются телами; антимагия и некоторые антагонисты сопротивляются.</summary>
    private void OnMindSwap(Entity<ImperialMindSwapSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        if (args.Target is not { } target || target == args.Caster || !HasComp<MobStateComponent>(target))
            return;

        var caster = args.Caster;
        if (_mobs.IsDead(target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-mindswap-dead"), caster, caster);
            return;
        }

        if (!_mind.TryGetMind(target, out var targetMind, out var targetMindComp) || targetMindComp.UserId == null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-mindswap-catatonic", ("target", target)), caster, caster);
            return;
        }

        if (!_mind.TryGetMind(caster, out var casterMind, out var casterMindComp))
            return;

        args.Handled = true;
        if (_antimagic.CanBlockMagic(target) || HasComp<ImperialWizardMindComponent>(targetMind) || ResistsMindSwap(targetMind))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-mindswap-resist", ("target", target)), caster, caster);
            return;
        }

        _mind.TransferTo(casterMind, target, mind: casterMindComp);
        _mind.TransferTo(targetMind, caster, mind: targetMindComp);
        _statusNew.TryAddStatusEffectDuration(caster, SleepingSystem.StatusEffectForcedSleeping, ent.Comp.Unconscious);
        _statusNew.TryAddStatusEffectDuration(target, SleepingSystem.StatusEffectForcedSleeping, ent.Comp.Unconscious);
        _audio.PlayGlobal(MindSwapSound, Robust.Shared.Player.Filter.Entities(caster, target), true);
        Log.Info($"{ToPrettyString(target)} и {ToPrettyString(caster)} поменялись разумами (заклинание мага)");
    }

    /// <summary>Культисты, генокрады и революционеры сопротивляются обмену (has_antag_datum в TG).</summary>
    private bool ResistsMindSwap(EntityUid mind)
    {
        if (!TryComp<MindComponent>(mind, out var mindComp))
            return false;

        foreach (var role in mindComp.MindRoleContainer.ContainedEntities)
        {
            var name = MetaData(role).EntityPrototype?.ID ?? string.Empty;
            if (name.Contains("Cult") || name.Contains("Changeling") || name.Contains("Rev"))
                return true;
        }

        return false;
    }

    #endregion

    #region Отпирание

    /// <summary>knock: двери открываются (снимаются болты), шкафы отпираются и распахиваются.</summary>
    private void OnKnock(Entity<ImperialKnockSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        args.Handled = true;
        var caster = args.Caster;
        var coords = Transform(caster).Coordinates;

        // Маг внутри шкафа: шкаф распахивается.
        if (_containers.TryGetContainingContainer(caster, out var container) && TryComp<EntityStorageComponent>(container.Owner, out var holder))
        {
            if (TryComp<LockComponent>(container.Owner, out var holderLock) && holderLock.Locked)
                _lock.Unlock(container.Owner, caster, holderLock);
            _storage.OpenStorage(container.Owner, holder);
            coords = Transform(container.Owner).Coordinates;
        }

        foreach (var door in _lookup.GetEntitiesInRange<DoorComponent>(coords, ent.Comp.Radius))
        {
            if (TryComp<DoorBoltComponent>(door, out var bolts))
                _doors.SetBoltsDown((door, bolts), false);
            _doors.TryOpen(door, door.Comp);
        }

        foreach (var storage in _lookup.GetEntitiesInRange<EntityStorageComponent>(coords, ent.Comp.Radius))
        {
            if (TryComp<LockComponent>(storage, out var lockComp) && lockComp.Locked)
                _lock.Unlock(storage, caster, lockComp);
            _storage.OpenStorage(storage, storage.Comp);
        }
    }

    #endregion

    #region Скачок и обмен местами

    /// <summary>blink: случайная свободная клетка в кольце inner–outer.</summary>
    private void OnBlink(Entity<ImperialBlinkSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        var caster = args.Caster;
        var center = Transform(caster).Coordinates;
        var tiles = FreeTilesAround(center, ent.Comp.OuterRadius, true)
            .Where(t => t.TryDistance(EntityManager, center, out var d) && d >= ent.Comp.InnerRadius)
            .ToList();
        if (tiles.Count == 0)
            return;

        args.Handled = true;
        _transform.SetCoordinates(caster, _random.Pick(tiles));
        _transform.AttachToGridOrMap(caster);
        if (ent.Comp.ArriveSound != null)
            _audio.PlayPvs(ent.Comp.ArriveSound, caster);
    }

    /// <summary>swap: маг и цель меняются местами.</summary>
    private void OnSwap(Entity<ImperialSwapSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        if (args.Target is not { } target || target == args.Caster || !HasComp<MobStateComponent>(target))
            return;

        var caster = args.Caster;
        args.Handled = true;
        if (Blocked(caster, target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-swap-blocked"), target, target);
            return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-spell-swap-victim"), target, target, PopupType.MediumCaution);
        var casterCoords = _transform.GetMapCoordinates(caster);
        var targetCoords = _transform.GetMapCoordinates(target);
        _transform.SetMapCoordinates(target, casterCoords);
        _transform.SetMapCoordinates(caster, targetCoords);
        _audio.PlayPvs(SwapSound, caster);
        _audio.PlayPvs(SwapSound, target);
    }

    #endregion

    #region Эфирное перемещение

    /// <summary>ethereal_jaunt: превращение в бестелесную форму на 5 секунд.</summary>
    private void OnJaunt(Entity<ImperialJauntSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        var start = Transform(args.Caster).Coordinates;
        if (_polymorph.PolymorphEntity(args.Caster, ent.Comp.Polymorph) is not { } jaunt)
            return;

        args.Handled = true;
        var tracker = EnsureComp<ImperialJauntTrackerComponent>(jaunt);
        tracker.Start = start;
        tracker.LastFree = start;
        Spawn("ImperialSteamEffect", start);
    }

    /// <summary>update_exit_point(): последняя свободная клетка на пути.</summary>
    private void UpdateJaunts()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialJauntTrackerComponent, TransformComponent>();
        while (query.MoveNext(out _, out var tracker, out var xform))
        {
            if (now < tracker.NextCheck)
                continue;

            tracker.NextCheck = now + TimeSpan.FromSeconds(0.2);
            if (!IsTileBlocked(xform.Coordinates))
                tracker.LastFree = xform.Coordinates;
        }
    }

    private bool IsTileBlocked(EntityCoordinates coords)
    {
        if (_transform.GetGrid(coords) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var maps = EntityManager.System<SharedMapSystem>();
        return !maps.TryGetTileRef(gridUid, grid, maps.LocalToTile(gridUid, grid, coords), out var tileRef)
               || _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable);
    }

    /// <summary>stop_jaunt(): выйти не внутри стены — на последнюю свободную клетку.</summary>
    private void OnJauntReverted(Entity<ImperialJauntTrackerComponent> ent, ref PolymorphedEvent args)
    {
        if (!args.IsRevert)
            return;

        var exit = Transform(args.NewEntity).Coordinates;
        if (IsTileBlocked(exit))
        {
            var fallback = ent.Comp.LastFree ?? ent.Comp.Start;
            if (fallback is { } free)
            {
                _transform.SetCoordinates(args.NewEntity, free);
                _transform.AttachToGridOrMap(args.NewEntity);
                if (ent.Comp.LastFree == null)
                    _popup.PopupEntity(Loc.GetString("imperial-spell-jaunt-ripped"), args.NewEntity, args.NewEntity);
            }
        }

        Spawn("ImperialSteamEffect", Transform(args.NewEntity).Coordinates);
    }

    #endregion

    #region Отречение от тела

    /// <summary>ghostliness: 5 секунд сосредоточения, затем душа покидает тело.</summary>
    private void OnGhostliness(Entity<ImperialGhostlinessSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        var caster = args.Caster;
        if (!HasComp<HumanoidProfileComponent>(caster) || HasComp<ImperialLichComponent>(caster))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-ghost-no-soul"), caster, caster);
            return;
        }

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString(HasComp<ImperialSpiritComponent>(caster) ? "imperial-spell-ghost-focus-spirit" : "imperial-spell-ghost-focus"), caster, caster);
        _doAfter.TryStartDoAfter(new Content.Shared.DoAfter.DoAfterArgs(EntityManager, caster, TimeSpan.FromSeconds(5), new ImperialSpellChannelDoAfterEvent(), ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        });
    }

    private void OnGhostlinessDone(Entity<ImperialGhostlinessSpellComponent> ent, ref ImperialSpellChannelDoAfterEvent args)
    {
        var caster = args.User;
        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-ghost-broken"), caster, caster);
            return;
        }

        if (!HasComp<ImperialSpiritComponent>(caster))
        {
            // Пустая оболочка остаётся лежать на полу.
            if (MetaData(caster).EntityPrototype is { } proto)
            {
                var husk = Spawn(proto.ID, Transform(caster).Coordinates);
                _meta.SetEntityName(husk, Name(caster));
                _stun.TryUpdateParalyzeDuration(husk, TimeSpan.FromDays(1));
            }

            _popup.PopupEntity(Loc.GetString("imperial-spell-ghost-done"), caster, caster, PopupType.Large);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-ghost-done-spirit"), caster, caster);
        }

        var spirit = EnsureComp<ImperialSpiritComponent>(caster);
        spirit.PhaseAction ??= _actions.AddAction(caster, ent.Comp.PhaseAction);
        _actions.RemoveAction(ent.Owner);
        QueueDel(ent);
    }

    /// <summary>Дух сбрасывает материю (вещи падают) и становится бестелесным.</summary>
    private void OnSpiritPhase(Entity<ImperialSpiritComponent> ent, ref ImperialSpiritPhaseActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _polymorph.PolymorphEntity(ent, SpiritPhase);
    }

    private void OnSpiritUnphase(Entity<PolymorphedEntityComponent> ent, ref ImperialSpiritPhaseActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        _polymorph.Revert(ent.Owner);
    }

    #endregion

    private void UpdateMobility()
    {
        UpdateJaunts();
    }
}
