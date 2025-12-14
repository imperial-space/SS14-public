using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Systems;
using Content.Server.GameTicking.Rules;
using System.Linq;
using Content.Server.Chat.Systems;
using Robust.Shared.Random;
using Content.Server.MagicBarrier.Components;
using Robust.Shared.Timing;
using Robust.Shared.Map;
using Content.Server.Imperial.Medieval.SkeletonInvasion;
using Content.Shared.Humanoid;
using Content.Server.Imperial.Medieval.Boss;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.EntitySerialization;
using Content.Server.GameTicking;
using Content.Server.Flash;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Utility;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Server.Storage.EntitySystems;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Robust.Shared.Player;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Imperial.Medieval.Plague;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

public sealed class MedievalPlagueRuleSystem : GameRuleSystem<MedievalPlagueRuleComponent>
{
    [Dependency] private readonly MedievalPlagueSystem _plague = default!;

    protected override void AppendRoundEndText(EntityUid uid, MedievalPlagueRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        var stats = _plague.GetData();

        args.AddLine(Loc.GetString("medieval-plague-round-end-infected-count", ("count", stats.Infected)));
        args.AddLine(Loc.GetString("medieval-plague-round-end-immune-count", ("count", stats.Immune)));
        args.AddLine(Loc.GetString("medieval-plague-round-end-plague-tier", ("tier", stats.Tier)));
        args.AddLine(Loc.GetString("medieval-plague-round-end-symptoms-count", ("count", stats.Symptoms)));
    }
}
