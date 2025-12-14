using Content.Shared.Imperial.Medieval.Lycantropy;
using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Polymorph;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Lycantropy.UI;

public sealed class LycantropyUiController : UIController
{
    [Dependency] private readonly IPlayerManager _player = default!;

    private SelectWerewolfFormMenu? _formMenu;
    private LycantropyProgressMenu? _menu;

    public void ToggleFormMenu(Dictionary<string, ProtoId<PolymorphPrototype>> forms)
    {
        if (_formMenu != null)
        {
            _formMenu.Close();
            return;
        }

        _formMenu = new();
        _formMenu.Populate(forms);

        _formMenu.OnClose += () => _formMenu = null;
        _formMenu.OnSelected += args =>
        {
            if (!_player.LocalEntity.HasValue)
                return;

            EntityManager.RaisePredictiveEvent(new SelectWerewolfFormEvent(EntityManager.GetNetEntity(_player.LocalEntity.Value), args));
        };

        _formMenu.OpenCentered();
    }

    public void ToggleProgressMenu(List<ProtoId<LycantropyAbilityPrototype>> unlocked, int points)
    {
        if (_menu != null)
        {
            _menu.Close();
            _menu = null;
        }
        else
        {
            _menu = UIManager.CreateWindow<LycantropyProgressMenu>();
            _menu.Populate(unlocked, points);

            _menu.OnClose += () => _menu = null;
            _menu.BuyPressed += args =>
            {
                if (!_player.LocalEntity.HasValue)
                    return;

                EntityManager.RaisePredictiveEvent(new BuyLycantropyAbilityEvent(EntityManager.GetNetEntity(_player.LocalEntity.Value), args));
            };

            _menu.OpenCentered();
        }
    }

    public void Populate(List<ProtoId<LycantropyAbilityPrototype>> unlocked, int points)
    {
        _menu?.Populate(unlocked, points);
    }
}
