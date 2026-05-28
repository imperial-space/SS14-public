using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.XxRaay.Android;

/// <summary>
/// Сообщение от клиента с выбором: принять или отклонить передачу девиантности.
/// </summary>
[Serializable, NetSerializable]
public sealed class AndroidDeviantConsentChoiceMessage : BoundUserInterfaceMessage
{
    public readonly bool Accepted;

    public AndroidDeviantConsentChoiceMessage(bool accepted)
    {
        Accepted = accepted;
    }
}

