using System.Linq;
using System.Numerics;
using Content.Client.Message;
using Content.Shared.Imperial.Languages;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Languages;

/// <summary>Открывает меню языков по просьбе сервера и шлёт выбор.</summary>
public sealed class ImperialLanguageMenuSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;

    private ImperialLanguageMenu? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ImperialLanguageMenuOpenMessage>(OnOpen);
        SubscribeLocalEvent<ImperialLanguageSpeakerComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnOpen(ImperialLanguageMenuOpenMessage msg)
    {
        if (_window != null)
        {
            _window.Close();
            return;
        }

        _window = new ImperialLanguageMenu(id => RaiseNetworkEvent(new ImperialLanguageSelectMessage(id)));
        _window.OnClose += () => _window = null;
        Refresh();
        _window.OpenCentered();
    }

    private void OnState(Entity<ImperialLanguageSpeakerComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Owner == _player.LocalEntity)
            Refresh();
    }

    private void Refresh()
    {
        if (_window == null || _player.LocalEntity is not { } local || !TryComp<ImperialLanguageSpeakerComponent>(local, out var speaker))
            return;

        _window.Update((local, speaker));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
    }
}
