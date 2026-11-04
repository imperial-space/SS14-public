using System.Linq;
using System.Text;
using Content.Server.Chat.Managers;
using Content.Shared.Alert;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Mood;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Drunk;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Mood;

public static class ImperialMood
{
    public const int Happy4 = 15, Happy3 = 10, Happy2 = 6, Happy1 = 2, Sad1 = -3, Sad2 = -7, Sad3 = -15, Sad4 = -20;
    public const int LevelNeutral = 5;
    public const float SanityMaximum = 150, SanityGreat = 125, SanityNeutral = 100, SanityDisturbed = 75, SanityUnstable = 50, SanityCrazy = 25, SanityInsane = 0;

    /// <summary>RUN_DELAY TG: замедления рассудка прибавляются к нему.</summary>
    public const float RunDelay = 2.2f;
}
