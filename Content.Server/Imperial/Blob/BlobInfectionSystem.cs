using System.Numerics;
using Content.Server.Body.Components;
using Content.Server.Imperial.Blob.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Radio.Components;
using Content.Shared.Administration.Systems;
using Content.Server.Atmos.Components;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Blob;
using Content.Shared.Imperial.Blob.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Blob;

/// <summary>
/// Заражение спорами: спора ищет человека в критическом состоянии, забирается ему на голову
/// и превращает его в союзника блоба с панцирем.
/// </summary>
public sealed class BlobInfectionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly BlobOvermindSystem _overmind = default!;

    public const string InfectKey = "BlobInfectTarget";
    public const string InfectRangeKey = "BlobInfectRange";
    private static readonly EntProtoId HelmetPrototype = "ClothingHeadHelmetBlob";
    private static readonly SoundSpecifier SplatSound = new SoundPathSpecifier("/Audio/Imperial/blob/splat.ogg");
    private static readonly TimeSpan ThinkInterval = TimeSpan.FromSeconds(0.25);
    private static readonly string[] BlobRadioChannels = { "Blob", "BlobHive" };

    private readonly HashSet<Entity<MobStateComponent>> _nearby = new();
    private readonly List<Entity<BlobSporeLatchComponent>> _spores = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlobSporeLatchComponent, BlobSporeLatchDoAfterEvent>(OnLatchDoAfter);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        _spores.Clear();
        var query = EntityQueryEnumerator<BlobSporeLatchComponent>();
        while (query.MoveNext(out var uid, out var latch))
            _spores.Add((uid, latch));

        foreach (var spore in _spores)
        {
            if (TerminatingOrDeleted(spore) || now < spore.Comp.NextThink)
                continue;
            spore.Comp.NextThink = now + ThinkInterval;
            Think(spore);
        }
    }

    /// <summary>Заражение — главный приоритет ИИ споры.</summary>
    private void Think(Entity<BlobSporeLatchComponent> spore)
    {
        if (!TryComp<MobStateComponent>(spore, out var state) || state.CurrentState != MobState.Alive || HasComp<ActorComponent>(spore) ||
            CompOrNull<BlobMobComponent>(spore)?.Type == BlobMobType.WeakSpore)
            return;

        var target = FindTarget(spore);
        TryComp<HTNComponent>(spore, out var htn);

        if (target == null)
        {
            if (spore.Comp.LatchTarget == null)
                return;
            spore.Comp.LatchTarget = null;
            if (htn != null)
            {
                htn.Blackboard.Remove<EntityCoordinates>(InfectKey);
                if (!spore.Comp.LatchInProgress)
                    _htn.Replan(htn);
            }
            return;
        }

        if (spore.Comp.LatchTarget != target)
        {
            spore.Comp.LatchTarget = target;
            if (htn != null)
            {
                htn.Blackboard.SetValue(InfectKey, new EntityCoordinates(target.Value, 0, 0));
                htn.Blackboard.SetValue(InfectRangeKey, spore.Comp.ApproachRange);
                htn.Blackboard.Remove<EntityUid>("Target");
                _npc.WakeNPC(spore, htn);
                _htn.Replan(htn);
            }
        }

        if (!spore.Comp.LatchInProgress && InRange(spore, target.Value, spore.Comp.InfectionRange))
            TryStartLatch(spore, target.Value);
    }

    private EntityUid? FindTarget(Entity<BlobSporeLatchComponent> spore)
    {
        if (spore.Comp.LatchTarget is { } current && IsValidTarget(current) &&
            InRange(spore, current, spore.Comp.TargetSearchRange))
            return current;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(Transform(spore).Coordinates, spore.Comp.TargetSearchRange, _nearby);

        EntityUid? best = null;
        var bestDist = float.MaxValue;
        var pos = _transform.GetWorldPosition(spore);
        foreach (var candidate in _nearby)
        {
            if (!IsValidTarget(candidate))
                continue;
            var dist = (_transform.GetWorldPosition(candidate) - pos).LengthSquared();
            if (dist >= bestDist)
                continue;
            best = candidate;
            bestDist = dist;
        }

        return best;
    }

    /// <summary>Цель заражения: человек в критическом состоянии, ещё не союзник блоба.</summary>
    public bool IsValidTarget(EntityUid target)
    {
        return !TerminatingOrDeleted(target) &&
               HasComp<HumanoidProfileComponent>(target) &&
               TryComp<MobStateComponent>(target, out var state) && state.CurrentState == MobState.Critical &&
               !HasComp<BlobInfectedComponent>(target) && !_overmind.IsBlobAlly(target);
    }

    private bool InRange(EntityUid a, EntityUid b, float range)
    {
        var pa = _transform.GetMapCoordinates(a);
        var pb = _transform.GetMapCoordinates(b);
        return pa.MapId == pb.MapId && (pa.Position - pb.Position).LengthSquared() <= range * range;
    }

    /// <summary>Спора начинает забираться на голову (5 секунд).</summary>
    public bool TryStartLatch(Entity<BlobSporeLatchComponent> spore, EntityUid target)
    {
        if (spore.Comp.LatchInProgress || !IsValidTarget(target) || CompOrNull<BlobMobComponent>(spore)?.Type == BlobMobType.WeakSpore)
            return false;

        var args = new DoAfterArgs(EntityManager, spore, TimeSpan.FromSeconds(spore.Comp.LatchDuration),
            new BlobSporeLatchDoAfterEvent(), spore, target: target, used: spore)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            RequireCanInteract = false,
            DistanceThreshold = spore.Comp.InfectionRange + 0.5f,
            BlockDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return false;

        if (TryComp<HTNComponent>(spore, out var htn))
            _npc.SleepNPC(spore, htn);
        if (TryComp<PhysicsComponent>(spore, out var physics))
            _physics.SetLinearVelocity(spore, Vector2.Zero, body: physics);

        spore.Comp.LatchInProgress = true;
        spore.Comp.LatchTarget = target;
        _popup.PopupEntity(Loc.GetString("blob-spore-latching", ("spore", spore.Owner), ("target", target)), target, PopupType.MediumCaution);
        return true;
    }

    private void OnLatchDoAfter(Entity<BlobSporeLatchComponent> spore, ref BlobSporeLatchDoAfterEvent args)
    {
        spore.Comp.LatchInProgress = false;
        if (TryComp<HTNComponent>(spore, out var htn))
        {
            _npc.WakeNPC(spore, htn);
            _htn.Replan(htn);
        }

        if (args.Handled || args.Cancelled || args.Target is not { } target || !IsValidTarget(target))
            return;

        args.Handled = true;
        var overmind = CompOrNull<BlobMobComponent>(spore)?.Overmind;
        Infect(target, overmind);
        QueueDel(spore);
    }

    /// <summary>Заражение: панцирь на голову, полное лечение, фракция блоба.</summary>
    public void Infect(EntityUid target, EntityUid? overmind)
    {
        if (HasComp<BlobInfectedComponent>(target))
            return;

        AttachHelmet(target);

        _faction.ClearFactions(target);
        _faction.AddFaction(target, BlobOvermindSystem.BlobFaction);

        var infected = EnsureComp<BlobInfectedComponent>(target);
        infected.Overmind = overmind;

        // Переживает вакуум, как миньоны блоба.
        RemComp<RespiratorComponent>(target);
        EnsureComp<PressureImmunityComponent>(target);

        EnsureRadio(target);
        AllowBlobPassage(target, infected);

        _rejuvenate.PerformRejuvenate(target);
        _audio.PlayPvs(SplatSound, target, AudioParams.Default.WithVolume(BlobStructureSystem.Db(50)).WithVariation(0.125f));
        _popup.PopupEntity(Loc.GetString("blob-infection-started"), target, target, PopupType.LargeCaution);
        _overmind.SendMessage(target, Loc.GetString("blob-infection-greet"));
    }

    private void AttachHelmet(EntityUid target)
    {
        EntityUid? oldHead = null;
        if (_inventory.TryUnequip(target, target, "head", out var removed, silent: true, force: true))
            oldHead = removed;

        var helmet = Spawn(HelmetPrototype, Transform(target).Coordinates);
        if (_inventory.TryEquip(target, target, helmet, "head", silent: true, force: true))
            return;

        QueueDel(helmet);
        if (oldHead != null && Exists(oldHead.Value))
            _inventory.TryEquip(target, target, oldHead.Value, "head", silent: true, force: true);
    }

    private void EnsureRadio(EntityUid uid)
    {
        var transmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(uid);
        var active = EnsureComp<ActiveRadioComponent>(uid);
        EnsureComp<IntrinsicRadioReceiverComponent>(uid);
        foreach (var channel in BlobRadioChannels)
        {
            transmitter.Channels.Add(channel);
            active.Channels.Add(channel);
        }
    }

    /// <summary>PASSBLOB: заражённый проходит сквозь структуры блоба.</summary>
    private void AllowBlobPassage(EntityUid uid, BlobInfectedComponent infected)
    {
        if (!TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            var removed = fixture.CollisionMask & (int) CollisionGroup.BlobImpassable;
            if (!fixture.Hard || removed == 0)
                continue;
            infected.DisabledFixtureMasks[id] = removed;
            _physics.SetCollisionMask(uid, id, fixture, fixture.CollisionMask & ~removed, fixtures);
        }
    }
}
