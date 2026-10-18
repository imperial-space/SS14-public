using System.Linq;
using Content.Shared.Chat;
using Content.Server.Construction.Components;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Climbing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Changeling;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Placeable;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Changeling;

/// <summary>
/// Подробности TG, которым нужны свои системы: конечности (regenerate_limbs, dismember), освещённость для тьмы,
/// образцы речи жертвы, яд пчёл головы-улья, удар рукой-клинком по столам и компьютерам, выход из клетки последним шансом.
/// </summary>
public sealed partial class ImperialChangelingSystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly SoundSpecifier ComputerSlash = new SoundPathSpecifier("/Audio/Weapons/slash.ogg");

    /// <summary>Конечности по категориям органов: рука = плечо + кисть.</summary>
    private static readonly string[] ArmCategories = { "ArmLeft", "HandLeft", "ArmRight", "HandRight" };
    private static readonly string[] LimbCategories = { "ArmLeft", "HandLeft", "ArmRight", "HandRight", "LegLeft", "FootLeft", "LegRight", "FootRight" };

    /// <summary>LING_ABSORB_RECENT_SPEECH.</summary>
    private const int RecentSpeechLines = 8;

    /// <summary>darkness_adapted: dark_color_threshold и LIGHTING_TILE_IS_DARK.</summary>
    private const float DarkAlphaThreshold = 70;
    private const float TileIsDark = 0.2f;

    /// <summary>Последняя прозрачность генокрада во тьме (last_alpha).</summary>
    private readonly Dictionary<EntityUid, float> _darkAlpha = new();

    /// <summary>headcrab/escaping_prison(): стены рушатся через секунду, если генокрад не сдвинулся.</summary>
    private readonly List<(EntityUid Ling, EntityCoordinates Where, List<EntityUid> Walls, TimeSpan At)> _pendingWallBreak = new();

    private void InitializeDetails()
    {
        SubscribeLocalEvent<EntitySpokeEvent>(OnAnySpoke, before: new[] { typeof(Content.Server.Radio.EntitySystems.RadioSystem) });
        SubscribeLocalEvent<ImperialChangelingArmBladeComponent, MeleeHitEvent>(OnArmBladeHit);
        SubscribeLocalEvent<ImperialChangelingBeeVenomComponent, MeleeHitEvent>(OnBeeHit);
        SubscribeLocalEvent<Content.Server.Chat.Systems.ExpandICChatRecipientsEvent>(OnExpandRecipients);
    }

    private void UpdateDetails(TimeSpan now)
    {
        var deaf = EntityQueryEnumerator<ImperialChangelingDeafComponent>();
        while (deaf.MoveNext(out var deafUid, out var deafComp))
        {
            if (now >= deafComp.ExpiresAt)
                RemCompDeferred<ImperialChangelingDeafComponent>(deafUid);
        }

        for (var i = _pendingWallBreak.Count - 1; i >= 0; i--)
        {
            var (ling, where, walls, at) = _pendingWallBreak[i];
            if (now < at)
                continue;

            _pendingWallBreak.RemoveAt(i);
            // finalize_destruction(): генокрад сдвинулся — стены целы.
            if (TerminatingOrDeleted(ling) || !Transform(ling).Coordinates.TryDistance(EntityManager, where, out var moved) || moved > 0.5f)
                continue;

            foreach (var wall in walls)
            {
                if (!TerminatingOrDeleted(wall))
                    _damage.TryChangeDamage(wall, new DamageSpecifier(_proto.Index<DamageTypePrototype>("Structural"), 10000), true, origin: ling);
            }
        }
    }

    #region Глухота

    /// <summary>soundbang_act(deafen_pwr): цель не слышит чужую речь указанное время.</summary>
    private void ApplyDeafness(EntityUid target, TimeSpan duration)
    {
        var comp = EnsureComp<ImperialChangelingDeafComponent>(target);
        var until = _timing.CurTime + duration;
        if (until > comp.ExpiresAt)
            comp.ExpiresAt = until;
    }

    private void OnExpandRecipients(Content.Server.Chat.Systems.ExpandICChatRecipientsEvent args)
    {
        var deaf = new List<ICommonSession>();
        foreach (var (session, data) in args.Recipients)
        {
            if (!data.Observer && session.AttachedEntity is { } listener && listener != args.Source && HasComp<ImperialChangelingDeafComponent>(listener))
                deaf.Add(session);
        }

        foreach (var session in deaf)
        {
            args.Recipients.Remove(session);
        }
    }

    #endregion

    #region Конечности

    /// <summary>get_missing_limbs(): категории конечностей из исходного тела, которых сейчас нет.</summary>
    private List<(string Category, EntProtoId Proto)> GetMissingLimbs(EntityUid uid, IReadOnlyCollection<string> categories)
    {
        var missing = new List<(string, EntProtoId)>();
        if (!TryComp<InitialBodyComponent>(uid, out var initial) || !_container.TryGetContainer(uid, BodyComponent.ContainerID, out var organs))
            return missing;

        var present = new HashSet<string>();
        foreach (var organ in organs.ContainedEntities)
        {
            if (TryComp<OrganComponent>(organ, out var organComp) && organComp.Category is { } category)
                present.Add(category.Id);
        }

        foreach (var (category, proto) in initial.Organs)
        {
            if (categories.Contains(category.Id) && !present.Contains(category.Id))
                missing.Add((category.Id, proto.Id));
        }

        return missing;
    }

    /// <summary>regenerate_limbs(): отрастить недостающие конечности и вернуть им облик текущего профиля.</summary>
    private bool RegenerateLimbs(Entity<ImperialChangelingComponent> ent, IReadOnlyCollection<string> categories)
    {
        var missing = GetMissingLimbs(ent, categories);
        if (missing.Count == 0 || !_container.TryGetContainer(ent, BodyComponent.ContainerID, out var organs))
            return false;

        foreach (var (_, proto) in missing)
        {
            var limb = Spawn(proto, Transform(ent).Coordinates);
            if (!_container.Insert(limb, organs))
                Del(limb);
        }

        if ((ent.Comp.CurrentProfile ?? ent.Comp.FirstProfile) is { } profile && !TerminatingOrDeleted(profile))
            _visualBody.CopyAppearanceFrom(profile, ent.Owner);

        return true;
    }

    /// <summary>Конечности отрастают с громким хрустом: demon_consume и крик.</summary>
    private void LimbsRegrownFeedback(EntityUid uid, string othersKey, string selfKey, bool sound = true)
    {
        if (sound)
            _audio.PlayPvs(DemonConsume, uid);
        _popup.PopupEntity(Loc.GetString(othersKey, ("user", IdentityOf(uid))), uid, Filter.PvsExcept(uid), true, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString(selfKey), uid, uid, PopupType.LargeCaution);
        _chatSystem.TryEmoteWithChat(uid, "Scream", ignoreActionBlocker: true, forceEmote: true);
    }

    /// <summary>dismember(): оторвать обе руки. Возвращает число оторванных рук.</summary>
    private int TearOffArms(EntityUid uid)
    {
        if (!_container.TryGetContainer(uid, BodyComponent.ContainerID, out var organs))
            return 0;

        var removed = new HashSet<string>();
        foreach (var organ in organs.ContainedEntities.ToList())
        {
            if (!TryComp<OrganComponent>(organ, out var organComp) || organComp.Category is not { } category || !ArmCategories.Contains(category.Id))
                continue;

            _container.Remove(organ, organs, force: true);
            QueueDel(organ);
            removed.Add(category.Id.EndsWith("Left") ? "Left" : "Right");
        }

        return removed.Count;
    }

    private EntityUid IdentityOf(EntityUid uid)
    {
        return Content.Shared.IdentityManagement.Identity.Entity(uid, EntityManager);
    }

    #endregion

    #region Тьма

    /// <summary>get_lumcount(): сумма ближних незаслонённых источников света, от 0 до 1.</summary>
    private float GetLumcount(EntityUid uid)
    {
        var here = _transform.GetMapCoordinates(uid);
        var lum = 0f;
        foreach (var light in _lookup.GetEntitiesInRange<PointLightComponent>(here, 12f))
        {
            if (!light.Comp.Enabled || light.Comp.Energy <= 0 || light.Comp.Radius <= 0)
                continue;

            var there = _transform.GetMapCoordinates(light.Owner);
            if (there.MapId != here.MapId)
                continue;

            var distance = (there.Position - here.Position).Length();
            if (distance >= light.Comp.Radius || !_examine.InRangeUnOccluded(there, here, light.Comp.Radius, null))
                continue;

            lum += light.Comp.Energy * (1 - distance / light.Comp.Radius);
            if (lum >= 1)
                return 1;
        }

        return lum;
    }

    /// <summary>darkness_adapted/update_invis(): alpha = clamp(65 + (lum - 0.2) * 255, 65, 255).</summary>
    private void UpdateDarkness(Entity<ImperialChangelingComponent> ent)
    {
        var alpha = Math.Clamp(65 + (GetLumcount(ent) - TileIsDark) * 255, 65, 255);
        if (_darkAlpha.TryGetValue(ent.Owner, out var last) && Math.Abs(last - alpha) < 1)
            return;

        _darkAlpha[ent.Owner] = alpha;
        // Хамелеон сам управляет невидимостью, пока включён.
        if (ent.Comp.Toggled.Contains(ChameleonPower) || !TryComp<Content.Shared.Stealth.Components.StealthComponent>(ent, out var stealth))
            return;

        _stealth.SetVisibility(ent.Owner, alpha / 255f, stealth);
    }

    /// <summary>examine_mob(): кожа неестественно мерцает на свету.</summary>
    private bool IsShimmering(EntityUid uid)
    {
        return _darkAlpha.TryGetValue(uid, out var alpha) && alpha > DarkAlphaThreshold;
    }

    #endregion

    #region Образцы речи

    /// <summary>logging[LOG_SAY]: последние реплики каждого гуманоида.</summary>
    private void OnAnySpoke(EntitySpokeEvent args)
    {
        if (!HasComp<HumanoidProfileComponent>(args.Source))
            return;

        var speech = EnsureComp<ImperialChangelingRecentSpeechComponent>(args.Source);
        speech.Lines.Add(args.Message);
        if (speech.Lines.Count > RecentSpeechLines)
            speech.Lines.RemoveAt(0);
    }

    /// <summary>copy_recent_speech(): образцы речи жертвы в досье и в чат.</summary>
    private void AbsorbRecentSpeech(Entity<ImperialChangelingComponent> ent, EntityUid target, string name, System.Text.StringBuilder stolen)
    {
        if (!TryComp<ImperialChangelingRecentSpeechComponent>(target, out var speech) || speech.Lines.Count == 0)
            return;

        stolen.Append(Loc.GetString("imperial-changeling-memory-speech-header", ("name", name)));
        ChatToChangeling(ent, Loc.GetString("imperial-changeling-memory-speech-chat", ("name", name)), bold: true);

        // Самые свежие реплики первыми, как reverse_range(say_log).
        for (var i = speech.Lines.Count - 1; i >= 0; i--)
        {
            var line = speech.Lines[i];
            stolen.Append($" \"{line}\"");
            ChatToChangeling(ent, $"\"{line}\"", bold: false);
        }

        stolen.Append(Loc.GetString("imperial-changeling-memory-speech-end", ("name", name)));
        ChatToChangeling(ent, Loc.GetString("imperial-changeling-memory-speech-chat-end", ("name", name)), bold: true);
    }

    /// <summary>to_chat(span_boldnotice / span_notice).</summary>
    private void ChatToChangeling(EntityUid uid, string message, bool bold)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        var escaped = Robust.Shared.Utility.FormattedMessage.EscapeText(message);
        var wrapped = bold ? $"[color=#6685f5][bold]{escaped}[/bold][/color]" : $"[color=#6685f5]{escaped}[/color]";
        _chat.ChatMessageToOne(Content.Shared.Chat.ChatChannel.Server, message, wrapped, default, false, actor.PlayerSession.Channel);
    }

    #endregion

    #region Рука-клинок

    /// <summary>arm_blade/afterattack(): столы разваливаются, компьютеры крушатся как от attack_alien.</summary>
    private void OnArmBladeHit(Entity<ImperialChangelingArmBladeComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var target in args.HitEntities)
        {
            if (HasComp<BonkableComponent>(target) && HasComp<PlaceableSurfaceComponent>(target))
            {
                // smash.deconstruct(FALSE)
                _damage.TryChangeDamage(target, new DamageSpecifier(_proto.Index<DamageTypePrototype>("Structural"), 10000), true, origin: args.User);
                continue;
            }

            if (HasComp<ComputerComponent>(target))
            {
                // attack_generic(user, 60, BRUTE, MELEE) + slash.ogg
                _damage.TryChangeDamage(target, new DamageSpecifier(_proto.Index<DamageTypePrototype>("Slash"), 60), origin: args.User);
                _audio.PlayPvs(ComputerSlash, target);
            }
        }
    }

    #endregion

    #region Пчёлы головы-улья

    /// <summary>minion_additional_changes(): пчела берёт случайный реагент из головы-улья.</summary>
    private void AssignBeeReagent(EntityUid bee, EntityUid hiveHead)
    {
        if (!_solutions.TryGetSolution(hiveHead, "hive", out _, out var solution) || solution.Contents.Count == 0)
            return;

        var reagent = _random.Pick(solution.Contents).Reagent.Prototype;
        if (!_proto.TryIndex<Content.Shared.Chemistry.Reagent.ReagentPrototype>(reagent, out var proto))
            return;

        EnsureComp<ImperialChangelingBeeVenomComponent>(bee).Reagent = reagent;
        // name = "[initial(name)] ([toxin.name])"
        var baseName = MetaData(bee).EntityPrototype is { } beeProto ? beeProto.Name : Name(bee);
        _metaData.SetEntityName(bee, $"{baseName} ({proto.LocalizedName})");
    }

    /// <summary>/datum/element/venomous: 1–5 единиц реагента при укусе.</summary>
    private void OnBeeHit(Entity<ImperialChangelingBeeVenomComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        foreach (var target in args.HitEntities)
        {
            _bloodstream.TryAddToBloodstream(target, new Solution(ent.Comp.Reagent, _random.Next(1, 6)));
        }
    }

    #endregion

    #region Последний шанс в клетке

    /// <summary>is_walled(): три и больше заблокированных соседних клетки.</summary>
    private bool IsWalled(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var tile = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);
        var blocked = 0;
        foreach (var dir in new[] { Direction.North, Direction.South, Direction.East, Direction.West })
        {
            if (_turf.IsTileBlocked(grid, tile.Offset(dir), CollisionGroup.Impassable, gridComp))
                blocked++;
        }

        return blocked >= 3;
    }

    /// <summary>Центр клетки, на которой стоит генокрад: range(1, user) — квадрат 3x3 вокруг неё.</summary>
    private EntityCoordinates GetTileCenter(EntityUid uid)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return xform.Coordinates;

        return _map.GridTileToLocal(grid, gridComp, _map.TileIndicesFor(grid, gridComp, xform.Coordinates));
    }

    /// <summary>escaping_prison(): преграды рядом получают 300 урона, стены рвутся через секунду.</summary>
    private void EscapePrison(EntityUid uid)
    {
        _popup.PopupEntity(Loc.GetString("imperial-changeling-last-resort-crush", ("user", IdentityOf(uid))), uid, PopupType.LargeCaution);
        _stun.TryUpdateStunDuration(uid, TimeSpan.FromSeconds(1.5));

        var walls = new List<EntityUid>();
        foreach (var obstacle in _lookup.GetEntitiesInRange<PhysicsComponent>(GetTileCenter(uid), 1.5f, LookupFlags.Static))
        {
            if (obstacle.Owner == uid || !obstacle.Comp.Hard || !obstacle.Comp.CanCollide || !Transform(obstacle).Anchored)
                continue;

            if (_tag.HasTag(obstacle, WallTag))
            {
                walls.Add(obstacle);
                continue;
            }

            _damage.TryChangeDamage(obstacle.Owner, new DamageSpecifier(_proto.Index<DamageTypePrototype>("Structural"), 300), true, origin: uid);
        }

        _pendingWallBreak.Add((uid, Transform(uid).Coordinates, walls, _timing.CurTime + TimeSpan.FromSeconds(1)));
    }

    #endregion
}

/// <summary>Последние реплики гуманоида для copy_recent_speech().</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingRecentSpeechComponent : Component
{
    [ViewVariables]
    public List<string> Lines = new();
}

/// <summary>beegent пчелы из головы-улья.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingBeeVenomComponent : Component
{
    [ViewVariables]
    public string Reagent = string.Empty;
}

/// <summary>Вещи, которые обезьяна не может надеть: слот, куда их вернуть в человеческой форме.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingStashComponent : Component
{
    [ViewVariables]
    public Dictionary<EntityUid, string> Slots = new();
}

/// <summary>Оглушён резонирующим криком: не слышит чужую речь до ExpiresAt.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingDeafComponent : Component
{
    [ViewVariables]
    public TimeSpan ExpiresAt;
}
