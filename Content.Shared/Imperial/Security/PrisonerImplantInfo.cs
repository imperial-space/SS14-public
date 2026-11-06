using Content.Shared.Actions;
using Content.Shared.DragDrop;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

[Serializable, NetSerializable]
public sealed class PrisonerImplantInfo
{
    public NetEntity Implant;
    public string Category = string.Empty;
    public List<string> InfoKeys = new();
    public List<string> InfoValues = new();
    public bool CanInject;
    public bool CanWarn;
}
