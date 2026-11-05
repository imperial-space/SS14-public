using System.Linq;
using Content.Server.Electrocution;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Popups;
using Content.Shared.Slippery;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Fishing;

/// <summary>Рыба с электрогенезом даёт разряд. Аквариум-генератор забирает энергию и ставит Handled.</summary>
[ByRefEvent]
public record struct ImperialFishElectrogenesisEvent(Entity<ImperialFishComponent> Fish, float Power, bool Handled = false);
