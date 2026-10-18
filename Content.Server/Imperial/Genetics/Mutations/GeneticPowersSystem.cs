using System.Linq;
using System.Numerics;
using Content.Server.Administration;
using Content.Server.Chat.Managers;
using Content.Server.Electrocution;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Server.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Imperial.Genetics.Prototypes;
using Content.Shared.Item;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Temperature;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Genetics.Mutations;

/// <summary>
/// Активные способности мутаций и пассивные эффекты, которых нет в SS14: сила, яд, адаптации, мученик.
/// Хромосома силы усиливает эффект в 1.5 раза.
/// </summary>
public sealed class GeneticPowersSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly SharedContentEyeSystem _eye = default!;
    [Dependency] private readonly SharedCuffableSystem _cuffable = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    private static readonly ProtoId<GeneticMutationPrototype> ShockMutation = "MutationShock";
    private static readonly ProtoId<GeneticMutationPrototype> MendingMutation = "MutationLayOnHands";
    private static readonly ProtoId<GeneticMutationPrototype> TelekinesisMutation = "MutationTelekinesis";
    private static readonly ProtoId<GeneticMutationPrototype> FarsightMutation = "MutationFarsight";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GenomeComponent, GeneticTelekinesisActionEvent>(OnTelekinesis);
        SubscribeLocalEvent<GenomeComponent, GeneticShockTouchActionEvent>(OnShockTouch);
        SubscribeLocalEvent<GenomeComponent, GeneticMendingTouchActionEvent>(OnMendingTouch);
        SubscribeLocalEvent<GenomeComponent, GeneticTelepathyActionEvent>(OnTelepathy);
        SubscribeLocalEvent<GenomeComponent, GeneticMindReadActionEvent>(OnMindRead);
        SubscribeLocalEvent<GenomeComponent, GeneticOlfactionActionEvent>(OnOlfaction);
        SubscribeLocalEvent<GenomeComponent, GeneticSpawnInHandActionEvent>(OnSpawnInHand);
        SubscribeLocalEvent<GenomeComponent, GeneticAutotomyActionEvent>(OnAutotomy);
        SubscribeLocalEvent<GenomeComponent, GeneticAdrenalineActionEvent>(OnAdrenaline);
        SubscribeLocalEvent<GenomeComponent, GeneticFarsightActionEvent>(OnFarsight);

        SubscribeLocalEvent<GeneticStrengthComponent, MeleeHitEvent>(OnStrengthHit);
        SubscribeLocalEvent<GeneticVenomComponent, MeleeHitEvent>(OnVenomHit);
        SubscribeLocalEvent<GeneticAdaptationComponent, ModifyChangedTemperatureEvent>(OnTemperatureChange);
        SubscribeLocalEvent<GeneticFarsightComponent, ComponentShutdown>(OnFarsightShutdown);
        SubscribeLocalEvent<GeneticDamageResistanceComponent, DamageModifyEvent>(OnResistanceDamage);
        SubscribeLocalEvent<GeneticFarsightComponent, MutationDeactivatedEvent>(OnFarsightLost);
    }

    private static float Power(GenomeComponent genome, ProtoId<GeneticMutationPrototype> mutation)
    {
        return genome.Active.TryGetValue(mutation, out var active) && (active.Chromosome & ChromosomeKind.Power) != 0
            ? 1.5f
            : 1f;
    }

    #region Активные способности

    /// <summary>Телекинез: предмет летит в руку, существо отбрасывает.</summary>
    private void OnTelekinesis(Entity<GenomeComponent> ent, ref GeneticTelekinesisActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var target = args.Target;
        var user = ent.Owner;

        if (HasComp<ItemComponent>(target) && !Transform(target).Anchored)
        {
            if (!_hands.TryPickupAnyHand(user, target))
            {
                var direction = _xform.GetWorldPosition(user) - _xform.GetWorldPosition(target);
                _throwing.TryThrow(target, direction, 10f, user);
            }

            return;
        }

        if (HasComp<MobStateComponent>(target))
        {
            var away = _xform.GetWorldPosition(target) - _xform.GetWorldPosition(user);
            if (away.LengthSquared() < 0.01f)
                away = Vector2.UnitX;

            _throwing.TryThrow(target, Vector2.Normalize(away) * 4f * Power(ent, TelekinesisMutation), 8f, user);
        }
    }

    private void OnShockTouch(Entity<GenomeComponent> ent, ref GeneticShockTouchActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var power = Power(ent, ShockMutation);
        _electrocution.TryDoElectrocution(args.Target, ent, (int) (20 * power), TimeSpan.FromSeconds(3 * power), true);
    }

    /// <summary>Исцеляющее касание: цель лечится, треть исцелённого урона переходит на целителя.</summary>
    private void OnMendingTouch(Entity<GenomeComponent> ent, ref GeneticMendingTouchActionEvent args)
    {
        if (args.Handled || args.Target == ent.Owner)
            return;

        args.Handled = true;
        var power = Power(ent, MendingMutation);
        var amount = -5 * power;
        var heal = new DamageSpecifier
        {
            DamageDict =
            {
                ["Blunt"] = amount, ["Slash"] = amount, ["Piercing"] = amount,
                ["Heat"] = amount, ["Cold"] = amount, ["Shock"] = amount,
            },
        };
        var healed = _damageable.ChangeDamage(args.Target, heal, true, origin: ent);
        if (healed.Empty)
            return;

        var taken = -healed.GetTotal().Float() / 3f;
        if (taken > 0)
            _damageable.TryChangeDamage(ent.Owner, new DamageSpecifier { DamageDict = { ["Blunt"] = taken } }, true);

        _popup.PopupEntity(Loc.GetString("genetics-power-mending", ("user", ent.Owner), ("target", args.Target)), args.Target);
    }

    private void OnTelepathy(Entity<GenomeComponent> ent, ref GeneticTelepathyActionEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(ent, out var actor))
            return;

        args.Handled = true;
        var target = args.Target;
        var user = ent.Owner;
        _quickDialog.OpenDialog(actor.PlayerSession,
            Loc.GetString("genetics-power-telepathy-title"),
            Loc.GetString("genetics-power-telepathy-prompt"),
            (string message) => SendTelepathy(user, target, message));
    }

    private void SendTelepathy(EntityUid user, EntityUid target, string message)
    {
        message = message.Trim();
        if (message.Length == 0 || Deleted(target))
            return;

        if (message.Length > 200)
            message = message[..200];

        var text = Loc.GetString("genetics-power-telepathy-received", ("message", message));
        _popup.PopupEntity(text, target, target, PopupType.Medium);
        if (TryComp<ActorComponent>(target, out var actor))
            _chatManager.DispatchServerMessage(actor.PlayerSession, text);

        _popup.PopupEntity(Loc.GetString("genetics-power-telepathy-sent", ("target", target)), user, user);
    }

    private void OnMindRead(Entity<GenomeComponent> ent, ref GeneticMindReadActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!TryComp<MindContainerComponent>(args.Target, out var mind) || !mind.HasMind)
        {
            _popup.PopupEntity(Loc.GetString("genetics-power-mindread-empty"), ent, ent);
            return;
        }

        var text = Loc.GetString("genetics-power-mindread", ("name", MetaData(args.Target).EntityName));
        _popup.PopupEntity(text, ent, ent, PopupType.Medium);
        if (TryComp<ActorComponent>(ent, out var actor))
            _chatManager.DispatchServerMessage(actor.PlayerSession, text);
    }

    /// <summary>Сверхчутьё: по отпечаткам на предмете — направление и расстояние до их владельца.</summary>
    private void OnOlfaction(Entity<GenomeComponent> ent, ref GeneticOlfactionActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!TryComp<ForensicsComponent>(args.Target, out var forensics) || forensics.Fingerprints.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("genetics-power-olfaction-nothing"), ent, ent);
            return;
        }

        var userPos = _xform.GetMapCoordinates(ent);
        var query = EntityQueryEnumerator<FingerprintComponent, TransformComponent>();
        while (query.MoveNext(out var owner, out var fingerprint, out var xform))
        {
            if (owner == ent.Owner || fingerprint.Fingerprint == null || !forensics.Fingerprints.Contains(fingerprint.Fingerprint))
                continue;

            var ownerPos = _xform.GetMapCoordinates(owner, xform);
            if (ownerPos.MapId != userPos.MapId)
                continue;

            var delta = ownerPos.Position - userPos.Position;
            var direction = Loc.GetString($"genetics-direction-{delta.ToWorldAngle().GetDir().ToString().ToLowerInvariant()}");
            _popup.PopupEntity(Loc.GetString("genetics-power-olfaction-found",
                ("direction", direction), ("distance", (int) delta.Length())), ent, ent, PopupType.Medium);
            return;
        }

        _popup.PopupEntity(Loc.GetString("genetics-power-olfaction-lost"), ent, ent);
    }

    private void OnSpawnInHand(Entity<GenomeComponent> ent, ref GeneticSpawnInHandActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var item = Spawn(args.Prototype, Transform(ent).Coordinates);
        _hands.PickupOrDrop(ent, item);
    }

    /// <summary>Автотомия: отбросить конечность и выскользнуть из наручников.</summary>
    private void OnAutotomy(Entity<GenomeComponent> ent, ref GeneticAutotomyActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<CuffableComponent>(ent, out var cuffable) || cuffable.Container.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("genetics-power-autotomy-nothing"), ent, ent);
            return;
        }

        args.Handled = true;
        _cuffable.Uncuff(ent, ent, cuffable.Container.ContainedEntities.Last(), cuffable);
        _damageable.TryChangeDamage(ent.Owner, new DamageSpecifier { DamageDict = { ["Slash"] = 15 } }, true);
        _popup.PopupEntity(Loc.GetString("genetics-power-autotomy", ("user", ent.Owner)), ent, PopupType.MediumCaution);
    }

    private void OnAdrenaline(Entity<GenomeComponent> ent, ref GeneticAdrenalineActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (TryComp<StaminaComponent>(ent, out var stamina))
            _stamina.TryTakeStamina(ent, -stamina.StaminaDamage, stamina);

        _popup.PopupEntity(Loc.GetString("genetics-power-adrenaline"), ent, ent, PopupType.Medium);
    }

    private void OnFarsight(Entity<GenomeComponent> ent, ref GeneticFarsightActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (RemComp<GeneticFarsightComponent>(ent))
            return;

        EnsureComp<GeneticFarsightComponent>(ent);
        _eye.SetMaxZoom(ent, new Vector2(1.8f));
        _eye.SetZoom(ent, new Vector2(1.8f));
    }

    private void OnFarsightLost(Entity<GeneticFarsightComponent> ent, ref MutationDeactivatedEvent args)
    {
        if (args.Mutation == FarsightMutation)
            RemCompDeferred<GeneticFarsightComponent>(ent);
    }

    private void OnFarsightShutdown(Entity<GeneticFarsightComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _eye.SetMaxZoom(ent, Vector2.One);
        _eye.ResetZoom(ent);
    }

    #endregion

    #region Пассивные

    /// <summary>Сила и Халк: удар без оружия сильнее.</summary>
    private void OnStrengthHit(Entity<GeneticStrengthComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.Weapon != ent.Owner)
            return;

        args.BonusDamage += args.BaseDamage * (ent.Comp.Multiplier - 1f);
        if (ent.Comp.Bonus is { } bonus)
            args.BonusDamage += bonus;
    }

    /// <summary>Ядовитость: царапины и укусы впрыскивают яд.</summary>
    private void OnVenomHit(Entity<GeneticVenomComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.Weapon != ent.Owner)
            return;

        foreach (var target in args.HitEntities)
        {
            if (target != ent.Owner)
                _damageable.TryChangeDamage(target, ent.Comp.Damage, origin: ent);
        }
    }

    /// <summary>Сопротивляемость урону от инфузии (голиаф, таракан).</summary>
    private void OnResistanceDamage(Entity<GeneticDamageResistanceComponent> ent, ref DamageModifyEvent args)
    {
        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, ent.Comp.Modifiers);
    }

    /// <summary>Адаптация: тело не нагревается или не остывает от среды.</summary>
    private void OnTemperatureChange(Entity<GeneticAdaptationComponent> ent, ref ModifyChangedTemperatureEvent args)
    {
        if (args.TemperatureDelta < 0 && ent.Comp.Cold || args.TemperatureDelta > 0 && ent.Comp.Heat)
            args.TemperatureDelta = 0;
    }

    #endregion
}

/// <summary>Удар без оружия сильнее (сила, Халк).</summary>
[RegisterComponent]
public sealed partial class GeneticStrengthComponent : Component
{
    [DataField]
    public float Multiplier = 1.5f;

    /// <summary>Дополнительный урон, например структурный у Халка.</summary>
    [DataField]
    public DamageSpecifier? Bonus;
}

/// <summary>Удары без оружия отравляют.</summary>
[RegisterComponent]
public sealed partial class GeneticVenomComponent : Component
{
    [DataField]
    public DamageSpecifier Damage = new() { DamageDict = { ["Poison"] = 3 } };
}

/// <summary>Адаптация к холоду и/или жаре.</summary>
[RegisterComponent]
public sealed partial class GeneticAdaptationComponent : Component
{
    [DataField]
    public bool Cold;

    [DataField]
    public bool Heat;
}

/// <summary>Включённая дальнозоркость.</summary>
[RegisterComponent]
public sealed partial class GeneticFarsightComponent : Component;

/// <summary>Сопротивляемость урону (инфузия голиафа, таракана).</summary>
[RegisterComponent]
public sealed partial class GeneticDamageResistanceComponent : Component
{
    [DataField(required: true)]
    public DamageModifierSet Modifiers = new();
}
