using System.Linq;
using Content.Shared.Imperial.Medieval.Plague;
using Robust.Client.Audio;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Plague.UI;

public sealed class MedievalPlagueUiController : UIController
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;

    private MedievalPlagueMenu? _menu;
    private MedievalPlagueSymptomMiniMenu? _miniMenu;

    private SoundSpecifier _openSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/Plague/menu_open.ogg");
    private SoundSpecifier _closeSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/Plague/menu_close.ogg");

    public void ToggleMenu(Dictionary<ProtoId<MedievalPlagueSymptomPrototype>, MedievalPlagueSymptomData> data, SummaryPlagueData info, int allowedPoints)
    {
        if (_menu != null)
        {
            _menu.Close();
            return;
        }

        _menu = new();
        _menu.OnSymptomSelect = args => SelectSymptom(args, data, info, allowedPoints);
        _menu.InfoPressed = () => OpenInfo(info);

        var audio = EntityManager.System<AudioSystem>();

        _menu.OnClose += () =>
        {
            _miniMenu?.Close();
            _miniMenu = null;
            _menu = null;

            if (_player.LocalSession != null)
                audio.PlayGlobal(_closeSound, _player.LocalSession);
        };

        _menu.Populate(_proto, data, info);
        _menu.OpenCentered();

        if (_player.LocalSession != null)
            audio.PlayGlobal(_openSound, _player.LocalSession);
    }

    public void Populate(Dictionary<ProtoId<MedievalPlagueSymptomPrototype>, MedievalPlagueSymptomData> data, SummaryPlagueData info, int allowedPoints)
    {
        if (_menu != null)
        {
            _menu.Populate(_proto, data, info);
            _menu.OnSymptomSelect = args => SelectSymptom(args, data, info, allowedPoints);
            _menu.InfoPressed = () => OpenInfo(info);
        }

        if (_miniMenu != null)
        {
            if (_miniMenu.Info)
            {
                _miniMenu.PopulateAsInfo(info);
                return;
            }

            var proto = _proto.Index(_miniMenu.Proto);
            var points = proto.GetCost(info);

            if (data.TryGetValue(proto.ID, out var dat))
                points -= dat.Points;

            if (!ReqsMet(proto, data))
                points = -1;

            points = Math.Clamp(points, -1, allowedPoints);

            _miniMenu.Populate(proto, points);
        }
    }

    private void SelectSymptom(ProtoId<MedievalPlagueSymptomPrototype> protoId, Dictionary<ProtoId<MedievalPlagueSymptomPrototype>, MedievalPlagueSymptomData> data, SummaryPlagueData info, int allowedPoints)
    {
        if (_miniMenu == null)
        {
            _miniMenu = new();
            _miniMenu.OnClose += () => _miniMenu = null;
            _miniMenu.OpenCenteredRight();
        }

        var proto = _proto.Index(protoId);
        var points = proto.GetCost(info);
        if (data.TryGetValue(proto.ID, out var dat))
            points -= dat.Points;

        if (!ReqsMet(proto, data))
            points = -1;

        points = Math.Clamp(points, -1, allowedPoints);

        _miniMenu.Populate(proto, points);
        var ent = EntityManager.GetNetEntity(_player.LocalEntity);
        if (!ent.HasValue)
            return;

        _miniMenu.OnAddPoints = (_, args) => EntityManager.RaisePredictiveEvent(new AddPlaguePointsMessage(proto, args, ent.Value));
    }

    private void OpenInfo(SummaryPlagueData info)
    {
        if (_miniMenu == null)
        {
            _miniMenu = new();
            _miniMenu.OnClose += () => _miniMenu = null;
            _miniMenu.OpenCenteredRight();
        }

        _miniMenu.PopulateAsInfo(info);
    }

    private bool ReqsMet(MedievalPlagueSymptomPrototype proto, Dictionary<ProtoId<MedievalPlagueSymptomPrototype>, MedievalPlagueSymptomData> dict)
    {
        var unlocked = dict.Where(x => x.Value.Unlocked && proto.Required.ToList().Contains(x.Key));
        return unlocked.Count() == proto.Required.Count();
    }
}
