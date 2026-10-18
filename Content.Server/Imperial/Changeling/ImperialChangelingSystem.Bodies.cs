using System.Linq;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Changeling.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Gibbing;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Changeling;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Changeling;

/// <summary>
/// Смена тела TG: низшая форма (lesserform.dm), последний шанс (headcrab.dm), червь и яйцо (headslug, headslug_eggs.dm).
/// </summary>
public sealed partial class ImperialChangelingSystem
{
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly Content.Shared.Sprite.SharedScaleVisualsSystem _scale = default!;

    public const string LesserFormPower = "ImperialChangelingLesserForm";
    public const string LastResortPower = "ImperialChangelingLastResort";

    private static readonly EntProtoId Monkey = "MobMonkey";
    private static readonly EntProtoId Headslug = "ImperialChangelingHeadslug";
    private static readonly SoundSpecifier Crack = new SoundPathSpecifier("/Audio/Imperial/Changeling/crack1.ogg");
    private static readonly SoundSpecifier GoreSplat = new SoundPathSpecifier("/Audio/Imperial/Changeling/goresplat.ogg");

    /// <summary>EGG_INCUBATION_TIME.</summary>
    private static readonly TimeSpan EggIncubation = TimeSpan.FromMinutes(4);

    /// <summary>LAST_RESORT_BLIND_RANGE.</summary>
    private const float LastResortBlindRange = 4;

    private readonly Dictionary<EntityUid, TimeSpan> _lastResortConfirm = new();

    /// <summary>headcrab: тело лопнет (1.1 с), червь появится ещё через 0.5 с.</summary>
    private readonly List<(EntityUid Body, TimeSpan At)> _pendingBurst = new();

    private readonly List<(EntityUid Mind, EntityCoordinates Coords, TimeSpan At, EntityUid OldBody)> _pendingSlug = new();

    private void InitializeBodies()
    {
        _powers[LesserFormPower] = UseLesserForm;
        _powers[LastResortPower] = UseLastResort;

        SubscribeLocalEvent<ImperialChangelingHeadslugComponent, MeleeHitEvent>(OnHeadslugHit);
        SubscribeLocalEvent<ImperialChangelingHeadslugComponent, Content.Shared.Examine.ExaminedEvent>(OnHeadslugExamined);
    }

    private void UpdateBodies(TimeSpan now)
    {
        for (var i = _pendingBurst.Count - 1; i >= 0; i--)
        {
            var (body, at) = _pendingBurst[i];
            if (now < at)
                continue;

            _pendingBurst.RemoveAt(i);
            if (!TerminatingOrDeleted(body))
                BurstBody(body);
        }

        for (var i = _pendingSlug.Count - 1; i >= 0; i--)
        {
            var (mind, coords, at, oldBody) = _pendingSlug[i];
            if (now < at)
                continue;

            _pendingSlug.RemoveAt(i);
            SpawnHeadslug(mind, coords, oldBody);
        }

        var slugs = EntityQueryEnumerator<ImperialChangelingHeadslugComponent>();
        while (slugs.MoveNext(out var uid, out var slug))
        {
            if (slug.DieAt is { } die && now >= die)
            {
                slug.DieAt = null;
                if (TryComp<DamageableComponent>(uid, out _))
                    _damage.TryChangeDamage(uid, new Content.Shared.Damage.DamageSpecifier(_proto.Index<Content.Shared.Damage.Prototypes.DamageTypePrototype>("Bloodloss"), 200), true);
            }
        }

        UpdateMorphs(now);

        var eggs = EntityQueryEnumerator<ImperialChangelingEggComponent>();
        while (eggs.MoveNext(out var uid, out var egg))
        {
            if (now >= egg.HatchAt)
                PopEgg((uid, egg));
        }
    }

    #region Перенос генокрада

    /// <summary>
    /// В TG генокрад живёт на разуме и переходит вместе с ним; здесь данные переносятся с тела на тело.
    /// </summary>
    public ImperialChangelingComponent MoveChangeling(Entity<ImperialChangelingComponent> from, EntityUid to)
    {
        var old = from.Comp;
        var consumed = new Dictionary<EntityUid, EntityUid?>();
        EntityUid? currentIdentity = null;
        if (TryComp<ChangelingIdentityComponent>(from, out var oldIdentity))
        {
            consumed = new Dictionary<EntityUid, EntityUid?>(oldIdentity.ConsumedIdentities);
            currentIdentity = oldIdentity.CurrentIdentity;
            // Иначе ванильное хранилище удалит все копии вместе с компонентом.
            oldIdentity.ConsumedIdentities.Clear();
            RemComp(from, oldIdentity);
        }

        var innate = old.Innate.ToList();
        var purchased = old.Purchased.ToList();
        var data = (old.Chemicals, old.MaxChemicals, old.RechargeRate, old.GeneticPoints, old.TotalGeneticPoints, old.AbsorbedCount,
            old.TrueAbsorbs, old.CanRespec, old.DnaMax, old.StingRange, old.ChangelingId, old.HiveName, old.Profiles.ToList(),
            old.FirstProfile, old.CurrentProfile, old.Memories.ToList(), old.StolenAntagInfo, old.Headslug);

        RemComp<ImperialChangelingComponent>(from);

        var comp = EnsureComp<ImperialChangelingComponent>(to);
        (comp.Chemicals, comp.MaxChemicals, comp.RechargeRate, comp.GeneticPoints, comp.TotalGeneticPoints, comp.AbsorbedCount,
            comp.TrueAbsorbs, comp.CanRespec, comp.DnaMax, comp.StingRange, comp.ChangelingId, comp.HiveName, comp.Profiles,
            comp.FirstProfile, comp.CurrentProfile, comp.Memories, comp.StolenAntagInfo, comp.Headslug) = data;

        Entity<ImperialChangelingComponent> ent = (to, comp);
        _actions.AddAction(to, ref comp.EmporiumAction, EmporiumAction);
        var ui = EnsureComp<UserInterfaceComponent>(to);
        _ui.SetUi((to, ui), ImperialChangelingUiKey.Emporium, new InterfaceData("ImperialChangelingEmporiumBoundUserInterface"));
        _ui.SetUi((to, ui), ImperialChangelingUiKey.Transform, new InterfaceData("ImperialChangelingTransformBoundUserInterface"));

        comp.Innate.AddRange(innate);
        comp.Purchased.AddRange(purchased);
        foreach (var id in innate.Concat(purchased))
        {
            if (!_proto.TryIndex<ImperialChangelingPowerPrototype>(id, out var power))
                continue;

            GrantAction(ent, power);
            if (_powerGranted.TryGetValue(power.ID, out var granted))
                granted(ent);
        }

        GrantInfo(ent);
        AddHivemind(to);

        // Новое хранилище клонирует новое тело при появлении: эта копия не нужна.
        var identity = EnsureComp<ChangelingIdentityComponent>(to);
        foreach (var extra in identity.ConsumedIdentities.Keys.ToList())
        {
            if (!consumed.ContainsKey(extra))
                _identity.DropStoredIdentity((to, identity), extra);
        }

        foreach (var (stored, original) in consumed)
        {
            identity.ConsumedIdentities[stored] = original;
        }

        identity.CurrentIdentity = currentIdentity;
        Dirty(to, identity);
        Dirty(ent);
        SyncMind(ent);
        return comp;
    }

    /// <summary>Новое тело: генокрад, разум и урон переходят, старое тело исчезает, вещи падают.</summary>
    private EntityUid SwapBody(Entity<ImperialChangelingComponent> ent, EntProtoId proto, bool keepDamage = true)
    {
        var old = ent.Owner;
        var body = Spawn(proto, _transform.GetMapCoordinates(old));
        _transform.SetLocalRotation(body, Transform(old).LocalRotation);

        if (keepDamage && TryComp<DamageableComponent>(old, out var oldDamage) && HasComp<DamageableComponent>(body))
            _damage.SetDamage(body, _damage.GetAllDamage((old, oldDamage)));

        MoveChangeling(ent, body);
        if (_mind.TryGetMind(old, out var mindId, out _))
            _mind.TransferTo(mindId, body);

        TransferBelongings(old, body, IsLesserForm(body) || proto == Monkey);

        foreach (var item in _inventory.GetHandOrInventoryEntities(old).ToList())
        {
            _transform.DropNextTo(item, old);
        }

        QueueDel(old);
        return body;
    }

    /// <summary>/datum/species/monkey: no_equip_flags = OCLOTHING | GLOVES | FEET | SUITSTORE.</summary>
    private static readonly HashSet<string> MonkeyNoEquip = new() { "outerClothing", "gloves", "shoes", "suitstorage" };

    private const string StashContainer = "imperial_changeling_stash";

    /// <summary>
    /// set_species() в TG оставляет вещи на том же теле, падает только то, что новый вид носить не может.
    /// Здесь тело новое, поэтому вещи переезжают слот в слот. Слотов, которых у обезьяны SS14 нет
    /// (спина, пояс, очки, шея, карманы), прячутся в теле и возвращаются при обратном превращении.
    /// </summary>
    private void TransferBelongings(EntityUid old, EntityUid body, bool toMonkey)
    {
        // finish_monkeyize(): uncuff()
        if (TryComp<Content.Shared.Cuffs.Components.CuffableComponent>(old, out var cuffable))
        {
            foreach (var cuff in cuffable.Container.ContainedEntities.ToList())
            {
                _container.Remove(cuff, cuffable.Container, force: true);
                _transform.DropNextTo(cuff, old);
            }
        }

        var items = new List<(string Slot, EntityUid Item, bool Dependent)>();
        if (_inventory.TryGetSlots(old, out var slots))
        {
            foreach (var slot in slots)
            {
                if (_inventory.TryGetSlotEntity(old, slot.Name, out var item))
                    items.Add((slot.Name, item.Value, !string.IsNullOrEmpty(slot.DependsOn)));
            }
        }

        // Сначала снимаем карманы и прочее зависимое, иначе они упадут вместе с комбинезоном.
        foreach (var (slot, _, _) in items.OrderByDescending(i => i.Dependent))
        {
            _inventory.TryUnequip(old, slot, silent: true, force: true);
        }

        var held = _hands.EnumerateHeld(old).ToList();
        foreach (var item in held)
        {
            _hands.TryDrop(old, item, checkActionBlocker: false);
        }

        // Спрятанное в прошлой форме возвращается на свои слоты.
        if (TryComp<ImperialChangelingStashComponent>(old, out var oldStash) && _container.TryGetContainer(old, StashContainer, out var stashed))
        {
            foreach (var item in stashed.ContainedEntities.ToList())
            {
                _container.Remove(item, stashed, force: true);
                if (oldStash.Slots.TryGetValue(item, out var slot) && slot.Length > 0)
                    items.Add((slot, item, slots?.FirstOrDefault(s => s.Name == slot) is { DependsOn: { Length: > 0 } }));
                else
                    held.Add(item);
            }
        }

        foreach (var (slot, item, _) in items.OrderBy(i => i.Dependent))
        {
            if (toMonkey && MonkeyNoEquip.Contains(slot))
            {
                // Плоть (DROPDEL) не остаётся на полу.
                if (HasComp<ChangelingFleshClothingComponent>(item))
                    QueueDel(item);
                continue;
            }

            if (_inventory.HasSlot(body, slot) && _inventory.TryEquip(body, item, slot, silent: true, force: true))
                continue;

            Stash(body, item, slot);
        }

        foreach (var item in held)
        {
            if (_hands.TryPickupAnyHand(body, item, checkActionBlocker: false, animate: false))
                continue;

            // У обезьяны SS14 одна рука, у TG — две: лишнее держим в теле до обратного превращения.
            if (toMonkey)
                Stash(body, item, string.Empty);
            else
                _transform.DropNextTo(item, body);
        }
    }

    private void Stash(EntityUid body, EntityUid item, string slot)
    {
        var stash = EnsureComp<ImperialChangelingStashComponent>(body);
        var container = _container.EnsureContainer<Robust.Shared.Containers.Container>(body, StashContainer);
        if (!_container.Insert(item, container))
            return;

        stash.Slots[item] = slot;
    }

    /// <summary>became_monkey(): органическое оружие и хитин втягиваются (req_human).</summary>
    private void RetractHumanMutations(Entity<ImperialChangelingComponent> ent)
    {
        foreach (var (power, weapon) in _weapons)
        {
            if (ent.Comp.Purchased.Contains(power))
                UnequipWeapon(ent, weapon.Item, weapon.Name, weapon.Silent);
        }

        if (_suits.TryGetValue(ArmorPower, out var armor))
            CastOffSuit(ent, armor.Suit, armor.Helmet, armor.Name, armor.Slowdown, armor.Blood);
    }

    #endregion

    #region Низшая форма

    /// <summary>TRANSFORMATION_DURATION: анимация превращения.</summary>
    private static readonly TimeSpan MorphDuration = TimeSpan.FromSeconds(2.2);

    private static readonly EntProtoId MonkeyifyEffect = "ImperialChangelingMonkeyifyEffect";
    private static readonly EntProtoId HumanifyEffect = "ImperialChangelingHumanifyEffect";

    /// <summary>Тела в процессе превращения: когда закончить и в кого (null — в обезьяну).</summary>
    private readonly Dictionary<EntityUid, (TimeSpan At, EntityUid? Profile)> _pendingMorph = new();

    /// <summary>Тела, которым мы сами запретили двигаться на время превращения.</summary>
    private readonly HashSet<EntityUid> _morphBlocked = new();

    /// <summary>lesserform/sting_action(): обезьяна и обратно.</summary>
    private bool UseLesserForm(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        if (_pendingMorph.ContainsKey(ent.Owner))
            return false;

        if (!IsLesserForm(ent))
        {
            Popup(ent, "imperial-changeling-lesser-become");
            StartMorph(ent, null);
            return true;
        }

        // select_form(): единственный профиль выбирается сам.
        if (ent.Comp.Profiles.Count == 1 && ent.Comp.FirstProfile is { } only)
        {
            Popup(ent, "imperial-changeling-lesser-human");
            StartMorph(ent, only);
            return true;
        }

        ent.Comp.SelectingHumanForm = true;
        Dirty(ent);
        _ui.OpenUi(ent.Owner, ImperialChangelingUiKey.Transform, ent.Owner);
        return false;
    }

    private float GetLesserCost()
    {
        return _proto.TryIndex<ImperialChangelingPowerPrototype>(LesserFormPower, out var power) ? power.ChemicalCost : 5;
    }

    /// <summary>Выбор облика для возвращения из низшей формы (до закрытия окна).</summary>
    private bool TrySelectHumanForm(Entity<ImperialChangelingComponent> ent, EntityUid profile)
    {
        if (!ent.Comp.SelectingHumanForm)
            return false;

        ent.Comp.SelectingHumanForm = false;
        if (ent.Comp.Chemicals < GetLesserCost())
        {
            Popup(ent, "imperial-changeling-req-chems", ("amount", GetLesserCost()));
            return true;
        }

        Popup(ent, "imperial-changeling-lesser-human");
        AdjustChemicals(ent, -GetLesserCost());
        StartMorph(ent, profile);
        return true;
    }

    /// <summary>monkeyize()/humanize(): тело исчезает, на его месте анимация, оглушение на 2.2 секунды.</summary>
    private void StartMorph(Entity<ImperialChangelingComponent> ent, EntityUid? profile)
    {
        var uid = ent.Owner;
        Spawn(profile == null ? MonkeyifyEffect : HumanifyEffect, Transform(uid).Coordinates);
        // Stun(TRANSFORMATION_DURATION): обездвижен, но вещи из рук не падают (оглушение SS14 их роняет).
        if (!HasComp<Content.Shared.Interaction.Components.BlockMovementComponent>(uid))
        {
            AddComp<Content.Shared.Interaction.Components.BlockMovementComponent>(uid);
            _morphBlocked.Add(uid);
        }

        var stealth = EnsureComp<Content.Shared.Stealth.Components.StealthComponent>(uid);
        _stealth.SetVisibility(uid, -1f, stealth);
        _pendingMorph[uid] = (_timing.CurTime + MorphDuration, profile);
    }

    private void UpdateMorphs(TimeSpan now)
    {
        foreach (var (uid, (at, profile)) in _pendingMorph.ToList())
        {
            if (now < at)
                continue;

            _pendingMorph.Remove(uid);
            if (_morphBlocked.Remove(uid) && !TerminatingOrDeleted(uid))
                RemComp<Content.Shared.Interaction.Components.BlockMovementComponent>(uid);

            if (TerminatingOrDeleted(uid) || !TryComp<ImperialChangelingComponent>(uid, out var comp))
                continue;

            var ent = (uid, comp);
            if (profile == null)
            {
                // finish_monkeyize(): обезьяна выскальзывает из наручников.
                RetractHumanMutations(ent);
                var monkey = SwapBody(ent, Monkey);
                EnsureComp<ImperialChangelingLesserFormComponent>(monkey);
                SetLesserIcon(monkey, "human_form");
                continue;
            }

            Humanize(ent, profile.Value);
        }
    }

    /// <summary>finish_humanize(): тело вида профиля, затем transform() в него.</summary>
    private EntityUid Humanize(Entity<ImperialChangelingComponent> ent, EntityUid profile)
    {
        var species = MetaData(profile).EntityPrototype?.ID ?? "MobHuman";
        var body = SwapBody(ent, species);
        if (TryComp<ImperialChangelingComponent>(body, out var comp))
            TransformInto((body, comp), profile);
        return body;
    }

    #endregion


    /// <summary>apply_button_icon(): «Человеческая форма» в низшей форме.</summary>
    private void SetLesserIcon(EntityUid body, string state)
    {
        if (TryComp<ImperialChangelingComponent>(body, out var comp) && comp.Actions.TryGetValue(LesserFormPower, out var action))
            _actions.SetIcon(action, new Robust.Shared.Utility.SpriteSpecifier.Rsi(new Robust.Shared.Utility.ResPath("/Textures/Imperial/Changeling/actions.rsi"), state));
    }

    #region Последний шанс

    /// <summary>headcrab/sting_action(): тело раздувается и лопает, из останков выползает червь.</summary>
    private bool UseLastResort(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        var uid = ent.Owner;
        if (_pendingBurst.Any(p => p.Body == uid))
            return false;

        // tgui_alert «Уверены ли мы в том, что хотим уничтожить свое тело и создать червя?»
        var now = _timing.CurTime;
        if (!Confirm(ent, LastResortPower, "imperial-changeling-power-lastresort-name", "imperial-changeling-last-resort-confirm"))
            return false;

        _popup.PopupEntity(Loc.GetString("imperial-changeling-last-resort-swell", ("user", Identity.Entity(uid, EntityManager))), uid, PopupType.LargeCaution);
        // animate(user, transform = user.transform * 1.5, color = COLOR_RED, time = 1 SECONDS)
        var swell = EnsureComp<ImperialChangelingSwellComponent>(uid);
        swell.StartTime = now;
        Dirty(uid, swell);
        _audio.PlayPvs(Crack, uid);
        if (IsWalled(uid))
            EscapePrison(uid);
        _pendingBurst.Add((uid, now + TimeSpan.FromSeconds(1.1)));
        return true;
    }

    /// <summary>gibbing() → gore_explosion(): лёгкий взрыв, кровь и ослепление вокруг.</summary>
    private void BurstBody(EntityUid body)
    {
        var coords = Transform(body).Coordinates;
        _popup.PopupEntity(Loc.GetString("imperial-changeling-last-resort-burst", ("user", Identity.Entity(body, EntityManager))), body, PopupType.LargeCaution);
        _audio.PlayPvs(GoreSplat, body);
        _explosion.QueueExplosion(_transform.ToMapCoordinates(coords), "Default", 12, 2, 2, body, canCreateVacuum: false);

        // for(var/turf/bloody_turf in view(LAST_RESORT_BLIND_RANGE, user)): кровь на каждой клетке.
        for (var x = -LastResortBlindRange; x <= LastResortBlindRange; x++)
        {
            for (var y = -LastResortBlindRange; y <= LastResortBlindRange; y++)
            {
                if (x * x + y * y > LastResortBlindRange * LastResortBlindRange)
                    continue;

                _puddle.TrySpillAt(coords.Offset(new System.Numerics.Vector2(x, y)), new Content.Shared.Chemistry.Components.Solution("Blood", 1), out _, sound: false);
            }
        }

        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, LastResortBlindRange))
        {
            if (mob.Owner == body || !_interaction.InRangeUnobstructed(body, mob.Owner, LastResortBlindRange + 0.5f))
                continue;

            _puddle.TrySpillAt(mob.Owner, new Content.Shared.Chemistry.Components.Solution("Blood", 2), out _, sound: false);
            _audio.PlayPvs(Splat, mob.Owner, Robust.Shared.Audio.AudioParams.Default.WithVolume(-6));
            if (HasComp<BorgChassisComponent>(mob))
            {
                _popup.PopupEntity(Loc.GetString("imperial-changeling-last-resort-borg"), mob, mob, PopupType.LargeCaution);
                _stun.TryUpdateParalyzeDuration(mob, TimeSpan.FromSeconds(6));
                continue;
            }

            if (!HasComp<Content.Shared.Eye.Blinding.Components.BlindableComponent>(mob))
                continue;

            _popup.PopupEntity(Loc.GetString("imperial-changeling-last-resort-blind"), mob, mob, PopupType.LargeCaution);
            _stun.TryUpdateStunDuration(mob, TimeSpan.FromSeconds(4));
            _drunk.TryApplyDrunkenness(mob, TimeSpan.FromSeconds(12));
        }

        var mindId = _mind.TryGetMind(body, out var mind, out _) ? mind : (EntityUid?) null;
        if (mindId is { } m)
        {
            // Генокрад переедет на червя: тело пока не удаляем.
            _pendingSlug.Add((m, coords, _timing.CurTime + TimeSpan.FromSeconds(0.5), body));
        }

        _puddle.TrySpillAt(body, new Content.Shared.Chemistry.Components.Solution("Blood", 30), out _);
        if (mindId == null)
            _gibbing.Gib(body);
    }

    /// <summary>spawn_headcrab(): разум переходит в червя.</summary>
    private void SpawnHeadslug(EntityUid mindId, EntityCoordinates coords, EntityUid oldBody)
    {
        var slug = Spawn(Headslug, coords);
        if (!TerminatingOrDeleted(oldBody) && TryComp<ImperialChangelingComponent>(oldBody, out var ling))
            MoveChangeling((oldBody, ling), slug);

        _mind.TransferTo(mindId, slug);
        if (!TerminatingOrDeleted(oldBody))
            _gibbing.Gib(oldBody);

        Popup(slug, "imperial-changeling-headslug-born");
        ChatToChangeling(slug, Loc.GetString("imperial-changeling-headslug-born"), bold: true);
        ChatToChangeling(slug, Loc.GetString("imperial-changeling-headslug-help"), bold: false);
    }

    #endregion

    #region Червь и яйцо

    /// <summary>headslug/examine().</summary>
    private void OnHeadslugExamined(Entity<ImperialChangelingHeadslugComponent> ent, ref Content.Shared.Examine.ExaminedEvent args)
    {
        if (!_mobState.IsDead(ent))
            args.PushMarkup(Loc.GetString(HasComp<Robust.Shared.Player.ActorComponent>(ent) ? "imperial-changeling-headslug-examine-purpose" : "imperial-changeling-headslug-examine-aimless"));

        if (ent.Comp.EggLain)
            args.PushMarkup(Loc.GetString("imperial-changeling-headslug-examine-spent"));
    }

    /// <summary>check_and_implant(): укус мёртвого гуманоида откладывает яйцо.</summary>
    private void OnHeadslugHit(Entity<ImperialChangelingHeadslugComponent> ent, ref MeleeHitEvent args)
    {
        if (ent.Comp.EggLain)
            return;

        foreach (var target in args.HitEntities)
        {
            if (!HasComp<HumanoidProfileComponent>(target) || IsLesserForm(target) || !_mobState.IsDead(target))
                continue;

            if (HasComp<ImperialChangelingEggComponent>(target) || HasComp<Content.Shared.Imperial.Xenomorph.XenoEmbryoComponent>(target))
            {
                _popup.PopupEntity(Loc.GetString("imperial-changeling-headslug-pregnant"), target, ent.Owner);
                return;
            }

            var egg = EnsureComp<ImperialChangelingEggComponent>(target);
            egg.Origin = _mind.TryGetMind(ent.Owner, out var mindId, out _) ? mindId : null;
            egg.HatchAt = _timing.CurTime + EggIncubation;
            ent.Comp.EggLain = true;
            ent.Comp.DieAt = _timing.CurTime + TimeSpan.FromSeconds(10);

            _popup.PopupEntity(Loc.GetString("imperial-changeling-headslug-implant-others", ("slug", Identity.Entity(ent.Owner, EntityManager)), ("target", Identity.Entity(target, EntityManager))),
                ent.Owner, Filter.PvsExcept(ent.Owner), true, PopupType.MediumCaution);
            Popup(ent.Owner, "imperial-changeling-headslug-implant-self", ("target", Identity.Entity(target, EntityManager)));
            Popup(ent.Owner, "imperial-changeling-headslug-dying");
            return;
        }
    }

    /// <summary>pop(): из тела вылезает обезьяна-генокрад, тело лопается.</summary>
    private void PopEgg(Entity<ImperialChangelingEggComponent> egg)
    {
        var host = egg.Owner;
        var origin = egg.Comp.Origin;
        RemComp<ImperialChangelingEggComponent>(host);

        var monkey = Spawn(Monkey, _transform.GetMapCoordinates(host));
        EnsureComp<ImperialChangelingLesserFormComponent>(monkey);

        // origin.current мёртв или он призрак.
        if (origin is { } mindId && TryComp<Content.Shared.Mind.MindComponent>(mindId, out var mind)
            && (mind.OwnedEntity is not { } current || _mobState.IsDead(current) || HasComp<Content.Shared.Ghost.GhostComponent>(current)))
        {
            ImperialChangelingComponent? comp = null;
            if (mind.OwnedEntity is { } previous && TryComp<ImperialChangelingComponent>(previous, out var previousComp))
                comp = MoveChangeling((previous, previousComp), monkey);

            _mind.TransferTo(mindId, monkey);
            comp ??= MakeChangeling(monkey, headslug: true);

            Entity<ImperialChangelingComponent> ent = (monkey, comp);
            if (CanAbsorbDna(ent, host, verbose: false))
                AddNewProfile(ent, host);

            if (!comp.Purchased.Contains(LesserFormPower) && _proto.TryIndex<ImperialChangelingPowerPrototype>(LesserFormPower, out var lesser))
                GivePower(ent, lesser);
            SetLesserIcon(monkey, "human_form");
        }

        _gibbing.Gib(host);
    }

    #endregion
}
