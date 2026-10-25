using Robust.Shared.Random;
using System.Linq;
using System.Numerics;
using Content.Server.Body;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Temperature.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Drunk;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Gravity;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Quirks;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Sprite;
using Content.Shared.StepTrigger.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Body.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server.Imperial.Quirks;

#region Компоненты

/// <summary>TRAIT_EVIL.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkEvilComponent : Component;

/// <summary>TRAIT_SHIFTY_EYES: осматривающему кажется, что на него смотрят.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkShiftyEyesComponent : Component;

/// <summary>/datum/quirk/death_dnr_poll: тело без души после оживления достаётся призраку.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkThanatorenasiaComponent : Component;

/// <summary>/datum/quirk/drunkhealing.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkDrunkHealingComponent : Component
{
    public TimeSpan NextCheck;
}

/// <summary>/datum/component/empathy.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkEmpathComponent : Component
{
    public bool SeenEvil;
}

/// <summary>TRAIT_KEEN_NOSE: запах открытой ёмкости в руке выдаёт содержимое.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkKeenNoseComponent : Component;

/// <summary>TRAIT_LIGHT_STEP: острое под ногами ранит слабее.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkLightStepComponent : Component;

/// <summary>TRAIT_SELF_AWARE: точно оценивает свои раны.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkSelfAwareComponent : Component;

/// <summary>/datum/quirk/settler: невысокий, медленнее, крепкий.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkSettlerComponent : Component;

/// <summary>TRAIT_SKITTISH: с разбега запрыгивает в закрытый шкаф.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkSkittishComponent : Component
{
    public TimeSpan NextDive;
}

public enum ImperialSpacerState : byte
{
    None,
    Planet,
    PlanetTooLong,
    Space,
    SpaceComfortable,
    NoGravity,
}

/// <summary>/datum/quirk/spacer_born: высокий, хорош в невесомости, плохо переносит планеты.</summary>
[RegisterComponent]
public sealed partial class ImperialQuirkSpacerComponent : Component
{
    public ImperialSpacerState State;
    public TimeSpan StateSince;
    public TimeSpan NextCheck;
}

#endregion

public sealed partial class ImperialQuirkSystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly Content.Shared.Paper.PaperSystem _paper = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedImperialQuirkSystem _quirkShared = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedScaleVisualsSystem _scale = default!;
    [Dependency] private readonly SharedEntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly VisualBodySystem _visualBody = default!;

    private void InitializeNeutral()
    {
        SubscribeLocalEvent<BloodstreamComponent, ExaminedEvent>(OnLivingExamined);
        SubscribeLocalEvent<ImperialQuirkThanatorenasiaComponent, MobStateChangedEvent>(OnThanatorenasiaState);
    }

    private void InitializePositive()
    {
        SubscribeLocalEvent<SolutionContainerManagerComponent, ExaminedEvent>(OnContainerSniffed);
        SubscribeLocalEvent<ImperialQuirkLightStepComponent, DamageModifyEvent>(OnLightStepDamage);
        SubscribeLocalEvent<ImperialQuirkSkittishComponent, StartCollideEvent>(OnSkittishCollide);
        SubscribeLocalEvent<ImperialQuirkSpacerComponent, DamageModifyEvent>(OnSpacerDamage);
    }

    /// <summary>Одноразовые эффекты по id причуды (add_unique, post_add).</summary>
    private void ApplyOther(EntityUid mob, string quirk, Robust.Shared.Player.ICommonSession? player)
    {
        switch (quirk)
        {
            case "ImperialQuirkBald":
                if (_visualBody.TryGatherMarkingsData(mob, null, out _, out _, out var applied))
                {
                    foreach (var layers in applied.Values)
                    {
                        if (layers.TryGetValue(HumanoidVisualLayers.Hair, out var list))
                            list.Clear();
                    }

                    _visualBody.ApplyMarkings(mob, applied);
                }

                break;
            case "ImperialQuirkEvil":
                Message(player, Loc.GetString("imperial-quirk-evil-policy"));
                break;
            case "ImperialQuirkInsanity":
                Message(player, Loc.GetString("imperial-quirk-insanity-policy"));
                break;
            case "ImperialQuirkMonochromacy":
                if (HasDepartmentJob(mob, "Detective"))
                    Message(player, Loc.GetString("imperial-quirk-monochromacy-detective"));
                break;
            case "ImperialQuirkSettler":
                _scale.SetSpriteScale(mob, _scale.GetSpriteScale(mob) * new Vector2(1f, 0.85f));
                RecalcSpeed(mob);
                break;
            case "ImperialQuirkSpacer":
                _scale.SetSpriteScale(mob, _scale.GetSpriteScale(mob) * new Vector2(1f, 1.15f));
                break;
        }
    }

    private bool HasDepartmentJob(EntityUid mob, string job) => HasJob(mob, job);

    /// <summary>Множители скорости от всех причуд разом.</summary>
    private void RecalcSpeed(EntityUid mob)
    {
        var speed = 1f;
        if (HasComp<ImperialQuirkHemiplegicComponent>(mob))
            speed *= 0.6f;
        if (HasComp<ImperialQuirkSettlerComponent>(mob))
            speed *= 0.9f;
        if (TryComp<ImperialQuirkSpacerComponent>(mob, out var spacer))
        {
            speed *= spacer.State switch
            {
                ImperialSpacerState.Planet => 0.9f,
                ImperialSpacerState.PlanetTooLong => 0.75f,
                ImperialSpacerState.SpaceComfortable or ImperialSpacerState.NoGravity => 1.1f,
                _ => 1f,
            };
        }

        var comp = EnsureComp<ImperialQuirkSpeedComponent>(mob);
        _quirkShared.SetSpeed((mob, comp), speed, speed);
    }

    #region Осмотр

    private void OnLivingExamined(Entity<BloodstreamComponent> ent, ref ExaminedEvent args)
    {
        var target = ent.Owner;
        var examiner = args.Examiner;

        if (examiner == target)
        {
            if (HasComp<ImperialQuirkSelfAwareComponent>(target))
                SelfAwareExamine(target, args);
            return;
        }

        if (HasComp<ImperialQuirkEmpathComponent>(examiner))
            EmpathExamine(examiner, target, args);

        // handle_eye_contact: осмотренный тревожный нервничает, «бегающие глаза» мерещатся осматривающему.
        if (HasComp<ImperialQuirkSocialAnxietyComponent>(target) && TryComp<MindContainerComponent>(examiner, out var mind) && mind.HasMind)
            AnxietyEyeContact(target, examiner, false);

        if (HasComp<ImperialQuirkSocialAnxietyComponent>(examiner) && TryComp<MindContainerComponent>(target, out var targetMind) && targetMind.HasMind)
            AnxietyEyeContact(examiner, target, true);

        if (HasComp<ImperialQuirkShiftyEyesComponent>(target)
            && Transform(target).Coordinates.TryDistance(EntityManager, Transform(examiner).Coordinates, out var distance)
            && _random.Prob(Math.Max(0f, 10f - distance) / 100f))
        {
            _popup.PopupEntity(Loc.GetString("imperial-quirk-shifty-eyes", ("target", target)), examiner, examiner);
        }
    }

    /// <summary>get_empath_info.</summary>
    private void EmpathExamine(EntityUid empath, EntityUid target, ExaminedEvent args)
    {
        if (!HasComp<HumanoidProfileComponent>(target) || _mobState.IsDead(target))
            return;

        using (args.PushGroup(nameof(ImperialQuirkEmpathComponent)))
        {
            if (TryComp<CombatModeComponent>(target, out var combat) && combat.IsInCombatMode)
                args.PushMarkup(Loc.GetString("imperial-quirk-empath-guard", ("target", target)));

            if (TryComp<DamageableComponent>(target, out var damageable))
            {
                var damage = _damageable.GetAllDamage((target, damageable));
                if (damage.DamageDict.GetValueOrDefault("Asphyxiation").Float() >= 10)
                    args.PushMarkup(Loc.GetString("imperial-quirk-empath-winded", ("target", target)));
                if (damage.DamageDict.GetValueOrDefault("Poison").Float() >= 10)
                    args.PushMarkup(Loc.GetString("imperial-quirk-empath-sickly", ("target", target)));
            }

            if (TryComp<BlindableComponent>(target, out var blind) && blind.IsBlind)
                args.PushMarkup(Loc.GetString("imperial-quirk-empath-blind", ("target", target)));

            if (HasComp<ImperialQuirkDeafComponent>(target))
                args.PushMarkup(Loc.GetString("imperial-quirk-empath-deaf", ("target", target)));

            if (TryComp<TemperatureComponent>(target, out var temperature) && TryComp<TemperatureDamageComponent>(target, out var limits))
            {
                if (temperature.CurrentTemperature > limits.HeatDamageThreshold)
                    args.PushMarkup(Loc.GetString("imperial-quirk-empath-hot", ("target", target)));
                else if (temperature.CurrentTemperature < limits.ColdDamageThreshold)
                    args.PushMarkup(Loc.GetString("imperial-quirk-empath-cold", ("target", target)));
            }

            if (!HasComp<ImperialQuirkEvilComponent>(target))
                return;

            args.PushMarkup(Loc.GetString("imperial-quirk-empath-evil", ("target", target)));
            if (TryComp<ImperialQuirkEmpathComponent>(empath, out var comp) && !comp.SeenEvil)
            {
                comp.SeenEvil = true;
                _jitter.DoJitter(empath, TimeSpan.FromSeconds(15), true);
            }
        }
    }

    /// <summary>TRAIT_SELF_AWARE: точные цифры повреждений вместо «выглядит ранено».</summary>
    private void SelfAwareExamine(EntityUid mob, ExaminedEvent args)
    {
        if (!TryComp<DamageableComponent>(mob, out var damageable))
            return;

        var damage = _damageable.GetAllDamage((mob, damageable));
        var lines = damage.DamageDict.Where(d => d.Value > 0.5)
            .Select(d => Loc.GetString("imperial-quirk-self-aware-line",
                ("type", _proto.TryIndex<Content.Shared.Damage.Prototypes.DamageTypePrototype>(d.Key, out var type) ? type.LocalizedName : d.Key),
                ("amount", d.Value.Float().ToString("0.#", System.Globalization.CultureInfo.InvariantCulture))))
            .ToList();

        using (args.PushGroup(nameof(ImperialQuirkSelfAwareComponent)))
        {
            args.PushMarkup(lines.Count == 0
                ? Loc.GetString("imperial-quirk-self-aware-healthy")
                : Loc.GetString("imperial-quirk-self-aware", ("lines", string.Join("\n", lines))));
        }
    }

    /// <summary>TRAIT_KEEN_NOSE: открытая ёмкость в руке пахнет содержимым.</summary>
    private void OnContainerSniffed(Entity<SolutionContainerManagerComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<ImperialQuirkKeenNoseComponent>(args.Examiner)
            || !_container.TryGetContainingContainer(ent.Owner, out var container)
            || container.Owner != args.Examiner
            || TryComp<OpenableComponent>(ent, out var openable) && !openable.Opened)
        {
            return;
        }

        var reagents = new HashSet<string>();
        foreach (var (_, solution) in _solutions.EnumerateSolutions(ent.Owner))
        {
            foreach (var (reagent, quantity) in solution.Comp.Solution.Contents)
            {
                if (quantity > 0 && _proto.TryIndex<Content.Shared.Chemistry.Reagent.ReagentPrototype>(reagent.Prototype, out var proto))
                    reagents.Add(proto.LocalizedName);
            }
        }

        if (reagents.Count == 0)
            return;

        args.PushMarkup(Loc.GetString("imperial-quirk-keen-nose", ("reagents", string.Join(", ", reagents))));
    }

    #endregion

    #region Танаторенозия

    /// <summary>ghostrole_on_revive: ожившее тело без души становится ролью для призраков.</summary>
    private void OnThanatorenasiaState(Entity<ImperialQuirkThanatorenasiaComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.OldMobState != MobState.Dead || args.NewMobState == MobState.Dead)
            return;

        if (TryComp<MindContainerComponent>(ent, out var mind) && mind.HasMind || HasComp<GhostRoleComponent>(ent))
            return;

        var role = EnsureComp<GhostRoleComponent>(ent);
        role.RoleName = Loc.GetString("imperial-quirk-thanatorenasia-role", ("name", Name(ent)));
        role.RoleDescription = Loc.GetString("imperial-quirk-thanatorenasia-role-desc");
        role.RoleRules = Loc.GetString("imperial-quirk-thanatorenasia-rules");
        EnsureComp<GhostTakeoverAvailableComponent>(ent);
    }

    #endregion

    #region Положительные

    /// <summary>drunkhealing: чем пьянее, тем быстрее заживают раны и ожоги.</summary>
    private void UpdateDrunkHealing(Entity<ImperialQuirkDrunkHealingComponent> ent, TimeSpan now)
    {
        if (now < ent.Comp.NextCheck || !Alive(ent))
            return;

        ent.Comp.NextCheck = now + Second;
        if (!HasStatus(ent, SharedDrunkSystem.Drunk) || !_status.TryGetTime(ent, SharedDrunkSystem.Drunk, out var time) || time.EndEffectTime is not { } end)
            return;

        var drunk = (end - now).TotalSeconds;
        var (brute, burn) = drunk switch
        {
            >= 240 => (0.8f, 0.4f),
            >= 120 => (0.4f, 0.2f),
            >= 20 => (0.1f, 0.05f),
            _ => (0f, 0f),
        };

        if (brute <= 0)
            return;

        var heal = new DamageSpecifier();
        heal.DamageDict["Blunt"] = -brute;
        heal.DamageDict["Slash"] = -brute;
        heal.DamageDict["Piercing"] = -brute;
        heal.DamageDict["Heat"] = -burn;
        _damageable.TryChangeDamage(ent.Owner, heal, true, false);
    }

    private void OnLightStepDamage(EntityUid uid, ImperialQuirkLightStepComponent comp, DamageModifyEvent args)
    {
        if (args.Origin is { } origin && HasComp<StepTriggerComponent>(origin))
            args.Damage *= 0.5f;
    }

    /// <summary>TRAIT_SKITTISH: с разбега в закрытый шкаф — запрыгнуть внутрь и закрыться.</summary>
    private void OnSkittishCollide(Entity<ImperialQuirkSkittishComponent> ent, ref StartCollideEvent args)
    {
        var closet = args.OtherEntity;
        if (_timing.CurTime < ent.Comp.NextDive
            || !TryComp<EntityStorageComponent>(closet, out var storage) || storage.Open
            || !TryComp<InputMoverComponent>(ent, out var mover) || !mover.Sprinting
            || !Alive(ent) || TryComp<CombatModeComponent>(ent, out var combat) && combat.IsInCombatMode)
        {
            return;
        }

        ent.Comp.NextDive = _timing.CurTime + TimeSpan.FromSeconds(2);
        if (!_entityStorage.TryOpenStorage(ent, closet, true))
            return;

        _entityStorage.Insert(ent, closet, storage);
        _entityStorage.CloseStorage(closet, storage);
        _popup.PopupEntity(Loc.GetString("imperial-quirk-skittish", ("user", ent.Owner), ("closet", closet)), ent, PopupType.Medium);
    }

    private void OnSpacerDamage(EntityUid uid, ImperialQuirkSpacerComponent comp, DamageModifyEvent args)
    {
        // physiology.cold_mod *= 0.66
        if (args.Damage.DamageDict.TryGetValue("Cold", out var cold) && cold > 0)
            args.Damage.DamageDict["Cold"] = cold * 0.66f;
    }

    /// <summary>
    /// spacer_born: на планете с гравитацией сначала неуютно, через 3 минуты — гравитационная болезнь;
    /// в космосе через минуту становится легче, в невесомости — сразу.
    /// </summary>
    private void UpdateSpacer(Entity<ImperialQuirkSpacerComponent> ent, TimeSpan now)
    {
        if (now < ent.Comp.NextCheck)
            return;

        ent.Comp.NextCheck = now + TimeSpan.FromSeconds(2);
        var comp = ent.Comp;
        var onPlanet = Transform(ent).MapUid is { } map && HasComp<MapAtmosphereComponent>(map);
        var weightless = _gravity.IsWeightless(ent.Owner);

        var wanted = comp.State;
        if (weightless)
        {
            wanted = ImperialSpacerState.NoGravity;
        }
        else if (onPlanet)
        {
            if (comp.State is not (ImperialSpacerState.Planet or ImperialSpacerState.PlanetTooLong))
                wanted = ImperialSpacerState.Planet;
            else if (comp.State == ImperialSpacerState.Planet && now - comp.StateSince >= TimeSpan.FromMinutes(3))
                wanted = ImperialSpacerState.PlanetTooLong;
        }
        else
        {
            if (comp.State is ImperialSpacerState.None or ImperialSpacerState.NoGravity)
                wanted = ImperialSpacerState.SpaceComfortable;
            else if (comp.State is not (ImperialSpacerState.Space or ImperialSpacerState.SpaceComfortable))
                wanted = ImperialSpacerState.Space;
            else if (comp.State == ImperialSpacerState.Space && now - comp.StateSince >= TimeSpan.FromMinutes(1))
                wanted = ImperialSpacerState.SpaceComfortable;
        }

        if (wanted != comp.State)
        {
            var silent = comp.State == ImperialSpacerState.None;
            comp.State = wanted;
            comp.StateSince = now;
            RecalcSpeed(ent);
            if (!silent)
                _popup.PopupEntity(Loc.GetString($"imperial-quirk-spacer-{wanted.ToString().ToLowerInvariant()}"), ent, ent,
                    wanted is ImperialSpacerState.Planet or ImperialSpacerState.PlanetTooLong ? PopupType.MediumCaution : PopupType.Medium);
        }

        // gravity_sickness: тошнота и слабость.
        if (comp.State == ImperialSpacerState.PlanetTooLong && Alive(ent) && _random.Prob(0.02f))
        {
            _popup.PopupEntity(Loc.GetString("imperial-quirk-spacer-sick"), ent, ent, PopupType.SmallCaution);
            if (_random.Prob(0.3f) && !HasComp<ImperialQuirkStrongStomachComponent>(ent))
                _vomit.Vomit(ent);
        }
    }

    #endregion

    private void UpdateNeutral(float frameTime)
    {
    }

    private void UpdatePositive(float frameTime)
    {
        var now = _timing.CurTime;
        var drunks = EntityQueryEnumerator<ImperialQuirkDrunkHealingComponent>();
        while (drunks.MoveNext(out var uid, out var comp))
            UpdateDrunkHealing((uid, comp), now);

        var spacers = EntityQueryEnumerator<ImperialQuirkSpacerComponent>();
        while (spacers.MoveNext(out var uid, out var comp))
            UpdateSpacer((uid, comp), now);
    }
}
