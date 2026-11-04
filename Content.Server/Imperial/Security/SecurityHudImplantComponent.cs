using Content.Server.GameTicking;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Chat;
using Content.Shared.EntityEffects;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Security;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Implants;
using Content.Shared.Inventory;
using Content.Shared.Overlays;
using Content.Shared.Popups;
using Content.Shared.StatusEffect;
using Content.Shared.Storage.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Security;

/// <summary>organ/cyberimp/eyes/hud/security: HUD СБ без очков.</summary>
[RegisterComponent]
public sealed partial class SecurityHudImplantComponent : Component
{
    /// <summary>Какие HUD-компоненты добавил сам имплант (остальные у носителя уже были).</summary>
    [ViewVariables]
    public List<string> Added = new();
}
