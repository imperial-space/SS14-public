using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Окно замены заклинания — показывается когда все слоты заняты.
/// Игрок выбирает одно из уже подготовленных заклинаний для замены.
/// </summary>
public sealed class CultBloodMagicSwapWindow : DefaultWindow
{
    public event Action<string>? OnSwapSelected;

    private static readonly Dictionary<string, string> SpellLocKeys = new()
    {
        ["ActionCultStun"]                = "cult-spell-stun",
        ["ActionCultShackles"]            = "cult-spell-shackles",
        ["ActionCultTeleport"]            = "cult-spell-teleport",
        ["ActionCultEmp"]                 = "cult-spell-emp",
        ["ActionCultTwistedConstruction"] = "cult-spell-twisted-construction",
        ["ActionCultSummonDagger"]        = "cult-spell-summon-dagger",
        ["ActionCultSummonEquipment"]     = "cult-spell-summon-equipment",
        ["ActionCultConcealPresence"]     = "cult-spell-conceal-presence",
        ["ActionCultBloodRites"]          = "cult-spell-blood-rites",
    };

    public CultBloodMagicSwapWindow()
    {
        Title = Loc.GetString("cult-blood-magic-swap-title");
        MinSize = new Vector2(300, 200);
    }

    /// <summary>Заполняет окно кнопками подготовленных заклинаний.</summary>
    public void Populate(string newSpellId, List<string> preparedSpells)
    {
        Contents.RemoveAllChildren();

        var newSpellName = SpellLocKeys.TryGetValue(newSpellId, out var lk)
            ? Loc.GetString(lk)
            : newSpellId;

        var vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6),
            SeparationOverride = 4,
        };

        var label = new Label
        {
            Text = Loc.GetString("cult-blood-magic-swap-choose", ("spell", newSpellName)),
            HorizontalExpand = true,
        };
        vbox.AddChild(label);

        foreach (var spellId in preparedSpells)
        {
            var displayName = SpellLocKeys.TryGetValue(spellId, out var key)
                ? Loc.GetString(key)
                : spellId;

            var capturedId = spellId;
            var btn = new Button
            {
                Text = displayName,
                HorizontalExpand = true,
                MinSize = new Vector2(0, 32),
            };
            btn.OnPressed += _ =>
            {
                OnSwapSelected?.Invoke(capturedId);
                Close();
            };
            vbox.AddChild(btn);
        }

        Contents.AddChild(vbox);
    }
}
