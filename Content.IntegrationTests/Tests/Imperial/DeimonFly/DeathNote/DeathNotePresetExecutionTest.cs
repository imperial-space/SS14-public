using System.Linq;
using System.Numerics;
using Content.Server.Access.Systems;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Imperial.DeimonFly.DeathNote.Presets;
using Content.Server.NPC.Components;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Turrets;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Imperial.DeimonFly.DeathNote;

public sealed class DeathNotePresetExecutionTest : DeathNotePresetTestBase
{
    private static readonly ProtoId<DeathNotePresetPrototype> HeartAttackPreset = "DeathNoteHeartAttack";
    private static readonly ProtoId<DeathNotePresetPrototype> PoisonPreset = "DeathNotePoison";
    private static readonly ProtoId<DeathNotePresetPrototype> TurretPreset = "DeathNoteTurret";
    private static readonly ProtoId<NpcFactionPrototype> NanoTrasenFaction = "NanoTrasen";
    private static readonly ProtoId<NpcFactionPrototype> AllHostileFaction = "AllHostile";
    private static readonly ProtoId<AccessLevelPrototype> SecurityAccess = "Security";
    private static readonly ProtoId<DamageTypePrototype> BluntDamage = "Blunt";

    [TestCase("DeathNoteHeartAttack")]
    [TestCase("DeathNoteExplosion")]
    [TestCase("DeathNoteFire")]
    [TestCase("DeathNoteAsphyxiation")]
    [TestCase("DeathNoteElectrocution")]
    [TestCase("DeathNotePoison")]
    [TestCase("DeathNoteMeteor")]
    [TestCase("DeathNoteCarp")]
    [TestCase("DeathNoteSpiders")]
    [TestCase("DeathNoteLightning")]
    [TestCase("DeathNoteTurret")]
    [TestCase("DeathNoteBluntDamage")]
    [TestCase("DeathNoteSlashDamage")]
    [TestCase("DeathNotePiercingDamage")]
    [TestCase("DeathNoteHeatDamage")]
    [TestCase("DeathNoteColdDamage")]
    [TestCase("DeathNoteShockDamage")]
    [TestCase("DeathNoteCausticDamage")]
    [TestCase("DeathNotePoisonDamage")]
    [TestCase("DeathNoteRadiationDamage")]
    [TestCase("DeathNoteCellularDamage")]
    [TestCase("DeathNoteBloodlossDamage")]
    [TestCase("DeathNoteCeilingCollapse")]
    [TestCase("DeathNoteBluespaceAnomaly")]
    public async Task ConfiguredPresetHandlerCanExecute(string presetId)
    {
        await SpawnTarget("DeathNote");

        DeathNotePresetExecutionResult result = default;
        await Server.WaitPost(() =>
        {
            var registry = SEntMan.System<DeathNotePresetRegistrySystem>();
            var preset = Server.ProtoMan.Index<DeathNotePresetPrototype>(presetId);
            var effectTarget = SEntMan.SpawnEntity(
                "MobHuman",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            Assert.That(registry.TryGetHandler(preset.Handler, out var handler), Is.True);
            Assert.That(handler, Is.Not.Null);
            result = handler!.Execute(
                new DeathNotePresetExecutionContext(1, STarget!.Value, null, SPlayer, effectTarget),
                preset.Parameters);
        });

        Assert.That(result.Success, Is.True, result.Error);
        if (presetId != "DeathNoteFire")
            await RunTicks(5);
    }

    [Test]
    public async Task HeartAttackHasConfiguredPreludeAndRandomDamageRange()
    {
        await SpawnTarget("DeathNote");

        await Server.WaitPost(() =>
        {
            var registry = SEntMan.System<DeathNotePresetRegistrySystem>();
            var preset = Server.ProtoMan.Index(HeartAttackPreset);
            Assert.That(registry.TryGetHandler(preset.Handler, out var handler), Is.True);
            Assert.That(handler, Is.AssignableTo<IDeathNotePresetPreludeHandler>());
            Assert.Multiple(() =>
            {
                Assert.That(preset.Parameters.PreludeSound, Is.Not.Null);
                Assert.That(preset.Parameters.PreludeDuration.TotalSeconds, Is.EqualTo(13.035).Within(0.001));
                Assert.That(preset.Parameters.RandomDamageMin, Is.EqualTo(200));
                Assert.That(preset.Parameters.RandomDamageMax, Is.EqualTo(395));
                Assert.That(preset.Parameters.ExecutionAdvance.TotalSeconds, Is.EqualTo(0.6).Within(0.001));
            });
        });
    }

    [Test]
    public async Task PoisonPresetFailsWhenTargetHasNoBloodstream()
    {
        await SpawnTarget("DeathNote");

        DeathNotePresetExecutionResult result = default;
        await Server.WaitPost(() =>
        {
            var registry = SEntMan.System<DeathNotePresetRegistrySystem>();
            var preset = Server.ProtoMan.Index(PoisonPreset);
            var target = SEntMan.SpawnEntity(
                "WallSolid",
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            Assert.That(registry.TryGetHandler(preset.Handler, out var handler), Is.True);
            Assert.That(handler, Is.Not.Null);
            result = handler!.Execute(
                new DeathNotePresetExecutionContext(1, STarget!.Value, null, SPlayer, target),
                preset.Parameters);
        });

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("bloodstream"));
        });
    }

    [Test]
    public async Task HostileFactionRestoresOriginalAndOwnedFactionComponentsOnDeath()
    {
        await SpawnTarget("DeathNote");

        EntityUid originalFactionTarget = default;
        EntityUid ownedFactionTarget = default;
        await Server.WaitPost(() =>
        {
            var coordinates = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            originalFactionTarget = SEntMan.SpawnEntity("MobHuman", coordinates);
            ownedFactionTarget = SEntMan.SpawnEntity("MobHuman", coordinates);

            var factions = SEntMan.System<NpcFactionSystem>();
            var originalFaction = SEntMan.EnsureComponent<NpcFactionMemberComponent>(originalFactionTarget);
            factions.ClearFactions((originalFactionTarget, originalFaction));
            factions.AddFaction((originalFactionTarget, originalFaction), NanoTrasenFaction);
            SEntMan.RemoveComponent<NpcFactionMemberComponent>(ownedFactionTarget);

            var registry = SEntMan.System<DeathNotePresetRegistrySystem>();
            var preset = Server.ProtoMan.Index(TurretPreset);
            Assert.That(registry.TryGetHandler(preset.Handler, out var handler), Is.True);
            Assert.That(handler, Is.Not.Null);
            Assert.That(
                handler!.Execute(
                    new DeathNotePresetExecutionContext(
                        1,
                        STarget!.Value,
                        null,
                        SPlayer,
                        originalFactionTarget),
                    preset.Parameters).Success,
                Is.True);
            Assert.That(
                handler.Execute(
                    new DeathNotePresetExecutionContext(
                        2,
                        STarget.Value,
                        null,
                        SPlayer,
                        ownedFactionTarget),
                    preset.Parameters).Success,
                Is.True);

            Assert.That(originalFaction.Factions, Is.EquivalentTo(new[] { AllHostileFaction }));
            Assert.That(
                SEntMan.GetComponent<NpcFactionMemberComponent>(ownedFactionTarget).Factions,
                Is.EquivalentTo(new[] { AllHostileFaction }));

            var mobState = SEntMan.System<MobStateSystem>();
            mobState.ChangeMobState(originalFactionTarget, MobState.Dead);
            mobState.ChangeMobState(ownedFactionTarget, MobState.Dead);
        });
        await RunTicks(2);

        Assert.Multiple(() =>
        {
            Assert.That(
                SEntMan.GetComponent<NpcFactionMemberComponent>(originalFactionTarget).Factions,
                Is.EquivalentTo(new[] { NanoTrasenFaction }));
            Assert.That(SEntMan.HasComponent<NpcFactionMemberComponent>(ownedFactionTarget), Is.False);
        });
    }

    [Test]
    public async Task TurretDeathUsesCustomTargetingWithoutChangingAccessExemptions()
    {
        await SpawnTarget("DeathNote");

        EntityUid turret = default;
        EntityUid markedTarget = default;
        EntityUid ordinaryTarget = default;
        var markedBefore = true;
        var markedAfter = false;
        var markedAfterCleanup = true;
        var ordinaryAfter = true;
        var factionCandidateBefore = false;
        var factionCandidateAfter = false;
        var explicitHostileAfter = false;
        var explicitHostileAfterCleanup = true;
        var lethalModeApplied = false;
        var targetRegisteredOnTurret = false;
        var targetRemovedFromTurret = false;
        var requiredHits = 0;
        var enhancedDamageTotal = 0f;
        var excessShotDamage = -1f;
        var markedProjectileIgnoresResistance = false;

        await Server.WaitPost(() =>
        {
            var coordinates = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            turret = SEntMan.SpawnEntity("WeaponEnergyTurretSecurity", coordinates);
            markedTarget = SEntMan.SpawnEntity("MobHuman", coordinates);
            ordinaryTarget = SEntMan.SpawnEntity("MobHuman", coordinates);

            var access = SEntMan.System<AccessSystem>();
            access.TrySetTags(
                markedTarget,
                new[] { SecurityAccess },
                SEntMan.EnsureComponent<AccessComponent>(markedTarget));
            access.TrySetTags(
                ordinaryTarget,
                new[] { SecurityAccess },
                SEntMan.EnsureComponent<AccessComponent>(ordinaryTarget));

            var targetSettings = SEntMan.System<TurretTargetSettingsSystem>();
            var turretSettings = SEntMan.GetComponent<TurretTargetSettingsComponent>(turret);
            markedBefore = targetSettings.EntityIsTargetForTurret((turret, turretSettings), markedTarget);
            var factions = SEntMan.System<NpcFactionSystem>();
            factionCandidateBefore = factions.GetNearbyHostiles(turret, 5f).Contains(markedTarget);

            var registry = SEntMan.System<DeathNotePresetRegistrySystem>();
            var preset = Server.ProtoMan.Index(TurretPreset);
            Assert.That(registry.TryGetHandler(preset.Handler, out var handler), Is.True);
            Assert.That(handler, Is.Not.Null);
            Assert.That(
                handler!.Execute(
                    new DeathNotePresetExecutionContext(
                        3,
                        STarget!.Value,
                        null,
                        SPlayer,
                        markedTarget),
                    preset.Parameters).Success,
                Is.True);

            markedAfter = targetSettings.EntityIsTargetForTurret((turret, turretSettings), markedTarget);
            ordinaryAfter = targetSettings.EntityIsTargetForTurret((turret, turretSettings), ordinaryTarget);
            factionCandidateAfter = factions.GetNearbyHostiles(turret, 5f).Contains(markedTarget);
            explicitHostileAfter = factions.GetHostiles(turret).Contains(markedTarget);

            var deathNoteTurret = SEntMan.GetComponent<DeathNoteEnergyTurretComponent>(turret);
            var fireModes = SEntMan.GetComponent<BatteryWeaponFireModesComponent>(turret);
            lethalModeApplied = fireModes.CurrentFireMode == deathNoteTurret.LethalFireMode &&
                                deathNoteTurret.LethalFireMode != deathNoteTurret.OriginalFireMode;
            targetRegisteredOnTurret = deathNoteTurret.Targets.Contains(markedTarget);

            var targetOverride = SEntMan.GetComponent<DeathNoteFactionOverrideComponent>(markedTarget);
            requiredHits = targetOverride.EnergyTurretHitsRemaining;
            for (var i = 0; i < requiredHits; i++)
            {
                var projectileUid = SEntMan.SpawnEntity("BulletEnergyTurretLaser", coordinates);
                var projectileMarker =
                    SEntMan.EnsureComponent<DeathNoteEnergyTurretProjectileComponent>(projectileUid);
                projectileMarker.Target = markedTarget;
                var projectile = SEntMan.GetComponent<ProjectileComponent>(projectileUid);
                var hit = new ProjectileHitEvent(
                    new DamageSpecifier(projectile.Damage),
                    markedTarget,
                    turret);
                SEntMan.EventBus.RaiseLocalEvent(projectileUid, ref hit);
                enhancedDamageTotal += hit.Damage.GetTotal().Float();
                markedProjectileIgnoresResistance |= projectile.IgnoreResistances;
            }

            var excessProjectileUid = SEntMan.SpawnEntity("BulletEnergyTurretLaser", coordinates);
            var excessMarker =
                SEntMan.EnsureComponent<DeathNoteEnergyTurretProjectileComponent>(excessProjectileUid);
            excessMarker.Target = markedTarget;
            var excessProjectile = SEntMan.GetComponent<ProjectileComponent>(excessProjectileUid);
            var excessHit = new ProjectileHitEvent(
                new DamageSpecifier(excessProjectile.Damage),
                markedTarget,
                turret);
            SEntMan.EventBus.RaiseLocalEvent(excessProjectileUid, ref excessHit);
            excessShotDamage = excessHit.Damage.GetTotal().Float();

            SEntMan.RemoveComponent<DeathNoteFactionOverrideComponent>(markedTarget);
            markedAfterCleanup = targetSettings.EntityIsTargetForTurret((turret, turretSettings), markedTarget);
            explicitHostileAfterCleanup = factions.GetHostiles(turret).Contains(markedTarget);
            targetRemovedFromTurret =
                !SEntMan.GetComponent<DeathNoteEnergyTurretComponent>(turret).Targets.Contains(markedTarget);
        });

        Assert.Multiple(() =>
        {
            Assert.That(markedBefore, Is.False, "Security access should ordinarily exempt the target.");
            Assert.That(factionCandidateBefore, Is.True, "The ordinary crew faction should be hostile to the turret.");
            Assert.That(markedAfter, Is.False,
                "The Death Note must not alter the shared energy-turret ID exemption check.");
            Assert.That(factionCandidateAfter, Is.True, "The Death Note target must enter the turret's hostile candidates.");
            Assert.That(explicitHostileAfter, Is.True, "The faction override must not hide the target from the turret.");
            Assert.That(ordinaryAfter, Is.False, "Unmarked players must retain their ordinary ID exemption.");
            Assert.That(lethalModeApplied, Is.True, "The marked turret must automatically enter a damaging fire mode.");
            Assert.That(targetRegisteredOnTurret, Is.True, "The turret override must retain its marked target.");
            Assert.That(requiredHits, Is.InRange(2, 3), "The configured fate must require two or three hits.");
            Assert.That(enhancedDamageTotal, Is.InRange(200f, 395f), "All enhanced hits must stay inside the configured total-damage range.");
            Assert.That(excessShotDamage, Is.Zero, "Later projectiles from the same burst must not over-damage the corpse.");
            Assert.That(markedProjectileIgnoresResistance, Is.True, "Enhanced hits must ignore the target's protection.");
            Assert.That(markedAfterCleanup, Is.False, "The target's ID exemption must return after cleanup.");
            Assert.That(explicitHostileAfterCleanup, Is.False, "Explicit turret hostility must be removed with the effect.");
            Assert.That(targetRemovedFromTurret, Is.True, "The target must be removed from the turret override on cleanup.");
        });
    }

    [Test]
    public async Task TurretDeathActivatesAndFinishesOnlyItsMarkedCriticalTarget()
    {
        await SpawnTarget("DeathNote");

        var playerCoordinates = SEntMan.GetCoordinates(PlayerCoords);
        for (var x = 0; x <= 4; x++)
        {
            await SetTile(
                Plating,
                SEntMan.GetNetCoordinates(playerCoordinates.Offset(new Vector2(x, 0f))),
                MapData.Grid);
        }
        await SetTile(
            Plating,
            SEntMan.GetNetCoordinates(playerCoordinates.Offset(new Vector2(2f, 1f))),
            MapData.Grid);

        EntityUid turret = default;
        EntityUid markedTarget = default;
        EntityUid ordinaryCriticalTarget = default;
        await Server.WaitPost(() =>
        {
            var coordinates = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            turret = SEntMan.SpawnEntity(
                "WeaponEnergyTurretSecurity",
                coordinates.Offset(new Vector2(4f, 0f)));
            markedTarget = SEntMan.SpawnEntity(
                "MobHuman",
                coordinates.Offset(new Vector2(1f, 0f)));
            ordinaryCriticalTarget = SEntMan.SpawnEntity(
                "MobHuman",
                coordinates.Offset(new Vector2(2f, 1f)));

            var access = SEntMan.System<AccessSystem>();
            access.TrySetTags(
                markedTarget,
                new[] { SecurityAccess },
                SEntMan.EnsureComponent<AccessComponent>(markedTarget));
            var targetSettings = SEntMan.System<TurretTargetSettingsSystem>();
            var turretSettings = SEntMan.GetComponent<TurretTargetSettingsComponent>(turret);
            Assert.That(
                targetSettings.EntityIsTargetForTurret((turret, turretSettings), markedTarget),
                Is.False,
                "Security access must still exempt the marked target from ordinary turret targeting.");

            var registry = SEntMan.System<DeathNotePresetRegistrySystem>();
            var preset = Server.ProtoMan.Index(TurretPreset);
            Assert.That(registry.TryGetHandler(preset.Handler, out var handler), Is.True);
            Assert.That(handler, Is.Not.Null);
            Assert.That(
                handler!.Execute(
                    new DeathNotePresetExecutionContext(
                        4,
                        STarget!.Value,
                        null,
                        SPlayer,
                        markedTarget),
                    preset.Parameters).Success,
                Is.True);
            Assert.That(
                targetSettings.EntityIsTargetForTurret((turret, turretSettings), markedTarget),
                Is.False,
                "Starting the fate must not modify the shared turret access check.");

            var damageable = SEntMan.System<DamageableSystem>();
            var criticalDamage = new DamageSpecifier(
                Server.ProtoMan.Index(BluntDamage),
                FixedPoint2.New(100));
            damageable.TryChangeDamage(markedTarget, criticalDamage);
            damageable.TryChangeDamage(ordinaryCriticalTarget, criticalDamage);

            Assert.That(
                SEntMan.GetComponent<DeployableTurretComponent>(turret).Enabled,
                Is.True,
                "The Death Note component must activate an initially retracted energy turret.");
            Assert.That(
                SEntMan.GetComponent<MobStateComponent>(markedTarget).CurrentState,
                Is.EqualTo(MobState.Critical));
            Assert.That(
                SEntMan.GetComponent<MobStateComponent>(ordinaryCriticalTarget).CurrentState,
                Is.EqualTo(MobState.Critical));
        });

        await RunTicks(150);

        await Server.WaitPost(() =>
        {
            var hasRangedCombat = SEntMan.TryGetComponent(
                turret,
                out NPCRangedCombatComponent rangedCombat);
            var deployable = SEntMan.GetComponent<DeployableTurretComponent>(turret);
            Assert.That(
                SEntMan.GetComponent<MobStateComponent>(markedTarget).CurrentState,
                Is.EqualTo(MobState.Dead),
                "The activated turret must keep firing at its marked target through critical state. " +
                $"Ranged combat: {hasRangedCombat}; status: {rangedCombat?.Status}; " +
                $"LOS: {rangedCombat?.TargetInLOS}; target: {rangedCombat?.Target}; " +
                $"enabled: {deployable.Enabled}; state: {deployable.CurrentState}; " +
                $"animation completion: {deployable.AnimationCompletionTime}; server time: {Server.Timing.CurTime}.");
            Assert.That(
                SEntMan.GetComponent<MobStateComponent>(ordinaryCriticalTarget).CurrentState,
                Is.EqualTo(MobState.Critical),
                "The critical-state override must not apply to an unmarked player.");
        });
    }
}
