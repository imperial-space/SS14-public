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

/// <summary>Горожанин Касра: бродит по улицам, пока его не уведут в убежище.</summary>
[RegisterComponent]
public sealed partial class CadiaRefugeeComponent : Component;
