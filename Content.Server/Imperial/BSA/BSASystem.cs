using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Machines.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared.Imperial.BSA;
using Content.Shared.Machines.Components;
using Content.Shared.Machines.Events;
using Content.Shared.Power;
using Content.Shared.Warps;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.BSA;

public sealed class BSASystem : EntitySystem
{
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MultipartMachineSystem _multipartMachine = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BSAControlBoxComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<BSAControlBoxComponent, MultipartMachineAssemblyStateChanged>(OnAssemblyChanged);
        SubscribeLocalEvent<BSAControlBoxComponent, BSASelectTargetMessage>(OnSelectTarget);
        SubscribeLocalEvent<BSAControlBoxComponent, BSAFireMessage>(OnFire);
        SubscribeLocalEvent<BSAControlBoxComponent, BSAScanMessage>(OnScan);
    }

    // ─────────────────────────── Tick ────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BSAControlBoxComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // t+5s: звук выстрела (после artillery.ogg)
            if (comp.ShotAudioTime != null && now >= comp.ShotAudioTime.Value)
            {
                comp.ShotAudioTime = null;
                _audio.PlayPvs(comp.ShotSound, uid);
            }

            // t+9.5s: вспышка у ствола
            if (comp.MuzzleFlashTime != null && now >= comp.MuzzleFlashTime.Value)
            {
                comp.MuzzleFlashTime = null;
                if (comp.FlashTarget is { } flashTarget && Exists(flashTarget))
                    SpawnShotProjectile(uid, comp, flashTarget);
                comp.FlashTarget = null;
            }

            // Фактический взрыв (t+9s)
            if (comp.PendingFireTarget == null || comp.PendingFireTime == null)
                continue;

            if (now < comp.PendingFireTime.Value)
                continue;

            var target = comp.PendingFireTarget.Value;
            comp.PendingFireTarget = null;
            comp.PendingFireTime = null;
            StopAccumulation(uid, comp);

            if (Exists(target))
                DoExplosion(uid, comp, target);
        }
    }

    // ─────────────────────────── Power ───────────────────────────

    private void OnPowerChanged(Entity<BSAControlBoxComponent> ent, ref PowerChangedEvent args)
    {
        ent.Comp.Powered = args.Powered;
        UpdateUI(ent.Owner, ent.Comp);
    }

    // ─────────────────────────── Assembly ────────────────────────

    private void OnAssemblyChanged(Entity<BSAControlBoxComponent> ent, ref MultipartMachineAssemblyStateChanged args)
    {
        ent.Comp.Assembled = args.IsAssembled;
        Dirty(ent.Owner, ent.Comp);

        // Звук цели/зарядки при сборке
        if (args.IsAssembled)
            _audio.PlayPvs(ent.Comp.GoalSound, ent.Owner);

        UpdateUI(ent.Owner, ent.Comp);
    }

    // ─────────────────────────── UI Messages ─────────────────────

    private void OnSelectTarget(EntityUid uid, BSAControlBoxComponent comp, BSASelectTargetMessage args)
    {
        if (args.Target == null)
        {
            comp.SelectedTarget = null;
        }
        else
        {
            var target = GetEntity(args.Target.Value);
            comp.SelectedTarget = IsValidTargetBeacon(uid, target) ? target : null;

            // Звук выбора маяка
            if (comp.SelectedTarget != null)
                _audio.PlayPvs(comp.GoalSound, uid);
        }

        UpdateUI(uid, comp);
    }

    private void OnFire(EntityUid uid, BSAControlBoxComponent comp, BSAFireMessage _args)
    {
        if (!CanFire(uid, comp))
            return;

        var target = comp.SelectedTarget;
        if (target == null || !IsValidTargetBeacon(uid, target.Value))
        {
            comp.SelectedTarget = null;
            UpdateUI(uid, comp);
            return;
        }

        // Устанавливаем перезарядку сразу
        comp.NextFire = _timing.CurTime + comp.Cooldown;

        // t+5s: звук выстрела (после окончания artillery.ogg)
        // t+9s: фактический взрыв
        // t+9.5s: вспышка у ствола
        const float shotSoundDelay = 5f;
        const float explosionDelay = 9f;

        comp.FlashTarget = target.Value;
        comp.MuzzleFlashTime = _timing.CurTime + TimeSpan.FromSeconds(explosionDelay + 0.5f);
        comp.ShotAudioTime = _timing.CurTime + TimeSpan.FromSeconds(shotSoundDelay);
        comp.PendingFireTarget = target.Value;
        comp.PendingFireTime = _timing.CurTime + TimeSpan.FromSeconds(explosionDelay);

        // Имя маяка для оповещения
        var beaconName = TryComp<WarpPointComponent>(target.Value, out var warp)
            ? (warp.Location ?? MetaData(target.Value).EntityName)
            : MetaData(target.Value).EntityName;

        // Глобальное оповещение
        var announcement = Loc.GetString("bsa-alert-announcement", ("beacon", beaconName));
        _chatManager.DispatchServerAnnouncement(announcement, Color.Red);

        // Звук тревоги на весь сервер
        if (comp.AlertSound != null)
            _audio.PlayGlobal(comp.AlertSound, Filter.Broadcast(), true, AudioParams.Default.WithVolume(5f));

        // Звук нажатия кнопки (click у пушки)
        _audio.PlayPvs(comp.ButtonSound, uid);

        // Звук цели / начала зарядки
        _audio.PlayPvs(comp.GoalSound, uid);

        // Циклический звук накопления (повторяется каждый раз когда заканчивается, -5 dB)
        StopAccumulation(uid, comp);
        var accum = _audio.PlayPvs(comp.AccumulationSound, uid,
            AudioParams.Default.WithVolume(-5f).WithLoop(true).WithMaxDistance(30f));
        if (accum.HasValue)
            comp.AccumulationSoundEntity = accum.Value.Entity;

        // Немедленно спаунить снаряд из дула
        // (вспышка спавнится в Update при MuzzleFlashTime)

        UpdateUI(uid, comp);
    }

    private void OnScan(EntityUid uid, BSAControlBoxComponent comp, BSAScanMessage _)
    {
        _audio.PlayPvs(comp.ButtonSound, uid);

        if (TryComp<MultipartMachineComponent>(uid, out var machine))
            _multipartMachine.Rescan((uid, machine));

        UpdateUI(uid, comp);
    }

    // ─────────────────────────── Fire Logic ──────────────────────

    private bool CanFire(EntityUid uid, BSAControlBoxComponent comp)
    {
        // Нужна сборка
        if (!comp.Assembled)
            return false;

        // Нужно питание
        if (!comp.Powered)
            return false;

        // Перезарядка
        if (comp.NextFire != null && _timing.CurTime < comp.NextFire.Value)
            return false;

        // Нет незавершённого выстрела
        if (comp.PendingFireTarget != null)
            return false;

        return true;
    }

    private bool IsValidTargetBeacon(EntityUid uid, EntityUid target)
    {
        if (!Exists(target) || !HasComp<WarpPointComponent>(target))
            return false;

        return Transform(target).MapID == Transform(uid).MapID;
    }

    /// <summary>
    /// Спаунит снаряд из дула BSA (позиция BSAFrontPart), летящий к цели.
    /// </summary>
    private void SpawnShotProjectile(EntityUid uid, BSAControlBoxComponent comp, EntityUid targetBeacon)
    {
        // Позиция ствола: ищем BSAFrontPart, у которого Master == uid
        MapCoordinates muzzleCoords = default;
        var foundMuzzle = false;

        var frontQuery = EntityQueryEnumerator<BSAFrontPartComponent, MultipartMachinePartComponent, TransformComponent>();
        while (frontQuery.MoveNext(out var frontUid, out _, out var partComp, out var partXform))
        {
            if (partComp.Master != uid)
                continue;

            muzzleCoords = _transform.GetMapCoordinates(frontUid, partXform);
            foundMuzzle = true;
            break;
        }

        if (!foundMuzzle)
            muzzleCoords = _transform.GetMapCoordinates(uid, Transform(uid));

        var targetCoords = _transform.GetMapCoordinates(targetBeacon, Transform(targetBeacon));

        if (muzzleCoords.MapId != targetCoords.MapId)
            return;

        var mapUid = _mapManager.GetMapEntityId(muzzleCoords.MapId);
        if (!mapUid.IsValid())
            return;

        var dir = targetCoords.Position - muzzleCoords.Position;
        if (dir == Vector2.Zero)
            return;

        // Вектор вперёд по оси СТВОЛА: ControlBox → FrontPart (не к цели!)
        var cannonCenter = _transform.GetMapCoordinates(uid, Transform(uid));
        var cannonForward = (muzzleCoords.Position - cannonCenter.Position);
        var forwardDir = cannonForward == Vector2.Zero ? dir.Normalized() : cannonForward.Normalized();

        // Спавним у конца ствола по оси пушки, не по направлению к цели
        var spawnCoords = new EntityCoordinates(mapUid, muzzleCoords.Position + forwardDir * comp.MuzzleOffset);
        var projectile = Spawn(comp.BSAShotProjectile, spawnCoords);

        // Поворот спрайта по оси ствола пушки
        _transform.SetWorldRotation(projectile, forwardDir.ToWorldAngle());

        // Снаряд — статичная вспышка у ствола, не летит
    }

    /// <summary>
    /// Фактический взрыв у цели (вызывается с задержкой из Update).
    /// </summary>
    private void DoExplosion(EntityUid uid, BSAControlBoxComponent comp, EntityUid targetBeacon)
    {
        var targetXform = Transform(targetBeacon);
        var targetCoords = _transform.GetMapCoordinates(targetBeacon, targetXform);

        // Взрыв у цели
        _explosion.QueueExplosion(
            targetCoords,
            comp.ExplosionType,
            comp.ExplosionTotalIntensity,
            comp.ExplosionSlope,
            comp.ExplosionMaxTileIntensity,
            uid,
            addLog: true);

        // Визуальный эффект попадания
        var effectParent = targetXform.GridUid ?? targetXform.MapUid;
        if (effectParent != null && !TerminatingOrDeleted(effectParent.Value))
        {
            var onGridCoords = new EntityCoordinates(effectParent.Value, targetXform.LocalPosition);
            Spawn(comp.ImpactEffect, onGridCoords);

            // Звук последствий: слышен в радиусе 25 тайлов (50×50) у цели (-5 dB)
            _audio.PlayPvs(comp.ConsequencesSound, onGridCoords,
                AudioParams.Default.WithVolume(-5f).WithMaxDistance(25f));
        }

        UpdateUI(uid, comp);
    }

    /// <summary>Останавливает петлю звука накопления.</summary>
    private void StopAccumulation(EntityUid uid, BSAControlBoxComponent comp)
    {
        if (comp.AccumulationSoundEntity is { } soundEnt && Exists(soundEnt))
            QueueDel(soundEnt);
        comp.AccumulationSoundEntity = null;
    }

    // ─────────────────────────── UI Update ───────────────────────

    public void UpdateUI(EntityUid uid, BSAControlBoxComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        TryComp<MultipartMachineComponent>(uid, out var machine);

        var assembled = comp.Assembled;
        var frontExists = _multipartMachine.HasPart((uid, machine), BSAParts.Front);
        var backExists = _multipartMachine.HasPart((uid, machine), BSAParts.Back);

        var now = _timing.CurTime;
        var canFire = assembled
            && comp.Powered
            && (comp.NextFire == null || now >= comp.NextFire.Value)
            && comp.SelectedTarget != null
            && Exists(comp.SelectedTarget.Value);

        // Собираем список маяков (WarpPoint) на той же карте
        var beacons = new List<(NetEntity, string)>();
        var myMap = Transform(uid).MapID;

        var warpQuery = EntityQueryEnumerator<WarpPointComponent, TransformComponent>();
        while (warpQuery.MoveNext(out var wUid, out var warp, out var wXform))
        {
            if (wXform.MapID != myMap)
                continue;

            var name = warp.Location ?? MetaData(wUid).EntityName;
            beacons.Add((GetNetEntity(wUid), name));
        }

        // Удаляем из выбора несуществующие или сменившие карту маяки
        NetEntity? netSelected = null;
        if (comp.SelectedTarget.HasValue && Exists(comp.SelectedTarget.Value))
            netSelected = GetNetEntity(comp.SelectedTarget.Value);

        var state = new BSAUIState(
            assembled,
            comp.Powered,
            frontExists,
            backExists,
            canFire,
            comp.NextFire,
            netSelected,
            beacons
        );

        _ui.SetUiState(uid, BSAControlBoxUiKey.Key, state);
    }
}
