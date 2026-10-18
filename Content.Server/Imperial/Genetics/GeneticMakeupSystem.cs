using System.Globalization;
using System.Linq;
using System.Text;
using Content.Server.Body;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Forensics.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Genetics;

/// <summary>
/// Генетический облик (genetic makeup из SS13). Уникальные ферменты — имя и ДНК, уникальная идентичность —
/// цвета кожи, глаз, волос и бороды, уникальные особенности — цвета остальных маркировок.
/// Строки ферментов шестнадцатеричные: по 6 символов на цвет, как блоки UI/UF в SS13.
/// </summary>
public sealed class GeneticMakeupSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly VisualBodySystem _visualBody = default!;

    private static readonly HashSet<HumanoidVisualLayers> IdentityLayers = new()
    {
        HumanoidVisualLayers.Hair,
        HumanoidVisualLayers.FacialHair,
    };

    /// <summary>Сколько цветов особенностей попадает в уникальные особенности.</summary>
    private const int FeatureColors = 4;

    public GeneticMakeupData Capture(EntityUid uid)
    {
        var data = new GeneticMakeupData
        {
            Name = MetaData(uid).EntityName,
            Dna = CompOrNull<DnaComponent>(uid)?.DNA ?? string.Empty,
            BloodType = GetBloodType(uid),
        };

        if (_visualBody.TryGatherMarkingsData(uid, null, out var profiles, out _, out var applied))
        {
            data.Profiles = new(profiles);
            data.Markings = applied.ToDictionary(
                c => c.Key,
                c => c.Value.ToDictionary(l => l.Key, l => new List<Marking>(l.Value)));
        }

        return data;
    }

    private string GetBloodType(EntityUid uid)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream))
            return string.Empty;

        foreach (var reagent in bloodstream.BloodReferenceSolution.Contents)
        {
            if (_proto.TryIndex<ReagentPrototype>(reagent.Reagent.Prototype, out var proto))
                return proto.LocalizedName;
        }

        return string.Empty;
    }

    #region Строки ферментов

    public string GetUniqueIdentity(GeneticMakeupData data)
    {
        var profile = data.Profiles.Values.FirstOrDefault();
        var builder = new StringBuilder();
        builder.Append(Hex(profile.SkinColor));
        builder.Append(Hex(profile.EyeColor));
        builder.Append(Hex(FirstColor(data, HumanoidVisualLayers.Hair)));
        builder.Append(Hex(FirstColor(data, HumanoidVisualLayers.FacialHair)));
        return builder.ToString();
    }

    public string GetUniqueFeatures(GeneticMakeupData data)
    {
        var builder = new StringBuilder();
        foreach (var marking in FeatureMarkings(data).Take(FeatureColors))
        {
            builder.Append(Hex(marking.MarkingColors.Count > 0 ? marking.MarkingColors[0] : Color.White));
        }

        return builder.ToString();
    }

    /// <summary>Уникальные ферменты: хеш ДНК, как md5(real_name) в SS13.</summary>
    public static string GetUniqueEnzymes(GeneticMakeupData data)
    {
        return data.Dna.Length > 0 ? data.Dna : string.Empty;
    }

    private static Color FirstColor(GeneticMakeupData data, HumanoidVisualLayers layer)
    {
        foreach (var layers in data.Markings.Values)
        {
            if (layers.TryGetValue(layer, out var markings) && markings.Count > 0 && markings[0].MarkingColors.Count > 0)
                return markings[0].MarkingColors[0];
        }

        return Color.Black;
    }

    private static IEnumerable<Marking> FeatureMarkings(GeneticMakeupData data)
    {
        foreach (var (_, layers) in data.Markings.OrderBy(c => c.Key.Id))
        {
            foreach (var (layer, markings) in layers.OrderBy(l => l.Key))
            {
                if (IdentityLayers.Contains(layer))
                    continue;

                foreach (var marking in markings)
                {
                    yield return marking;
                }
            }
        }
    }

    private static string Hex(Color color)
    {
        return $"{(int) (color.R * 255):X2}{(int) (color.G * 255):X2}{(int) (color.B * 255):X2}";
    }

    private static Color ParseHex(string hex, int offset)
    {
        if (offset + 6 > hex.Length
            || !int.TryParse(hex.AsSpan(offset, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return Color.White;
        }

        return new Color((byte) (value >> 16), (byte) (value >> 8), (byte) value);
    }

    /// <summary>Применяет строку уникальной идентичности: цвета кожи, глаз, волос и бороды.</summary>
    public void ApplyUniqueIdentity(EntityUid uid, string identity)
    {
        var data = Capture(uid);
        var skin = ParseHex(identity, 0);
        var eyes = ParseHex(identity, 6);
        foreach (var category in data.Profiles.Keys.ToList())
        {
            var profile = data.Profiles[category];
            profile.SkinColor = skin;
            profile.EyeColor = eyes;
            data.Profiles[category] = profile;
        }

        RecolorFirst(data, HumanoidVisualLayers.Hair, ParseHex(identity, 12));
        RecolorFirst(data, HumanoidVisualLayers.FacialHair, ParseHex(identity, 18));
        _visualBody.ApplyProfiles(uid, data.Profiles);
        _visualBody.ApplyMarkings(uid, data.Markings);
    }

    /// <summary>Применяет строку уникальных особенностей: цвета остальных маркировок.</summary>
    public void ApplyUniqueFeatures(EntityUid uid, string features)
    {
        var data = Capture(uid);
        var index = 0;
        foreach (var (_, layers) in data.Markings.OrderBy(c => c.Key.Id))
        {
            foreach (var (layer, markings) in layers.OrderBy(l => l.Key))
            {
                if (IdentityLayers.Contains(layer))
                    continue;

                for (var i = 0; i < markings.Count && index < FeatureColors; i++, index++)
                {
                    if (index * 6 + 6 <= features.Length)
                        markings[i] = markings[i].WithColorAt(0, ParseHex(features, index * 6));
                }
            }
        }

        _visualBody.ApplyMarkings(uid, data.Markings);
    }

    private static void RecolorFirst(GeneticMakeupData data, HumanoidVisualLayers layer, Color color)
    {
        foreach (var layers in data.Markings.Values)
        {
            if (!layers.TryGetValue(layer, out var markings) || markings.Count == 0)
                continue;

            markings[0] = markings[0].WithColorAt(0, color);
            return;
        }
    }

    #endregion

    /// <summary>Переносит облик целиком или по частям (apply_genetic_makeup).</summary>
    public void Apply(EntityUid uid, GeneticMakeupData makeup, DnaMakeupType type)
    {
        if (type is DnaMakeupType.Enzymes or DnaMakeupType.Mixed)
        {
            if (makeup.Name.Length > 0)
                _metaData.SetEntityName(uid, makeup.Name);

            if (makeup.Dna.Length > 0 && TryComp<DnaComponent>(uid, out var dna))
            {
                dna.DNA = makeup.Dna;
                Dirty(uid, dna);
            }
        }

        if (type is DnaMakeupType.Identity or DnaMakeupType.Features or DnaMakeupType.Mixed)
        {
            var current = Capture(uid);
            foreach (var (category, layers) in makeup.Markings)
            {
                if (!current.Markings.TryGetValue(category, out var target))
                    continue;

                foreach (var (layer, markings) in layers)
                {
                    var identityLayer = IdentityLayers.Contains(layer);
                    if (type == DnaMakeupType.Identity && !identityLayer || type == DnaMakeupType.Features && identityLayer)
                        continue;

                    target[layer] = new List<Marking>(markings);
                }
            }

            if (type is DnaMakeupType.Identity or DnaMakeupType.Mixed)
                _visualBody.ApplyProfiles(uid, makeup.Profiles);

            _visualBody.ApplyMarkings(uid, current.Markings);
        }
    }
}
