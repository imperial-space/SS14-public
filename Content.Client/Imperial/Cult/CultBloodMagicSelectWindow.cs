using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Окно выбора заклинания кровавой магии.
/// </summary>
public sealed class CultBloodMagicSelectWindow : DefaultWindow
{
    public event Action<string>? OnSpellSelected;

    private static readonly (string LocKey, string SpellId)[] Spells =
    {
        ("cult-spell-stun",               "ActionCultStun"),
        ("cult-spell-shackles",           "ActionCultShackles"),
        ("cult-spell-teleport",           "ActionCultTeleport"),
        ("cult-spell-emp",                "ActionCultEmp"),
        ("cult-spell-twisted-construction","ActionCultTwistedConstruction"),
        ("cult-spell-summon-dagger",      "ActionCultSummonDagger"),
        ("cult-spell-summon-equipment",   "ActionCultSummonEquipment"),
        ("cult-spell-conceal-presence",   "ActionCultConcealPresence"),        ("cult-spell-blood-rites",        "ActionCultBloodRites"),
    };

    public CultBloodMagicSelectWindow()
    {
        Title = "Выберите заклинание:";
        MinSize = new Vector2(280, 400);

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        var vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6),
            SeparationOverride = 4,
        };

        foreach (var (locKey, spellId) in Spells)
        {
            var capturedId = spellId;
            var btn = new Button
            {
                Text = Loc.GetString(locKey),
                HorizontalExpand = true,
                MinSize = new Vector2(0, 32),
            };
            btn.OnPressed += _ =>
            {
                OnSpellSelected?.Invoke(capturedId);
                Close();
            };
            vbox.AddChild(btn);
        }

        scroll.AddChild(vbox);
        Contents.AddChild(scroll);
    }
}
