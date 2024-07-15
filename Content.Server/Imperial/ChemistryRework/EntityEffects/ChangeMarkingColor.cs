using Content.Server.Humanoid;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Chemistry.ReactionEffects;

/// <summary>
///     Changes the mark of the entity layer. Also able to change the color of her skin.
/// </summary>

[DataDefinition]
public sealed partial class ChangeMarkingColor : EntityEffect
{
    /// <summary>
    ///     Includes all enums from <see cref="Shared.Humanoid.Markings.MarkingCategories"/> and Skin.
    /// </summary>
    [DataField("markingCategory")]
    public string MarkingCategory = "Hair";

    /// <summary>
    ///     This option ignores the <see cref="PaintingColor"/> parameter and inverts the color.
    /// </summary>
    [DataField("invertColor")]
    public bool InvertColor = false;

    /// <summary>
    ///    If no color was passed in and <see cref="InvertColor"/> is set to false, then generates a random color every metabolization cycle.
    /// </summary>
    [DataField("color")]
    public string? PaintingСolor;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) =>
        Loc.GetString("reagent-effect-guidebook-change-marking-color",
            ("chance", Probability),
            ("category", MarkingCategory)
        );

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (!Enum.TryParse(MarkingCategory, out Shared.Humanoid.Markings.MarkingCategories marking) && MarkingCategory != "Skin") return;

        var humSystem = args.EntityManager.System<HumanoidAppearanceSystem>();
        var color = InvertColor ? InvertMarkingColor(args, marking) : GenerateColor();

        if (MarkingCategory == "Skin")
            humSystem.SetSkinColor(args.TargetEntity, color);
        else
            humSystem.SetMarkingColor(args.TargetEntity, marking, 0, new List<Color> { color });
    }

    private Color GenerateColor()
    {
        if (PaintingСolor != null) return Color.FromHex(PaintingСolor);

        var random = IoCManager.Resolve<IRobustRandom>();

        var r = random.NextByte(255);
        var g = random.NextByte(255);
        var b = random.NextByte(255);

        return new Color(r, g, b);
    }

    private Color InvertMarkingColor(EntityEffectBaseArgs args, Shared.Humanoid.Markings.MarkingCategories marking)
    {
        if (
            !args.EntityManager.TryGetComponent<HumanoidAppearanceComponent>(args.TargetEntity, out var hum)
        ) return GenerateColor();

        if (!hum.MarkingSet.TryGetCategory(marking, out var markings) || MarkingCategory == "Skin")
        {
            if (MarkingCategory != "Skin") return GenerateColor();

            return Invert(hum.SkinColor);
        }

        foreach (var mark in markings)
            return Invert(mark.MarkingColors[0]);

        return GenerateColor();
    }

    private static Color Invert(Color color)
    {
        if (!SkinColor.VerifySkinColor(HumanoidSkinColor.HumanToned, color)) return new Color(1 - color.R, 1 - color.G, 1 - color.B);

        return SkinColor.HumanSkinTone(
            Convert.ToInt32(
                100 - SkinColor.HumanSkinToneFromColor(color)
            )
        );
    }
}
