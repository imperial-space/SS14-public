using Content.Server.Imperial.Heretic;
using Content.Server.Imperial.Heretic.Effects;
using Content.Server.Imperial.Heretic.Items;
using Content.Server.Imperial.Heretic.Paths.Moon;
using Content.Server.Imperial.Heretic.Paths.Moon.Parade;
using Content.Server.Imperial.Heretic.Paths.Void;
using System.Linq;
using Content.Shared.Chat;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Gibbing;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic.Items;

/// <summary>
/// clothing/suit/hooded/cultrobes/eldritch/moon: роба переводит любой урон в урон мозга,
/// а когда мозг кончается — убивает носителя с кровавым финалом.
/// </summary>
public sealed class HereticGlitteringArraySystem : EntitySystem
{
    [Dependency] private readonly HereticMoonBrainDamageSystem _brainDamage = default!;
    [Dependency] private readonly HereticMoonRingleaderSystem _ringleader = default!;
    [Dependency] private readonly SharedChatSystem _chat = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly SoundPathSpecifier BraindeathSound =
        new("/Audio/Imperial/heretic/sound_effects_curse_curse2.ogg");

    /// <summary>addtimer(kill_wearer, 5 SECONDS).</summary>
    private static readonly TimeSpan DeathDelay = TimeSpan.FromSeconds(5);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticGlitteringArrayComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HereticGlitteringArrayComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<HereticGlitteringArrayComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);

        SubscribeLocalEvent<HereticGlitteringArrayEquippedComponent, KnockDownAttemptEvent>(OnKnockdownAttempt);
        SubscribeLocalEvent<HereticGlitteringArrayEquippedComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<HereticGlitteringArrayEquippedComponent, ShotAttemptedEvent>(OnShootAttempt);
    }

    private void OnEquipped(Entity<HereticGlitteringArrayComponent> ent, ref ClothingGotEquippedEvent args)
    {
        ent.Comp.Wearer = args.Wearer;
        ent.Comp.NextTick = _timing.CurTime + ent.Comp.TickInterval;
        Dirty(ent);
        EnsureComp<HereticGlitteringArrayEquippedComponent>(args.Wearer);
    }

    private void OnUnequipped(Entity<HereticGlitteringArrayComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        ent.Comp.Wearer = null;
        ent.Comp.Braindead = false;
        Dirty(ent);
        RemComp<HereticGlitteringArrayEquippedComponent>(args.Wearer);
    }

    private void OnDamageModify(Entity<HereticGlitteringArrayComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (ent.Comp.Wearer is not { } wearer)
            return;

        var totalDamage = 0f;
        foreach (var v in args.Args.Damage.DamageDict.Values)
        {
            if (v > FixedPoint2.Zero)
                totalDamage += v.Float();
        }

        if (totalDamage <= 0f)
            return;

        var zeroed = new DamageSpecifier();
        foreach (var key in args.Args.Damage.DamageDict.Keys.ToList())
            zeroed.DamageDict[key] = FixedPoint2.Zero;
        args.Args.Damage = zeroed;

        // handle_damage(): урон уходит в мозг с надбавкой и без скидок от пассивки.
        _brainDamage.AddBrainDamage(wearer, totalDamage * ent.Comp.DamageModifier, ignoreMoonProtection: true);
        CheckBraindeath(ent, wearer);
    }

    private void OnKnockdownAttempt(Entity<HereticGlitteringArrayEquippedComponent> ent, ref KnockDownAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnAttackAttempt(Entity<HereticGlitteringArrayEquippedComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Disarm)
            return;
        if (args.Target == null)
            return;
        if (!HasComp<MobStateComponent>(args.Target.Value))
            return;
        if (args.Weapon != null && HasComp<HereticMoonBladeComponent>(args.Weapon.Value))
            return;
        args.Cancel();
    }

    private void OnShootAttempt(Entity<HereticGlitteringArrayEquippedComponent> ent, ref ShotAttemptedEvent args)
    {
        args.Cancel();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HereticGlitteringArrayComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Wearer is not { } wearer || !Exists(wearer))
                continue;

            // kill_wearer(): через 5 секунд после смерти мозга роба добивает носителя.
            if (comp.Braindead)
            {
                if (now >= comp.DeathAt)
                    KillWearer((uid, comp), wearer);
                continue;
            }

            if (now < comp.NextTick)
                continue;
            comp.NextTick = now + comp.TickInterval;

            if (_mobs.IsDead(wearer))
                continue;

            // process(): не еретику роба просто выжигает мозг.
            if (!HasComp<HereticComponent>(wearer))
                _brainDamage.AddBrainDamage(wearer, comp.NonHereticBrainDamage, ignoreMoonProtection: true);

            PlayEmote(wearer);
            CheckBraindeath((uid, comp), wearer);
        }
    }

    /// <summary>process(): чем сильнее повреждён мозг, тем чаще носителя пробивает на эмоции.</summary>
    private void PlayEmote(EntityUid wearer)
    {
        if (!TryComp<HereticMoonBrainDamageComponent>(wearer, out var brain))
            return;

        var damage = brain.BrainDamage;
        var (chance, emotes) = damage switch
        {
            <= 0f => (0f, Array.Empty<string>()),
            <= 30f => (0.2f, new[] { "Laugh" }),
            <= 60f => (0.4f, new[] { "Laugh", "Sigh" }),
            <= 100f => (0.6f, new[] { "Laugh", "Sigh", "Cough" }),
            <= 150f => (0.8f, new[] { "Laugh", "Sigh", "Cough", "Gasp" }),
            _ => (1f, new[] { "Laugh", "Sigh", "Cough", "Gasp", "Scream" }),
        };

        if (emotes.Length == 0 || !_random.Prob(chance))
            return;

        _chat.TryEmoteWithChat(wearer, _random.Pick(emotes), ignoreActionBlocker: true, forceEmote: true);
    }

    /// <summary>check_braindeath(): мозг кончился — запускаем отсчёт до кровавого финала.</summary>
    private void CheckBraindeath(Entity<HereticGlitteringArrayComponent> ent, EntityUid wearer)
    {
        if (ent.Comp.Braindead)
            return;
        if (!TryComp<HereticMoonBrainDamageComponent>(wearer, out var brain))
            return;
        if (brain.BrainDamage < HereticMoonBrainDamageComponent.MaxBrainDamage)
            return;

        ent.Comp.Braindead = true;
        ent.Comp.DeathAt = _timing.CurTime + DeathDelay;
        _audio.PlayPvs(BraindeathSound, wearer);
        _popup.PopupEntity(Loc.GetString("heretic-glittering-array-braindeath"), wearer, wearer, PopupType.LargeCaution);
    }

    /// <summary>kill_wearer() + on_death(): еретик напоследок проводит Восшествие, затем теряет голову.</summary>
    private void KillWearer(Entity<HereticGlitteringArrayComponent> ent, EntityUid wearer)
    {
        ent.Comp.Braindead = false;

        // kill_wearer(): еретик напоследок проводит Восшествие Артистов.
        if (HasComp<HereticComponent>(wearer))
            _ringleader.CastRingleader(wearer);

        _popup.PopupEntity(Loc.GetString("heretic-glittering-array-head-pops", ("name", Name(wearer))), wearer, PopupType.LargeCaution);
        _gibbing.Gib(wearer);
    }
}
