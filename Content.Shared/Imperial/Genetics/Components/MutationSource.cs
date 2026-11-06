using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Genetics.Components;

[Flags]
public enum MutationSource : byte
{
    None = 0,

    /// <summary>Ген активирован в собственном геноме (секвенсор, активатор).</summary>
    Activated = 1 << 0,

    /// <summary>Добавлена мутатором поверх генома — тратит стабильность.</summary>
    Mutator = 1 << 1,

    Admin = 1 << 2,

    /// <summary>Награда ДНК-хранилища: не снимается мутадоном, срывом и перемешиванием.</summary>
    Vault = 1 << 3,
}
