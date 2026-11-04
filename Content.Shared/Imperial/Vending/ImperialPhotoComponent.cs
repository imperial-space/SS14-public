using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>/obj/item/photo TG: снимок, подпись на обороте.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ImperialPhotoComponent : Component
{
    /// <summary>PNG снимка; на клиент уходит только через окно просмотра.</summary>
    [ViewVariables]
    public byte[] Image = Array.Empty<byte>();

    /// <summary>Миниатюра 8×8 RGBA для значка (get_small_icon).</summary>
    [DataField, AutoNetworkedField]
    public byte[] Thumbnail = Array.Empty<byte>();

    /// <summary>scribble: надпись на обороте.</summary>
    [DataField, AutoNetworkedField]
    public string? Scribble;

    /// <summary>picture.mobs_seen: кто попал в кадр.</summary>
    [ViewVariables]
    public List<EntityUid> MobsSeen = new();

    /// <summary>picture.dead_seen: кто попал в кадр мёртвым.</summary>
    [ViewVariables]
    public List<EntityUid> DeadSeen = new();
}
