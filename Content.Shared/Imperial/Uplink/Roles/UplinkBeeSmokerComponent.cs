using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>Пчелиный дымарь: делает пчёл вокруг союзниками.</summary>
[RegisterComponent]
public sealed partial class UplinkBeeSmokerComponent : Component
{
    [DataField]
    public float Range = 5f;
}
