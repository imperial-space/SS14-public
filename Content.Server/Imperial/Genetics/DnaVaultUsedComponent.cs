using System.Linq;
using Content.Server.Botany.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Examine;
using Content.Shared.Forensics.Components;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Components;
using Content.Shared.Imperial.Genetics.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Genetics;

/// <summary>Метка: существо уже получило улучшение ДНК-хранилища (TRAIT_USED_DNA_VAULT).</summary>
[RegisterComponent]
public sealed partial class DnaVaultUsedComponent : Component;
