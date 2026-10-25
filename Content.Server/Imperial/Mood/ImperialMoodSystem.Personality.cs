using System.Linq;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Mood;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Traits;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Mood;

/// <summary>/datum/personality TG: модификаторы настроения, реакции на других, паранойя, отношение к НТ.</summary>
public sealed partial class ImperialMoodSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public static readonly ProtoId<TraitCategoryPrototype> PersonalityCategory = "ImperialPersonalities";

    /// <summary>max_personalities.</summary>
    private const int MaxPersonalities = 5;

    private const float EmpathyRange = 4f;
    private const float ParanoiaRange = 5f;
    private const float PropagandaRange = 5f;

    private void InitializePersonality()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<ImperialMoodComponent, PullStartedMessage>(OnPulled);
    }

    public bool HasPersonality(ImperialMoodComponent comp, string key) => comp.Personalities.Contains(key);

    private bool HasPersonality(EntityUid uid, string key) => TryComp<ImperialMoodComponent>(uid, out var comp) && comp.Personalities.Contains(key);

    /// <summary>preference/personality/apply_to_human: личности из черт, без несовместимых и не больше пяти.</summary>
    private void OnSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (!HasComp<HumanoidProfileComponent>(args.Mob))
            return;

        var comp = EnsureComp<ImperialMoodComponent>(args.Mob);
        var chosen = new List<ImperialPersonalityPrototype>();
        foreach (var id in args.Profile.TraitPreferences)
        {
            if (!_proto.TryIndex(id, out var trait) || trait.Category != PersonalityCategory ||
                !_proto.TryIndex<ImperialPersonalityPrototype>(id.Id, out var personality))
                continue;

            if (chosen.Count >= MaxPersonalities || chosen.Any(p => p.Groups.Intersect(personality.Groups).Any()))
                continue;

            chosen.Add(personality);
        }

        SetPersonalities((args.Mob, comp), chosen.Select(p => p.Key));
    }

    /// <summary>add_personalities / clear_personalities с пересчётом модификаторов.</summary>
    public void SetPersonalities(Entity<ImperialMoodComponent> ent, IEnumerable<string> keys)
    {
        ent.Comp.Personalities = keys.ToHashSet();
        ClearMood(ent.AsNullable(), "paranoia_personality");
        ClearMood(ent.AsNullable(), "nt_personality");
        RecalcModifiers(ent);
    }

    /// <summary>Модификаторы /datum/mood от личностей и сверхчувствительности.</summary>
    public void RecalcModifiers(Entity<ImperialMoodComponent> ent)
    {
        var comp = ent.Comp;
        comp.MoodModifier = 1f
                            - (HasPersonality(comp, "apathetic") ? 0.2f : 0f)
                            + (HasPersonality(comp, "sensitive") ? 0.2f : 0f)
                            + (HasComp<ImperialQuirkHypersensitiveComponent>(ent) ? 0.5f : 0f);
        comp.NegativeLengthModifier = 1f - (HasPersonality(comp, "resilient") ? 0.2f : 0f) + (HasPersonality(comp, "brooding") ? 0.2f : 0f);
        comp.PositiveLengthModifier = 1f + (HasPersonality(comp, "hopeful") ? 0.2f : 0f) - (HasPersonality(comp, "pessimistic") ? 0.2f : 0f);
        UpdateMood(ent);
    }

    /// <summary>can_effect_mob, on_add и add_effects мудлетов, зависящие от личности. false — мудлет не действует.</summary>
    private bool ApplyPersonality(Entity<ImperialMoodComponent> ent, ImperialMoodEventPrototype proto, EntityUid? source,
        ref int change, ref string? description)
    {
        var comp = ent.Comp;
        float value = change;

        // MOOD_EVENT_WHIMSY: чудаки радуются глупостям, остальные от них морщатся.
        if (proto.Flags.Contains(ImperialMoodFlag.Whimsy))
        {
            var whimsical = HasPersonality(comp, "whimsical");
            if (value >= 0 && !whimsical || value < 0 && whimsical)
                return false;
        }

        if (proto.Flags.Contains(ImperialMoodFlag.Spiritual) && !HasPersonality(comp, "spiritual"))
            value *= 0.2f;

        if (proto.Flags.Contains(ImperialMoodFlag.Food))
        {
            if (HasPersonality(comp, "ascetic"))
                value *= 0.75f;
            if (HasPersonality(comp, "gourmand"))
                value *= 1.25f;
        }

        if (proto.Flags.Contains(ImperialMoodFlag.Fear))
        {
            if (HasPersonality(comp, "cowardly"))
                value *= 1.25f;
            if (HasPersonality(comp, "brave"))
                value *= 0.75f;
        }

        var name = source is { } s ? Name(s) : string.Empty;
        switch (proto.ID)
        {
            case "hug" when HasPersonality(comp, "aloof"):
                value = -1;
                description = Loc.GetString("imperial-mood-personality-hug-aloof");
                break;
            case "hug" when HasPersonality(comp, "callous"):
                value = 0;
                description = Loc.GetString("imperial-mood-personality-hug-callous");
                break;
            case "pet_animal" when source != null:
                value = PetAnimal(comp, source.Value, out var petText);
                description = Loc.GetString(petText, ("target", name));
                break;
            case "helped_up" when source != null && (HasPersonality(comp, "callous") || HasPersonality(comp, "misanthropic")):
                value = -2;
                description = Loc.GetString("imperial-mood-personality-helped-callous");
                break;
            case "helped_up" when source != null && HasPersonality(comp, "compassionate"):
                value = 2;
                description = Loc.GetString("imperial-mood-personality-helped-compassionate", ("target", name));
                break;
            case "drunk":
                if (HasPersonality(comp, "teetotal"))
                {
                    value *= -1.5f;
                    description = Loc.GetString("imperial-mood-personality-drunk-teetotal");
                }

                if (HasPersonality(comp, "bibulous"))
                    value *= 1.5f;
                break;
        }

        change = (int) Math.Floor(value);
        return true;
    }

    /// <summary>pet_animal/add_effects: любители и нелюбители животных, кошатники и собачники.</summary>
    private int PetAnimal(ImperialMoodComponent comp, EntityUid animal, out string text)
    {
        if (HasPersonality(comp, "animal_disliker"))
        {
            text = "imperial-mood-personality-pet-disliker";
            return -1;
        }

        var cat = IsAnimalKind(animal, "Cat");
        var dog = IsAnimalKind(animal, "Corgi") || IsAnimalKind(animal, "Dog") || IsAnimalKind(animal, "Pug");
        var catFan = HasPersonality(comp, "cat_fan");
        var dogFan = HasPersonality(comp, "dog_fan");

        if (dogFan && dog || catFan && cat || HasPersonality(comp, "animal_friend"))
        {
            text = "imperial-mood-personality-pet-love";
            return 3;
        }

        if (dogFan && cat)
        {
            text = "imperial-mood-personality-pet-not-dog";
            return -1;
        }

        if (catFan && dog)
        {
            text = "imperial-mood-personality-pet-not-cat";
            return -1;
        }

        text = "imperial-mood-personality-pet";
        return 1;
    }

    private bool IsAnimalKind(EntityUid uid, string kind)
    {
        if (MetaData(uid).EntityPrototype is not { } proto)
            return false;

        foreach (var (id, _) in _proto.EnumerateAllParents<EntityPrototype>(proto.ID, true))
        {
            if (id.StartsWith("Mob" + kind))
                return true;
        }

        return false;
    }

    /// <summary>add_personality_mood_to_viewers: эмпаты радуются чужой радости, мизантропы — чужой беде.</summary>
    private void SpreadToViewers(EntityUid source, string id, bool positive)
    {
        if (id is "empathetic_happy" or "empathetic_sad" or "misanthropic_happy" or "misanthropic_sad")
            return;

        foreach (var viewer in _lookup.GetEntitiesInRange<ImperialMoodComponent>(Transform(source).Coordinates, EmpathyRange))
        {
            if (viewer.Owner == source || _mobState.IsIncapacitated(viewer) || !_examine.InRangeUnOccluded(viewer, source, EmpathyRange))
                continue;

            if (HasPersonality(viewer.Comp, "empathetic"))
                AddMood(viewer.AsNullable(), positive ? "other_good_moodlet" : "other_bad_moodlet", positive ? "empathetic_happy" : "empathetic_sad");
            else if (HasPersonality(viewer.Comp, "misanthropic"))
                AddMood(viewer.AsNullable(), positive ? "other_good_moodlet" : "other_bad_moodlet", positive ? "misanthropic_sad" : "misanthropic_happy");
        }
    }

    /// <summary>aloof: не любит, когда его тащат или трогают.</summary>
    private void OnPulled(EntityUid uid, ImperialMoodComponent comp, PullStartedMessage args)
    {
        if (args.PulledUid == uid)
            BadTouch((uid, comp));
    }

    private void BadTouch(Entity<ImperialMoodComponent> ent)
    {
        if (!HasPersonality(ent.Comp, "aloof") || _mobState.IsDead(ent))
            return;

        AddMood(ent.AsNullable(), "bad_touch", ent.Comp.Sanity <= ImperialMood.SanityNeutral ? "very_bad_touch" : "bad_touch");
    }

    /// <summary>on_tick личностей: паранойя и отношение к корпорации.</summary>
    private void PollPersonalities(Entity<ImperialMoodComponent> ent)
    {
        var comp = ent.Comp;
        if (_mobState.IsIncapacitated(ent))
            return;

        if (HasPersonality(comp, "paranoid"))
        {
            var people = 0;
            foreach (var other in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(Transform(ent).Coordinates, ParanoiaRange))
            {
                if (other.Owner != ent.Owner && !_mobState.IsIncapacitated(other) && _examine.InRangeUnOccluded(ent, other, ParanoiaRange))
                    people++;
            }

            AddMood(ent.AsNullable(), "paranoia_personality", people switch
            {
                0 => "paranoid_alone",
                1 => "paranoid_one_on_one",
                <= 6 => "paranoid_small_group",
                _ => "paranoid_large_group",
            });
        }

        var loyalist = HasPersonality(comp, "loyalist");
        if (!loyalist && !HasPersonality(comp, "disillusioned"))
            return;

        if (SeesPropaganda(ent))
            AddMood(ent.AsNullable(), "nt_personality", loyalist ? "nt_loyalist" : "nt_disillusioned");
        else
            ClearMood(ent.AsNullable(), "nt_personality");
    }

    /// <summary>can_see_nt_propaganda: знаки и плакаты Нанотрейзен поблизости.</summary>
    private bool SeesPropaganda(EntityUid uid)
    {
        foreach (var near in _lookup.GetEntitiesInRange(Transform(uid).Coordinates, PropagandaRange))
        {
            if (MetaData(near).EntityPrototype?.ID is not { } id || !id.Contains("Nanotrasen") ||
                !(id.StartsWith("Sign") || id.StartsWith("Poster")))
                continue;

            if (_examine.InRangeUnOccluded(uid, near, PropagandaRange))
                return true;
        }

        return false;
    }

    /// <summary>select_random_personalities: случайный набор без конфликтов групп.</summary>
    public List<string> RandomPersonalities(int min, int max)
    {
        var all = _proto.EnumeratePrototypes<ImperialPersonalityPrototype>().ToList();
        _random.Shuffle(all);
        var count = _random.Next(min, max + 1);
        var result = new List<ImperialPersonalityPrototype>();
        foreach (var personality in all)
        {
            if (result.Count >= count)
                break;

            if (result.Any(p => p.Groups.Intersect(personality.Groups).Any()))
                continue;

            result.Add(personality);
        }

        return result.Select(p => p.Key).ToList();
    }

    public string PersonalityNames(ImperialMoodComponent comp)
    {
        var names = comp.Personalities.Select(k => Loc.GetString($"imperial-personality-{k}-name")).ToList();
        return names.Count == 0 ? Loc.GetString("imperial-mood-personality-none") : string.Join(", ", names);
    }
}
