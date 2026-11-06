using System.Numerics;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Cadia;

public static class CadiaObjectiveNames
{
    public static string Roman(int number)
    {
        if (number <= 0)
            return "?";
        var result = string.Empty;
        foreach (var (value, numeral) in new (int, string)[] { (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I") })
        {
            while (number >= value)
            {
                result += numeral;
                number -= value;
            }
        }
        return result;
    }
}
