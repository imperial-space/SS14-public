using System.Diagnostics.CodeAnalysis;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Server.Traitor.Uplink;
using Content.Server.GameTicking;
using Content.Shared.PDA;
using Content.Shared.GameTicking.Components;
using Content.Server.Imperial.SyndieBattle.Components;
using Content.Shared.Imperial.SyndieBattle.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Imperial.SyndieBattle;

public sealed class SyndieBattleRedemptionSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly SyndieBattleRuleSystem _ruleSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SyndieBattleRedemptionComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(EntityUid uid, SyndieBattleRedemptionComponent component, InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(args.User, out var actor) ||
            !TryGetActiveScore(actor.PlayerSession.UserId, out var rule, out var score))
        {
            return;
        }

        if (!HasComp<PdaComponent>(args.Used) ||
            !HasComp<SyndieBattlePdaComponent>(args.Used) ||
            !TryComp<SyndieBattlePdaOwnerComponent>(args.Used, out var pdaOwner))
        {
            _popupSystem.PopupEntity(Loc.GetString("syndiebattle-redemption-no-pda"), uid, args.User);
            return;
        }

        if (!HasComp<UplinkComponent>(args.Used))
        {
            _popupSystem.PopupEntity(Loc.GetString("syndiebattle-redemption-no-uplink"), uid, args.User);
            return;
        }

        if (actor.PlayerSession.UserId == pdaOwner.UserId)
        {
            _popupSystem.PopupEntity(Loc.GetString("syndiebattle-redemption-own-pda"), uid, args.User);
            return;
        }

        if (!pdaOwner.RedemptionAllowed)
        {
            _popupSystem.PopupEntity(Loc.GetString("syndiebattle-redemption-repeat-kill"), uid, args.User);
            return;
        }

        if (!_ruleSystem.TryRedeemPda(rule, score, (args.Used, pdaOwner), component, out var payout))
        {
            _popupSystem.PopupEntity(Loc.GetString("syndiebattle-redemption-error"), uid, args.User);
            return;
        }

        args.Handled = true;
        score.RedeemedPdas++;
        score.RedeemedTelecrystals += payout;
        QueueDel(args.Used);

        _popupSystem.PopupEntity(Loc.GetString("syndiebattle-redemption-success", ("amount", payout)), uid, args.User);
    }

    private bool TryGetActiveScore(
        NetUserId userId,
        [NotNullWhen(true)] out SyndieBattleRuleComponent? activeRule,
        [NotNullWhen(true)] out SyndieBattlePlayerScore? score)
    {
        var query = EntityQueryEnumerator<SyndieBattleRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var rule, out var gameRule))
        {
            if (!_gameTicker.IsGameRuleActive(uid, gameRule) ||
                !rule.Active ||
                !rule.PlayerScores.TryGetValue(userId, out score))
            {
                continue;
            }

            activeRule = rule;
            return true;
        }

        activeRule = null;
        score = null;
        return false;
    }
}
