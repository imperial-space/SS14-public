using Content.Shared.Imperial.Cadia;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cadia;

/// <summary>Спавнер призрачной роли войны: какую роль он выдаёт.</summary>
[RegisterComponent]
public sealed partial class CadiaRoleSpawnerComponent : Component
{
    [DataField]
    public ProtoId<CadiaRolePrototype> Role;
}
