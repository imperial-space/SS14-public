using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Server.Radio;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>Оружие бойца под «огнём на подавление».</summary>
[RegisterComponent]
public sealed partial class CadiaSuppressedGunComponent : Component;
