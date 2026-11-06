using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.Wizard;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Wizard;

/// <summary>Открывает окно выбора из списка по запросу сервера.</summary>
public sealed class ImperialListInputSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ImperialWizardListInputEvent>(OnInput);
    }

    private void OnInput(ImperialWizardListInputEvent ev)
    {
        var answered = false;
        var window = new ImperialListInputWindow(ev.Title, ev.Message, ev.Options);
        window.OnChoice += choice =>
        {
            answered = true;
            RaiseNetworkEvent(new ImperialWizardListChoiceEvent(ev.Id, choice));
            window.Close();
        };
        window.OnClose += () =>
        {
            if (!answered)
                RaiseNetworkEvent(new ImperialWizardListChoiceEvent(ev.Id, null));
        };
        window.OpenCentered();
    }
}
