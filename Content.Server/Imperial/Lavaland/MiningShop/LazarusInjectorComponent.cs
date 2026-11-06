using Robust.Shared.Audio;

namespace Content.Server.Imperial.Lavaland.MiningShop;

/// <summary>
/// Инжектор Лазаря (item/lazarus_injector из SS13): один раз воскрешает мёртвое животное и делает его другом.
/// После ЭМИ — неисправен: воскрешённый зверь враждебен всем.
/// </summary>
[RegisterComponent]
public sealed partial class LazarusInjectorComponent : Component
{
    [DataField] public bool Loaded = true;
    [DataField] public bool Malfunctioning;
    [DataField] public SoundSpecifier UseSound = new SoundPathSpecifier("/Audio/Effects/refill.ogg");
}
