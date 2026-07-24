using System.Linq;
using Content.Client.Imperial.DeimonFly.DeathNote.UI;
using Content.Shared.Imperial.DeimonFly.DeathNote.UI;

namespace Content.Client.Imperial.DeimonFly.DeathNote.Systems;

/// <summary>
/// Открывает только адресованные этому клиенту ролевые предписания.
/// </summary>
public sealed class DeathNoteInfluenceSystem : EntitySystem
{
    private readonly List<DeathNoteInfluenceWindow> _windows = new();

    [ViewVariables]
    public int ReceivedInfluenceCount { get; private set; }

    [ViewVariables]
    public string? LastScenario { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DeathNoteInfluenceMessage>(OnInfluence);
    }

    public override void Shutdown()
    {
        foreach (var window in _windows.ToArray())
            window.Close();

        _windows.Clear();
        base.Shutdown();
    }

    private void OnInfluence(DeathNoteInfluenceMessage message)
    {
        ReceivedInfluenceCount++;
        LastScenario = message.Scenario;
        var window = new DeathNoteInfluenceWindow();
        window.SetScenario(message.Scenario, message.IsCustomRoleplay);
        window.OnClose += () => _windows.Remove(window);
        _windows.Add(window);
        window.OpenCentered();
    }
}
