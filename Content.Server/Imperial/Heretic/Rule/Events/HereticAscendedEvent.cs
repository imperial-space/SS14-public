using System.Linq;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Imperial.Heretic.Objectives;
using Content.Server.Imperial.Heretic.Reality;
using Content.Server.Mind;
using Content.Server.RoundEnd;
using Content.Shared.GameTicking.Components;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Heretic.Rule;

/// <summary>Raised on the server when a heretic completes their ascension ritual.</summary>
public sealed class HereticAscendedEvent : EntityEventArgs
{
    public EntityUid Heretic;
    public HereticAscendedEvent(EntityUid heretic) => Heretic = heretic;
}
