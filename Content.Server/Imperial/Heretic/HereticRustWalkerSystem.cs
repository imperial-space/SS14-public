using System.Numerics;
using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticRustWalkerSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem      _damage  = default!;
    [Dependency] private readonly SharedTransformSystem _xform   = default!;
    [Dependency] private readonly SharedAudioSystem     _audio   = default!;
    [Dependency] private readonly PopupSystem           _popup   = default!;
    [Dependency] private readonly EntityLookupSystem    _lookup  = default!;
    [Dependency] private readonly IRobustRandom         _random  = default!;

    private static readonly SoundPathSpecifier WelderSound =
        new SoundPathSpecifier("/Audio/Imperial/heretic/sound_items_tools_welder.ogg");

    private static readonly SoundPathSpecifier HitSound =
        new SoundPathSpecifier("/Audio/Weapons/punch3.ogg");

    private static readonly string[] RuneEffects = {
        "HereticSmallRuneEffect1", "HereticSmallRuneEffect4",
        "HereticSmallRuneEffect7", "HereticSmallRuneEffect10"
    };

    private readonly HashSet<EntityUid> _wallBuffer   = new();
    private readonly HashSet<EntityUid> _structBuffer = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticRustWalkerComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<HereticRustWalkerComponent, HereticRustWalkerAggressiveSpreadActionEvent>(OnAggressiveSpread);
        SubscribeLocalEvent<HereticRustWalkerComponent, HereticRustWalkerLesserPatrinsReachActionEvent>(OnLesserPatrinsReach);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var walkerQuery = EntityQueryEnumerator<HereticRustWalkerComponent>();
        while (walkerQuery.MoveNext(out var uid, out var comp))
        {
            comp.HealAccum += frameTime;
            if (comp.HealAccum >= comp.HealInterval)
            {
                comp.HealAccum = 0f;
                if (_lookup.GetEntitiesInRange<HereticRustOverlayComponent>(_xform.GetMapCoordinates(uid), 0.6f).Count > 0)
                {
                    var heal = new DamageSpecifier();
                    heal.DamageDict["Blunt"] = FixedPoint2.New(-comp.HealAmount);
                    _damage.TryChangeDamage(uid, heal, ignoreResistances: true);
                }
            }
        }

        var projQuery = EntityQueryEnumerator<HereticPatrinsReachComponent>();
        while (projQuery.MoveNext(out var uid, out var proj))
        {
            if (Deleted(proj.CasterUid)) { QueueDel(uid); continue; }

            var currentPos = _xform.GetWorldPosition(uid);
            var dir = proj.TargetWorldPos - currentPos;

            if (dir.Length() < 0.4f || proj.TraveledDistance >= proj.MaxRange)
            {
                OnProjectileArrive(uid, proj);
                continue;
            }

            var moveVec = Vector2.Normalize(dir) * proj.Speed * frameTime;
            proj.TraveledDistance += moveVec.Length();
            var newPos = currentPos + moveVec;
            _xform.SetWorldPosition(uid, newPos);

            var tileX = (int)MathF.Floor(newPos.X);
            var tileY = (int)MathF.Floor(newPos.Y);
            if (proj.LastTile == null || proj.LastTile.Value.X != tileX || proj.LastTile.Value.Y != tileY)
            {
                proj.LastTile = new Vector2i(tileX, tileY);
                var moveDir = Vector2.Normalize(dir);
                var perp = new Vector2(-moveDir.Y, moveDir.X);
                var projMapCoords = _xform.GetMapCoordinates(uid);

                for (var i = -2; i <= 2; i++)
                {
                    if (!_random.Prob(proj.RustChance)) continue;
                    var offset = perp * i;
                    var tileMapPos = new MapCoordinates(projMapCoords.Position + offset, projMapCoords.MapId);
                    SpawnRustOverlayAt(tileMapPos);
                    Spawn(_random.Pick(RuneEffects), tileMapPos);
                    ReplaceWallsNear(tileMapPos);
                    DamageStructuresAt(tileMapPos, 0.6f, 15f);
                }

                _audio.PlayPvs(WelderSound, uid,
                    AudioParams.Default.WithVolume(-8f).WithPitchScale(_random.NextFloat(0.85f, 1.15f)));
            }
        }
    }

    private void OnProjectileArrive(EntityUid uid, HereticPatrinsReachComponent proj)
    {
        var projMapCoords = _xform.GetMapCoordinates(uid);

        var causticDamage = new DamageSpecifier();
        causticDamage.DamageDict["Caustic"] = FixedPoint2.New(25);
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(projMapCoords, 1.5f))
        {
            if (ent.Owner == proj.CasterUid) continue;
            _damage.TryChangeDamage(ent.Owner, causticDamage, ignoreResistances: false);
        }

        _audio.PlayPvs(HitSound, uid);

        for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
            {
                var pos = new MapCoordinates(projMapCoords.Position + new Vector2(dx, dy), projMapCoords.MapId);
                SpawnRustOverlayAt(pos);
                Spawn(_random.Pick(RuneEffects), pos);
                ReplaceWallsNear(pos);
                DamageStructuresAt(pos, 0.6f, 30f);
            }

        QueueDel(uid);
    }

    private void OnAggressiveSpread(EntityUid uid, HereticRustWalkerComponent comp, HereticRustWalkerAggressiveSpreadActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        var mapCoords = _xform.GetMapCoordinates(uid);
        for (var dx = -2; dx <= 2; dx++)
            for (var dy = -2; dy <= 2; dy++)
            {
                if (dx * dx + dy * dy > 4) continue;
                var pos = new MapCoordinates(mapCoords.Position + new Vector2(dx, dy), mapCoords.MapId);
                SpawnRustOverlayAt(pos);
                Spawn(_random.Pick(RuneEffects), pos);
                ReplaceWallsNear(pos);
                DamageStructuresAt(pos, 0.6f, 25f);
            }

        _audio.PlayPvs(WelderSound, uid);
        _popup.PopupEntity(Loc.GetString("heretic-rust-walker-aggressive-spread"), uid, uid, PopupType.Large);
    }

    private void OnLesserPatrinsReach(EntityUid uid, HereticRustWalkerComponent comp, HereticRustWalkerLesserPatrinsReachActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        var targetWorld = _xform.ToMapCoordinates(args.Target).Position;
        var projUid = Spawn("HereticPatrinsReachProjectile", Transform(uid).Coordinates);
        if (!TryComp<HereticPatrinsReachComponent>(projUid, out var projComp)) return;

        projComp.CasterUid = uid;
        projComp.TargetWorldPos = targetWorld;

        _audio.PlayPvs(WelderSound, uid);
        _popup.PopupEntity(Loc.GetString("heretic-rust-walker-lesser-patrins-reach"), uid, uid, PopupType.Medium);
    }

    private void OnMeleeHit(Entity<HereticRustWalkerComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit) return;
        foreach (var target in args.HitEntities)
        {
            var mapCoords = _xform.GetMapCoordinates(target);
            SpawnRustOverlayAt(mapCoords);
            Spawn(_random.Pick(RuneEffects), mapCoords);
            ReplaceWallsNear(mapCoords);
        }
    }

    private void ReplaceWallsNear(MapCoordinates tilePos)
    {
        _wallBuffer.Clear();
        _lookup.GetEntitiesInRange(tilePos.MapId, tilePos.Position, 0.6f, _wallBuffer);
        foreach (var wallUid in _wallBuffer)
        {
            var protoId = MetaData(wallUid).EntityPrototype?.ID;
            if (protoId is not ("WallSolid" or "WallReinforced")) continue;
            var wallCoords = Transform(wallUid).Coordinates;
            QueueDel(wallUid);
            Spawn(protoId == "WallSolid" ? "WallSolidRust" : "WallReinforcedRust", wallCoords);
        }
    }

    private void SpawnRustOverlayAt(MapCoordinates pos)
    {
        if (_lookup.GetEntitiesInRange<HereticRustOverlayComponent>(pos, 0.4f).Count > 0)
            return;
        Spawn("HereticRustOverlay", pos);
    }

    private void DamageStructuresAt(MapCoordinates pos, float radius, float amount)
    {
        _structBuffer.Clear();
        _lookup.GetEntitiesInRange(pos.MapId, pos.Position, radius, _structBuffer);
        var dmg = new DamageSpecifier();
        dmg.DamageDict["Structural"] = FixedPoint2.New(amount);
        foreach (var uid in _structBuffer)
        {
            if (HasComp<MobStateComponent>(uid)) continue;
            _damage.TryChangeDamage(uid, dmg, ignoreResistances: false);
        }
    }
}
