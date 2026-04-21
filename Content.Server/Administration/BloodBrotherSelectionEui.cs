using System.Linq;
using Content.Server.EUI;
using Content.Server.GameTicking.Rules;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server.Administration;

public sealed class BloodBrotherSelectionEui : BaseEui
{
    private readonly ICommonSession _targetPlayer;
    private readonly string _targetName;
    private readonly List<BloodBrotherSelectablePlayer> _candidates;
    private readonly BloodBrotherRuleSystem _bloodBrother;

    public BloodBrotherSelectionEui(
        ICommonSession targetPlayer,
        string targetName,
        List<BloodBrotherSelectablePlayer> candidates,
        BloodBrotherRuleSystem bloodBrother)
    {
        _targetPlayer = targetPlayer;
        _targetName = targetName;
        _candidates = candidates;
        _bloodBrother = bloodBrother;
    }

    public override EuiStateBase GetNewState()
    {
        return new BloodBrotherSelectionEuiState(_targetName, _candidates);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not BloodBrotherSelectionChoiceMessage choice)
            return;

        var teammate = _candidates.FirstOrDefault(candidate => candidate.UserId == choice.UserId);
        if (teammate != null && IoCManager.Resolve<IPlayerManager>().TryGetSessionById(teammate.UserId, out var session))
            _bloodBrother.TryMakeBloodBrotherPair(_targetPlayer, session);

        Close();
    }
}