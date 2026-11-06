using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Пинпоинтер экипажа (pinpointer/crew): выбор цели из членов экипажа с включёнными датчиками.</summary>
[RegisterComponent]
public sealed partial class ImperialCrewPinpointerComponent : Component;
