using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared.Mobs;
using Robust.Shared.Random;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>Убежище в катакомбах: горожанин, доведённый сюда, укрыт.</summary>
[RegisterComponent]
public sealed partial class CadiaRefugeeShelterComponent : Component
{
    [DataField]
    public float Radius = 6f;
}
