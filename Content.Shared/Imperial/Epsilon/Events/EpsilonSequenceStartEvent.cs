using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Epsilon;

/// <summary>
/// Протокол «Эпсилон»: сервер сообщает клиентам момент старта, клиент сам играет трек
/// и печатает надписи синхронно с ним.
/// </summary>
[Serializable, NetSerializable]
public sealed class EpsilonSequenceStartEvent(TimeSpan start) : EntityEventArgs
{
    public TimeSpan Start = start;
}
