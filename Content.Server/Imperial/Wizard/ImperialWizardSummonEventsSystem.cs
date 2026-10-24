using System.Linq;
using Content.Server.Ghost;
using Content.Server.Station.Systems;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard;

/// <summary>
/// summonevents() и /datum/round_event_control/wizard: после «Призыва событий» на станции
/// по таймеру случаются только волшебные события; каждая новая покупка учащает их.
/// </summary>
public sealed class ImperialWizardSummonEventsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ImperialSpellbookSpecialsSystem _specials = default!;
    [Dependency] private readonly ImperialWizardTeleportSystem _teleport = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedGodmodeSystem _godmode = default!;
    [Dependency] private readonly SharedPoweredLightSystem _lights = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    /// <summary>frequency_lower: 1 минута.</summary>
    private static readonly TimeSpan Lower = TimeSpan.FromMinutes(1);

    private bool _wizardMode;
    private TimeSpan _upper;
    private TimeSpan _nextEvent;
    private readonly List<(EntityUid Uid, TimeSpan End)> _invincible = new();

    private static readonly string[] RpgPrefixes =
    {
        "imperial-wizard-event-rpg-1", "imperial-wizard-event-rpg-2", "imperial-wizard-event-rpg-3",
        "imperial-wizard-event-rpg-4", "imperial-wizard-event-rpg-5", "imperial-wizard-event-rpg-6",
    };

    private static readonly string[] MadnessMessages =
    {
        "imperial-wizard-event-madness-1", "imperial-wizard-event-madness-2", "imperial-wizard-event-madness-3",
        "imperial-wizard-event-madness-4",
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _wizardMode = false;
            _invincible.Clear();
        });
    }

    /// <summary>summonevents(): первый раз — режим магов с интервалом 1–5 минут, далее верхняя граница падает на минуту.</summary>
    public void Intensify()
    {
        if (!_wizardMode)
        {
            _wizardMode = true;
            _upper = TimeSpan.FromMinutes(5);
            Reschedule();
            return;
        }

        _upper -= TimeSpan.FromMinutes(1);
        if (_upper < Lower)
            _upper = Lower;
        Reschedule();
    }

    private void Reschedule()
    {
        _nextEvent = _timing.CurTime + TimeSpan.FromSeconds(_random.NextDouble(Lower.TotalSeconds, _upper.TotalSeconds));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        for (var i = _invincible.Count - 1; i >= 0; i--)
        {
            if (_invincible[i].End > _timing.CurTime)
                continue;

            if (!TerminatingOrDeleted(_invincible[i].Uid))
                _godmode.DisableGodmode(_invincible[i].Uid);
            _invincible.RemoveAt(i);
        }

        if (!_wizardMode || _timing.CurTime < _nextEvent)
            return;

        Reschedule();
        RunRandom();
    }

    /// <summary>Случайное волшебное событие (wizardevent = TRUE).</summary>
    public void RunRandom()
    {
        var events = new Action[]
        {
            Darkness, Invincibility, Ghosts, SummonGuns, SummonMagic, Petsplosion, ShuffleLocations, ShuffleNames,
            ShuffleMinds, Magicarp, RpgLoot, FakeExplosion, Madness, CursedItems,
        };
        var ev = _random.Pick(events);
        Log.Info($"Волшебное событие: {ev.Method.Name}");
        ev();
    }

    public bool RunNamed(string name)
    {
        var method = GetType().GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (method == null || method.GetParameters().Length != 0)
            return false;

        method.Invoke(this, null);
        return true;
    }

    private List<EntityUid> Humans()
    {
        return _specials.StationHumans(null).ToList();
    }

    private void Announce(string loc)
    {
        foreach (var human in Humans())
        {
            _popup.PopupEntity(Loc.GetString(loc), human, human, PopupType.LargeCaution);
        }
    }

    /// <summary>advanced_darkness: все лампы станции лопаются.</summary>
    private void Darkness()
    {
        var query = EntityQueryEnumerator<PoweredLightComponent>();
        var lights = new List<(EntityUid, PoweredLightComponent)>();
        while (query.MoveNext(out var uid, out var light))
        {
            if (_station.GetOwningStation(uid) != null)
                lights.Add((uid, light));
        }

        foreach (var (uid, light) in lights)
        {
            _lights.TryDestroyBulb(uid, light);
        }

        Announce("imperial-wizard-event-darkness");
    }

    /// <summary>invincible: 40 секунд неуязвимости для всех.</summary>
    private void Invincibility()
    {
        var end = _timing.CurTime + TimeSpan.FromSeconds(40);
        foreach (var human in Humans())
        {
            _godmode.EnableGodmode(human);
            _invincible.Add((human, end));
        }

        Announce("imperial-wizard-event-invincible");
    }

    /// <summary>ghost: призраки становятся видимыми.</summary>
    private void Ghosts()
    {
        _ghost.MakeVisible(true);
        Announce("imperial-wizard-event-ghosts");
    }

    /// <summary>summonguns: rightandwrong(SUMMON_GUNS, null, 10).</summary>
    private void SummonGuns()
    {
        foreach (var human in Humans())
        {
            _specials.GiveSummoned(human, true);
        }
    }

    /// <summary>summonmagic: rightandwrong(SUMMON_MAGIC, null, 10).</summary>
    private void SummonMagic()
    {
        foreach (var human in Humans())
        {
            _specials.GiveSummoned(human, false);
        }
    }

    /// <summary>petsplosion: каждое животное станции размножается (не больше 100 новых).</summary>
    private void Petsplosion()
    {
        var animals = new List<(EntityUid Uid, string Proto)>();
        var query = EntityQueryEnumerator<ActiveNPCComponent, MobStateComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var meta))
        {
            if (meta.EntityPrototype is not { } proto || HasComp<HumanoidProfileComponent>(uid) || _mobs.IsDead(uid)
                || _station.GetOwningStation(uid) == null)
                continue;

            animals.Add((uid, proto.ID));
        }

        foreach (var (uid, proto) in animals.Take(100))
        {
            Spawn(proto, Transform(uid).Coordinates);
        }

        Announce("imperial-wizard-event-petsplosion");
    }

    /// <summary>shuffle/location: все люди меняются местами.</summary>
    private void ShuffleLocations()
    {
        var humans = Humans();
        if (humans.Count < 2)
            return;

        var coords = humans.Select(h => _xform.GetMapCoordinates(h)).ToList();
        _random.Shuffle(coords);
        for (var i = 0; i < humans.Count; i++)
        {
            Spawn("WizardSmoke", _xform.GetMapCoordinates(humans[i]));
            _xform.SetMapCoordinates(humans[i], coords[i]);
        }

        Announce("imperial-wizard-event-shuffle-location");
    }

    /// <summary>shuffle/identity: имена перемешиваются.</summary>
    private void ShuffleNames()
    {
        var humans = Humans();
        if (humans.Count < 2)
            return;

        var names = humans.Select(h => MetaData(h).EntityName).ToList();
        _random.Shuffle(names);
        for (var i = 0; i < humans.Count; i++)
        {
            _meta.SetEntityName(humans[i], names[i]);
        }

        Announce("imperial-wizard-event-shuffle-names");
    }

    /// <summary>shuffle/minds: разумы переселяются по кругу.</summary>
    private void ShuffleMinds()
    {
        var pairs = new List<(EntityUid Body, EntityUid Mind)>();
        foreach (var human in Humans())
        {
            if (_mind.TryGetMind(human, out var mindId, out _))
                pairs.Add((human, mindId));
        }

        if (pairs.Count < 2)
            return;

        _random.Shuffle(pairs);
        var bodies = pairs.Select(p => p.Body).ToList();
        for (var i = 0; i < pairs.Count; i++)
        {
            _mind.TransferTo(pairs[i].Mind, bodies[(i + 1) % bodies.Count], createGhost: false);
        }

        Announce("imperial-wizard-event-shuffle-minds");
    }

    /// <summary>magicarp: волшебные карпы у случайных маяков станции.</summary>
    private void Magicarp()
    {
        var areas = _teleport.GetAreas().Values.ToList();
        if (areas.Count == 0)
            return;

        for (var i = 0; i < 4; i++)
        {
            if (!_teleport.TryGetDestination(_random.Pick(areas), out var coords))
                continue;

            for (var j = 0; j < _random.Next(2, 5); j++)
            {
                Spawn("MobCarpMagic", coords);
            }
        }

        Announce("imperial-wizard-event-magicarp");
    }

    /// <summary>rpgloot: все предметы станции получают «зачарования» в названии.</summary>
    private void RpgLoot()
    {
        var items = new List<EntityUid>();
        var query = EntityQueryEnumerator<ItemComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_station.GetOwningStation(uid) != null)
                items.Add(uid);
        }

        foreach (var item in items)
        {
            var bonus = _random.Next(1, 4);
            _meta.SetEntityName(item, Loc.GetString(_random.Pick(RpgPrefixes), ("name", MetaData(item).EntityName), ("bonus", bonus)));
        }

        Announce("imperial-wizard-event-rpgloot");
    }

    /// <summary>fakeexplosion: звук взрыва и паника — без взрыва.</summary>
    private void FakeExplosion()
    {
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Effects/explosion1.ogg"), Filter.Broadcast(), true);
        Announce("imperial-wizard-event-fake-explosion");
    }

    /// <summary>madness: случайная «истина» и травмы для всех.</summary>
    private void Madness()
    {
        _specials.ApplyMadness(null, Loc.GetString(_random.Pick(MadnessMessages)));
    }

    /// <summary>curseditems: все надевают несъёмные шляпы и мантии волшебника.</summary>
    private void CursedItems()
    {
        foreach (var human in Humans())
        {
            Curse(human, "head", "ClothingHeadHatWizard");
            Curse(human, "outerClothing", "ClothingOuterWizard");
        }

        Announce("imperial-wizard-event-cursed-items");
    }

    private void Curse(EntityUid human, string slot, string proto)
    {
        if (_inventory.TryGetSlotEntity(human, slot, out _))
            _inventory.TryUnequip(human, slot, force: true);

        var item = Spawn(proto, Transform(human).Coordinates);
        if (_inventory.TryEquip(human, item, slot, force: true))
            EnsureComp<UnremoveableComponent>(item);
        else
            QueueDel(item);
    }
}
