using Content.Shared.Imperial.Wizard;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Wizard;

/// <summary>tgui_input_list(): спросить у игрока пункт из списка и выполнить действие с ответом.</summary>
public sealed class ImperialListInputSystem : EntitySystem
{
    private readonly Dictionary<int, (ICommonSession Session, List<string> Options, Action<string> Callback)> _pending = new();
    private int _nextId;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ImperialWizardListChoiceEvent>(OnChoice);
    }

    public void Open(ICommonSession session, string title, string message, List<string> options, Action<string> callback)
    {
        if (options.Count == 0)
            return;

        var id = ++_nextId;
        _pending[id] = (session, options, callback);
        RaiseNetworkEvent(new ImperialWizardListInputEvent(id, title, message, options), session);
    }

    private void OnChoice(ImperialWizardListChoiceEvent ev, EntitySessionEventArgs args)
    {
        if (!_pending.TryGetValue(ev.Id, out var pending) || pending.Session != args.SenderSession)
            return;

        _pending.Remove(ev.Id);

        // Ответ приходит от клиента — принимаем только один из предложенных вариантов.
        if (ev.Choice == null || !pending.Options.Contains(ev.Choice))
            return;

        pending.Callback(ev.Choice);
    }
}
