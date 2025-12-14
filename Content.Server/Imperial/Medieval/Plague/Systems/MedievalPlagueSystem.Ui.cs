using System.Linq;
using Content.Shared.Chat;
using Content.Shared.Imperial.Medieval.Plague;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Plague;

public sealed partial class MedievalPlagueSystem
{
    private SummaryPlagueData _data = new();

    private void InitializeUi()
    {
        SubscribeLocalEvent<MedievalPlagueGhostComponent, OpenPlagueEvolutionMenuActionEvent>(OnOpenMenu);

        SubscribeNetworkEvent<AddPlaguePointsMessage>(OnAddPoints);
    }

    private void OnOpenMenu(EntityUid uid, MedievalPlagueGhostComponent comp, OpenPlagueEvolutionMenuActionEvent args)
    {
        var ev = new OpenPlagueMenuMessage(_symptoms, GetData(), comp.Points);
        RaiseNetworkEvent(ev, uid);
    }

    private void OnAddPoints(AddPlaguePointsMessage args)
    {
        var uid = GetEntity(args.Ent);
        var comp = Comp<MedievalPlagueGhostComponent>(uid);

        var data = _symptoms.GetOrNew(args.Proto);
        if (data.Unlocked || comp.Points < args.Points)
            return;

        data.Points += args.Points;
        comp.Points -= args.Points;
        Dirty(uid, comp);
        _alerts.ShowAlert(uid, comp.AlertId);

        var proto = _proto.Index(args.Proto);

        if (data.Points >= proto.GetCost(_data))
        {
            data.Unlocked = true;
            DoPrototypeEffects(args.Proto);
            UpdateInfectAction();

            _data.Symptoms++;
            _data.Tier = Math.Max(_data.Tier, proto.Tier);

            var filter = Filter.Empty().AddWhereAttachedEntity(x => HasComp<MedievalPlagueGhostComponent>(x));
            var msg = Loc.GetString("medieval-plague-symptom-unlocked", ("symptom", Loc.GetString(proto.Name)));

            _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/Medieval/Plague/slime_node.ogg"), filter, true);
            _chat.ChatMessageToManyFiltered(filter, ChatChannel.Local,
                msg, msg, EntityUid.Invalid, false, true, Color.Crimson);
        }

        UpdateUi();
    }

    private void UpdateUi(EntityUid uid)
    {
        if (!TryComp<MedievalPlagueGhostComponent>(uid, out var comp))
            return;
        if (!_player.TryGetSessionByEntity(uid, out var session))
            return;

        var message = new PopulatePlagueMenuMessage(_symptoms, GetData(), comp.Points);
        RaiseNetworkEvent(message, session);
    }

    private void UpdateUi()
    {
        var ghosts = EntityManager.AllEntities<MedievalPlagueGhostComponent>();
        foreach (var item in ghosts)
        {
            if (!_player.TryGetSessionByEntity(item, out var session))
                continue;

            var message = new PopulatePlagueMenuMessage(_symptoms, GetData(), item.Comp.Points);
            RaiseNetworkEvent(message, session);
        }
    }

    public SummaryPlagueData GetData()
    {
        return _data;
    }
}
