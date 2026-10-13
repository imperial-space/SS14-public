using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Imperial.Xenobiology.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.Actions;
using Content.Shared.Administration.Systems;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Temperature.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Xenobiology;

/// <summary>
/// mob/living/basic/slime: жизнь, голод, рост, эволюция, деление, мутации и смерть слайма.
/// </summary>
public sealed partial class SlimeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly EntProtoId EvolveAction = "ActionSlimeEvolve";
    private static readonly EntProtoId ReproduceAction = "ActionSlimeReproduce";
    private static readonly EntProtoId BabySlime = "MobXenoSlime";
    private static readonly SoundSpecifier SmokeSound = new SoundPathSpecifier("/Audio/Imperial/Xenobiology/smoke.ogg");

    /// <summary>SSMOBS_DT: Life() раз в 2 секунды.</summary>
    private const float LifeInterval = 2f;

    /// <summary>Здоровье детёныша (maxHealth 150); взрослый — 150 / 0.75 = 200.</summary>
    private const float BabyHealth = 150f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlimeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SlimeComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<SlimeComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<SlimeComponent, SlimeEvolveActionEvent>(OnEvolveAction);
        SubscribeLocalEvent<SlimeComponent, SlimeReproduceActionEvent>(OnReproduceAction);

        InitializeCombat();
        InitializeFeeding();
        InitializeAi();
    }

    #region Появление

    private void OnMapInit(Entity<SlimeComponent> ent, ref MapInitEvent args)
    {
        var brain = EnsureComp<SlimeBrainComponent>(ent);
        brain.NextLife = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(0f, LifeInterval));

        EntityUid? evolve = null;
        EntityUid? reproduce = null;
        _actions.AddAction(ent, ref evolve, EvolveAction);
        _actions.AddAction(ent, ref reproduce, ReproduceAction);

        if (ent.Comp.RandomType)
        {
            ent.Comp.SlimeType = RandomSlimeType();
            ent.Comp.RandomType = false;
        }

        SetLifeStage(ent, ent.Comp.Stage, initial: true);
        SetSlimeType(ent, ent.Comp.SlimeType);
        if (ent.Comp.HungerDisabled)
            SetPacified(ent);
    }

    public ProtoId<SlimeTypePrototype> RandomSlimeType()
    {
        return _random.Pick(_proto.EnumeratePrototypes<SlimeTypePrototype>().ToList()).ID;
    }

    /// <summary>Создаёт слайма заданного цвета и возраста.</summary>
    public EntityUid SpawnSlime(Robust.Shared.Map.EntityCoordinates coords, ProtoId<SlimeTypePrototype>? type = null, SlimeLifeStage stage = SlimeLifeStage.Baby)
    {
        var uid = Spawn(BabySlime, coords);
        var comp = Comp<SlimeComponent>(uid);
        SetLifeStage((uid, comp), stage);
        SetSlimeType((uid, comp), type ?? comp.SlimeType);
        return uid;
    }

    #endregion

    #region Тип, возраст, имя

    /// <summary>set_slime_type().</summary>
    public void SetSlimeType(Entity<SlimeComponent> ent, ProtoId<SlimeTypePrototype> type)
    {
        ent.Comp.SlimeType = type;
        Dirty(ent);
        UpdateName(ent);
    }

    /// <summary>set_life_stage(): взрослый слайм живучее и бьёт вдвое сильнее.</summary>
    public void SetLifeStage(Entity<SlimeComponent> ent, SlimeLifeStage stage, bool initial = false)
    {
        ent.Comp.Stage = stage;
        Dirty(ent);

        var health = stage == SlimeLifeStage.Adult ? BabyHealth / 0.75f : BabyHealth;
        _thresholds.SetMobStateThreshold(ent, FixedPoint2.New(health), MobState.Dead);
        UpdateName(ent);
    }

    /// <summary>update_name(): «серый детёныш слайма (123)», если имя не меняли зельем.</summary>
    public void UpdateName(Entity<SlimeComponent> ent)
    {
        var brain = EnsureComp<SlimeNameComponent>(ent);
        if (brain.CustomName)
            return;

        if (brain.Number == 0)
            brain.Number = _random.Next(1, 1001);

        if (!_proto.TryIndex(ent.Comp.SlimeType, out var type))
            return;

        var stage = Loc.GetString(ent.Comp.Stage == SlimeLifeStage.Adult ? "slime-stage-adult" : "slime-stage-baby");
        _meta.SetEntityName(ent, Loc.GetString("slime-name", ("colour", Loc.GetString(type.Name)), ("stage", stage), ("number", brain.Number)));
    }

    public void SetCustomName(EntityUid uid, string name)
    {
        EnsureComp<SlimeNameComponent>(uid).CustomName = true;
        _meta.SetEntityName(uid, name);
    }

    #endregion

    #region Поведение

    /// <summary>set_pacified_behaviour(): не голоден, не в ярости.</summary>
    public void SetPacified(Entity<SlimeComponent> ent)
    {
        ent.Comp.HungerDisabled = true;
        ent.Comp.Rabid = false;
        ent.Comp.Nutrition = SlimeConstants.StartingNutrition;
        Dirty(ent);
        if (TryComp<SlimeBrainComponent>(ent, out var brain))
        {
            brain.Hunger = SlimeHunger.None;
            brain.HuntTarget = null;
        }
    }

    /// <summary>set_enraged_behaviour(): голоден и в ярости.</summary>
    public void SetEnraged(Entity<SlimeComponent> ent)
    {
        ent.Comp.HungerDisabled = false;
        ent.Comp.Rabid = true;
        Dirty(ent);
    }

    /// <summary>set_default_behaviour(): голоден, но в целом дружелюбен.</summary>
    public void SetDefault(Entity<SlimeComponent> ent)
    {
        ent.Comp.HungerDisabled = false;
        ent.Comp.Rabid = false;
        Dirty(ent);
    }

    public void SetRabid(Entity<SlimeComponent> ent, bool rabid)
    {
        ent.Comp.Rabid = rabid;
        Dirty(ent);
    }

    /// <summary>slime_type.core_type.</summary>
    public EntProtoId CoreFor(SlimeComponent slime)
    {
        return _proto.Index(slime.SlimeType).Core;
    }

    /// <summary>TRAIT_STASIS от консоли ксенобиологии.</summary>
    public void SetConsoleStasis(Entity<SlimeComponent> ent, bool stasis)
    {
        if (!TryComp<SlimeBrainComponent>(ent, out var brain))
            return;

        brain.ConsoleStasis = stasis;
        if (stasis && brain.FeedingOn != null)
            StopFeeding(ent, brain, silent: true);
        UpdateHtn(ent, brain);
    }

    /// <summary>proc/slime_scan(): отчёт сканера слаймов.</summary>
    public string ScanReport(Entity<SlimeComponent> ent)
    {
        var slime = ent.Comp;
        var type = _proto.Index(slime.SlimeType);
        var colour = Loc.GetString(type.Name);
        var stage = Loc.GetString(slime.Stage == SlimeLifeStage.Adult ? "slime-stage-adult" : "slime-stage-baby");
        var lines = new List<string>
        {
            Loc.GetString("slime-scan-header"),
            Loc.GetString("slime-scan-type", ("colour", colour), ("stage", stage)),
            Loc.GetString("slime-scan-nutrition", ("nutrition", (int) slime.Nutrition), ("max", (int) SlimeConstants.MaxNutrition)),
        };

        if (slime.Nutrition < SlimeConstants.StarveNutrition)
            lines.Add(Loc.GetString("slime-scan-starving"));
        else if (slime.Nutrition < SlimeConstants.HungerNutrition)
            lines.Add(Loc.GetString("slime-scan-hungry"));

        var health = 100;
        if (TryComp<DamageableComponent>(ent, out var damageable) && _thresholds.TryGetDeadThreshold(ent, out var dead) && dead > 0)
            health = (int) MathF.Round((1f - (_damageable.GetTotalDamage((ent, damageable)) / dead.Value).Float()) * 100f);
        lines.Add(Loc.GetString("slime-scan-power", ("power", slime.PowerLevel), ("health", health)));

        var mutations = type.Mutations.Keys.Select(k => Loc.GetString(_proto.Index(k).Name)).ToList();
        lines.Add(Loc.GetString("slime-scan-mutations", ("count", mutations.Count),
            ("list", mutations.Count == 0 ? Loc.GetString("slime-scan-none") : string.Join(", ", mutations))));
        lines.Add(Loc.GetString("slime-scan-instability", ("chance", (int) slime.MutationChance)));
        if (slime.Cores > 1)
            lines.Add(Loc.GetString("slime-scan-multiple-cores"));
        lines.Add(Loc.GetString("slime-scan-growth", ("grown", slime.AmountGrown), ("max", SlimeConstants.EvolutionThreshold)));

        if (slime.CrossbreedModification != null)
        {
            lines.Add(Loc.GetString("slime-scan-cross", ("modification", slime.CrossbreedModification)));
            lines.Add(Loc.GetString("slime-scan-cross-progress", ("amount", slime.AppliedCrossbreedAmount), ("max", SlimeConstants.ExtractCrossingRequired)));
        }

        return string.Join("\n", lines);
    }

    #endregion

    #region Осмотр

    private void OnExamined(Entity<SlimeComponent> ent, ref ExaminedEvent args)
    {
        if (_mobState.IsDead(ent))
            return;

        var power = ent.Comp.PowerLevel;
        var key = power switch
        {
            >= SlimeConstants.MaxPower => "slime-examine-power-max",
            >= SlimeConstants.MediumPower => "slime-examine-power-high",
            >= SlimeConstants.ExtraShockCost => "slime-examine-power-medium",
            _ => "slime-examine-power-low",
        };
        args.PushMarkup(Loc.GetString(key));

        if (ent.Comp.Overcrowded)
            args.PushMarkup(Loc.GetString("slime-examine-overcrowded"));

        if (TryComp<DamageableComponent>(ent, out var damageable) &&
            _thresholds.TryGetDeadThreshold(ent, out var max) && max > 0)
        {
            var fraction = (_damageable.GetTotalDamage((ent, damageable)) / max.Value).Float();
            if (fraction >= 0.6f)
                args.PushMarkup(Loc.GetString("slime-examine-damage-heavy"));
            else if (fraction > 0f)
                args.PushMarkup(Loc.GetString("slime-examine-damage-light"));
        }
    }

    #endregion

    #region Жизнь

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<SlimeComponent, SlimeBrainComponent>();
        while (query.MoveNext(out var uid, out var slime, out var brain))
        {
            if (now >= brain.NextLife)
            {
                brain.NextLife = now + TimeSpan.FromSeconds(LifeInterval);
                if (!_mobState.IsDead(uid))
                    Life((uid, slime), brain, LifeInterval);
            }

            UpdateFeeding((uid, slime), brain, now);
            UpdateAi((uid, slime), brain, now);
        }

        UpdateSlimed(now);
    }

    /// <summary>Life(): голод, стазис от BZ, оцепенение от холода.</summary>
    private void Life(Entity<SlimeComponent> ent, SlimeBrainComponent brain, float dt)
    {
        HandleStasis(ent, brain);
        HandleCold(ent, brain);

        if (!brain.BzStasis && !brain.ConsoleStasis)
            HandleNutrition(ent, brain, dt);
    }

    /// <summary>handle_slime_stasis(): 5% BZ при температуре ниже 100 °C усыпляет слайма.</summary>
    private void HandleStasis(Entity<SlimeComponent> ent, SlimeBrainComponent brain)
    {
        var mixture = _atmos.GetContainingMixture(ent.Owner, excite: false);
        var bz = 0f;
        if (mixture != null && mixture.TotalMoles > 0)
            bz = mixture.GetMoles(Gas.BZ) / mixture.TotalMoles;

        var temperature = mixture?.Temperature ?? Atmospherics.TCMB;
        if (bz >= 0.05f && temperature < Atmospherics.T0C + 100f)
        {
            if (brain.BzStasis)
                return;
            brain.BzStasis = true;
            _popup.PopupEntity(Loc.GetString("slime-stasis-start"), ent, ent, PopupType.MediumCaution);
            ent.Comp.PowerLevel = 0;
            ent.Comp.Rabid = false;
            Dirty(ent);
            StopFeeding(ent, brain, silent: true);
            UpdateHtn(ent, brain);
        }
        else if (brain.BzStasis)
        {
            brain.BzStasis = false;
            _popup.PopupEntity(Loc.GetString("slime-stasis-end"), ent, ent);
            UpdateHtn(ent, brain);
        }
    }

    /// <summary>handle_environment(): ниже −40 °C слайм цепенеет (status_effect/freon).</summary>
    private void HandleCold(Entity<SlimeComponent> ent, SlimeBrainComponent brain)
    {
        var frozen = TryComp<TemperatureComponent>(ent, out var temperature) &&
                     temperature.CurrentTemperature <= Atmospherics.T0C - 40f;
        if (frozen)
            _stun.TryUpdateParalyzeDuration(ent, TimeSpan.FromSeconds(LifeInterval + 0.5f));
        if (frozen == brain.Frozen)
            return;
        brain.Frozen = frozen;
        UpdateHtn(ent, brain);
    }

    /// <summary>handle_nutrition(): голод, рост и накопление заряда.</summary>
    private void HandleNutrition(Entity<SlimeComponent> ent, SlimeBrainComponent brain, float dt)
    {
        var slime = ent.Comp;
        if (slime.HungerDisabled)
        {
            slime.Nutrition = 100;
            brain.Hunger = SlimeHunger.None;
            Dirty(ent);
            return;
        }

        if (SptProb(1.25f, dt))
            AdjustNutrition(ent, (slime.Stage == SlimeLifeStage.Adult ? -1f : -0.5f) * dt);

        if (slime.Nutrition < SlimeConstants.StarveNutrition)
        {
            brain.Hunger = SlimeHunger.Starving;
            if (SptProb(0.5f, dt) && brain.Friends.Count > 0)
                Unfriend(ent, brain, _random.Pick(brain.Friends.ToList()));
        }
        else if (slime.Nutrition < SlimeConstants.HungerNutrition ||
                 slime.Nutrition < SlimeConstants.GrowNutrition && SptProb(25f, dt))
        {
            brain.Hunger = SlimeHunger.Hungry;
        }
        else
        {
            brain.Hunger = SlimeHunger.None;
        }

        if (slime.Nutrition <= 0f)
        {
            if (SptProb(50f, dt))
                Damage(ent, "Blunt", _random.Next(0, 6));
            return;
        }

        if (slime.Nutrition >= SlimeConstants.GrowNutrition)
        {
            if (slime.AmountGrown < SlimeConstants.EvolutionThreshold)
            {
                AdjustNutrition(ent, -2.5f * dt);
                slime.AmountGrown++;
            }

            if (slime.PowerLevel < SlimeConstants.MaxPower && SptProb(30 - slime.PowerLevel * 2, dt))
                slime.PowerLevel++;
        }
        else if (slime.PowerLevel < SlimeConstants.MediumPower && slime.Nutrition >= SlimeConstants.HungerNutrition &&
                 SptProb(25 - slime.PowerLevel * 5, dt))
        {
            slime.PowerLevel++;
        }

        Dirty(ent);
    }

    public void AdjustNutrition(Entity<SlimeComponent> ent, float change)
    {
        ent.Comp.Nutrition = Math.Clamp(ent.Comp.Nutrition + change, 0f, SlimeConstants.MaxNutrition);
        Dirty(ent);
    }

    /// <summary>SPT_PROB(chance, seconds): вероятность в процентах за секунду.</summary>
    private bool SptProb(float percentPerSecond, float seconds)
    {
        if (percentPerSecond <= 0f)
            return false;
        var p = 1f - MathF.Pow(1f - Math.Clamp(percentPerSecond, 0f, 100f) / 100f, seconds);
        return _random.Prob(p);
    }

    private void Damage(EntityUid uid, string type, float amount, EntityUid? origin = null)
    {
        if (amount == 0f)
            return;
        _damageable.TryChangeDamage(uid, new DamageSpecifier { DamageDict = { [type] = amount } }, ignoreResistances: true, origin: origin);
    }

    #endregion

    #region Эволюция и деление

    private void OnEvolveAction(Entity<SlimeComponent> ent, ref SlimeEvolveActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        TryEvolve(ent, feedback: true);
    }

    /// <summary>evolve/Activate(): детёныш становится взрослым за 100 питательности.</summary>
    public bool TryEvolve(Entity<SlimeComponent> ent, bool feedback)
    {
        var slime = ent.Comp;
        string? error = null;
        if (_mobState.IsIncapacitated(ent))
            error = "slime-evolve-unconscious";
        else if (slime.Stage == SlimeLifeStage.Adult)
            error = "slime-evolve-adult";
        else if (slime.AmountGrown < SlimeConstants.EvolutionThreshold)
            error = "slime-need-growth";
        else if (slime.Nutrition < SlimeConstants.EvolutionCost)
            error = "slime-evolve-need-food";

        if (error != null)
        {
            if (feedback)
                _popup.PopupEntity(Loc.GetString(error), ent, ent);
            return false;
        }

        AdjustNutrition(ent, -SlimeConstants.EvolutionCost);
        SetLifeStage(ent, SlimeLifeStage.Adult);
        slime.AmountGrown = 0;
        Dirty(ent);
        return true;
    }

    private void OnReproduceAction(Entity<SlimeComponent> ent, ref SlimeReproduceActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        TryReproduce(ent, feedback: true);
    }

    /// <summary>reproduce(): взрослый слайм делится на четырёх детёнышей с возможными мутациями.</summary>
    public bool TryReproduce(Entity<SlimeComponent> ent, bool feedback)
    {
        var slime = ent.Comp;
        string? error = null;
        if (_mobState.IsIncapacitated(ent))
            error = "slime-evolve-unconscious";
        else if (slime.Stage != SlimeLifeStage.Adult)
            error = "slime-reproduce-not-adult";
        else if (slime.AmountGrown < SlimeConstants.EvolutionThreshold)
            error = "slime-need-growth";

        if (error != null)
        {
            if (feedback)
                _popup.PopupEntity(Loc.GetString(error), ent, ent);
            return false;
        }

        var coords = _transform.GetMoverCoordinates(ent);
        var neighbours = _lookup.GetEntitiesInRange<SlimeComponent>(coords, 0.45f).Count(s => s.Owner != ent.Owner);
        slime.Overcrowded = neighbours >= SlimeConstants.OvercrowdAmount;
        Dirty(ent);
        if (slime.Overcrowded)
        {
            if (feedback)
                _popup.PopupEntity(Loc.GetString("slime-overcrowded"), ent, ent);
            return false;
        }

        var newNutrition = MathF.Floor(slime.Nutrition * 0.9f);
        var newPower = (int) MathF.Floor(slime.PowerLevel * 0.25f);
        var friends = TryComp<SlimeBrainComponent>(ent, out var brain) ? brain.Friends.ToList() : new List<EntityUid>();
        var player = HasComp<Robust.Shared.Player.ActorComponent>(ent);

        var created = new List<Entity<SlimeComponent>> { ent };
        for (var i = 0; i < 3; i++)
        {
            var baby = SpawnSlime(coords, RandomMutation(slime));
            var babyComp = Comp<SlimeComponent>(baby);
            created.Add((baby, babyComp));
            foreach (var friend in friends)
            {
                Befriend((baby, babyComp), friend);
            }

            // step_away(baby, src)
            var dir = _random.NextAngle().ToVec();
            _transform.SetCoordinates(baby, coords.Offset(dir * 0.6f));
        }

        slime.Nutrition = SlimeConstants.StartingNutrition;
        foreach (var baby in created)
        {
            if (player)
                baby.Comp.Nutrition = newNutrition;
            baby.Comp.PowerLevel = newPower;
            baby.Comp.MutationChance = slime.MutationChance > 0
                ? Math.Clamp(slime.MutationChance + _random.Next(-5, 6), 0, 100)
                : 0;
            Dirty(baby);
        }

        var selfType = RandomMutation(slime);
        SetLifeStage(ent, SlimeLifeStage.Baby);
        SetSlimeType(ent, selfType);
        slime.AmountGrown = 0;
        slime.MutatorUsed = false;
        Dirty(ent);
        return true;
    }

    /// <summary>get_random_mutation().</summary>
    public ProtoId<SlimeTypePrototype> RandomMutation(SlimeComponent slime)
    {
        if (slime.MutationChance >= 100)
            return "SlimeRainbow";

        if (!_random.Prob(slime.MutationChance / 100f) || !_proto.TryIndex(slime.SlimeType, out var type) || type.Mutations.Count == 0)
            return slime.SlimeType;

        var total = type.Mutations.Values.Sum();
        var roll = _random.NextFloat() * total;
        foreach (var (mutation, weight) in type.Mutations)
        {
            roll -= weight;
            if (roll <= 0f)
                return mutation;
        }

        return type.Mutations.Keys.Last();
    }

    #endregion

    #region Смерть

    /// <summary>death(): взрослый слайм распадается на детёныша и нового бешеного слайма.</summary>
    private void OnMobStateChanged(Entity<SlimeComponent> ent, ref MobStateChangedEvent args)
    {
        if (TryComp<SlimeBrainComponent>(ent, out var brain))
        {
            if (args.NewMobState != MobState.Alive)
                StopFeeding(ent, brain, silent: true);
            UpdateHtn(ent, brain);
        }

        if (args.NewMobState != MobState.Dead)
        {
            if (args.NewMobState == MobState.Alive)
                ent.Comp.Mood = SlimeMood.None;
            Dirty(ent);
            return;
        }

        ent.Comp.Mood = SlimeMood.None;
        Dirty(ent);

        if (ent.Comp.Stage != SlimeLifeStage.Adult)
            return;

        var coords = _transform.GetMoverCoordinates(ent);
        var type = ent.Comp.SlimeType;
        Timer.Spawn(0, () =>
        {
            if (TerminatingOrDeleted(ent))
                return;

            var newSlime = SpawnSlime(coords, type);
            SetRabid((newSlime, Comp<SlimeComponent>(newSlime)), true);

            SetLifeStage(ent, SlimeLifeStage.Baby);
            _rejuvenate.PerformRejuvenate(ent);
        });
    }

    #endregion

    /// <summary>spawn_corecross(): мутировавшее ядро съедает слайма и становится скрещенным экстрактом.</summary>
    public void SpawnCorecross(Entity<SlimeComponent> ent)
    {
        _popup.PopupEntity(Loc.GetString("slime-corecross-start", ("slime", ent.Owner)), ent, PopupType.MediumCaution);
        _audio.PlayPvs(SmokeSound, ent);

        var crossbreed = CrossbreedFor(ent.Comp.SlimeType, ent.Comp.CrossbreedModification);
        if (crossbreed != null)
            Spawn(crossbreed.Value, _transform.GetMoverCoordinates(ent));
        else
            _popup.PopupEntity(Loc.GetString("slime-corecross-fail"), ent, PopupType.Medium);

        QueueDel(ent);
    }

    /// <summary>Скрещенный экстракт для цвета и модификации; null, если такого нет.</summary>
    public EntProtoId? CrossbreedFor(ProtoId<SlimeTypePrototype> type, string? modification)
    {
        if (modification == null)
            return null;

        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || !proto.TryGetComponent<SlimeCrossbreedComponent>(out var cross, EntityManager.ComponentFactory))
                continue;
            if (cross.SlimeType == type && cross.Effect == modification)
                return proto.ID;
        }

        return null;
    }

    /// <summary>try_extract_cores(): хирургическое извлечение экстрактов из мёртвого слайма.</summary>
    public bool TryExtractCores(Entity<SlimeComponent> ent, int count = 1)
    {
        if (!_mobState.IsDead(ent) || count <= 0 || ent.Comp.Cores < count || !_proto.TryIndex(ent.Comp.SlimeType, out var type))
            return false;

        for (var i = 0; i < count; i++)
        {
            Spawn(type.Core, _transform.GetMoverCoordinates(ent));
            ent.Comp.Cores--;
        }

        Dirty(ent);
        return true;
    }

    /// <summary>Включает HTN, только если слайм может действовать сам.</summary>
    private void UpdateHtn(Entity<SlimeComponent> ent, SlimeBrainComponent brain)
    {
        if (!TryComp<HTNComponent>(ent, out var htn))
            return;

        var active = !_mobState.IsIncapacitated(ent) && !brain.BzStasis && !brain.ConsoleStasis &&
                     !brain.Frozen && brain.FeedingOn == null && !HasComp<Robust.Shared.Player.ActorComponent>(ent);
        _htn.SetHTNEnabled((ent, htn), active);
    }
}

/// <summary>Номер в имени слайма и признак имени, данного зельем.</summary>
[RegisterComponent]
public sealed partial class SlimeNameComponent : Component
{
    [ViewVariables]
    public int Number;

    [ViewVariables]
    public bool CustomName;
}
