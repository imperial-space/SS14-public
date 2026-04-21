using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cult;

// ─────────────────── Blood Spell Actions ────────────────────

/// <summary>Commune — broadcast via cult chat.</summary>
public sealed partial class CultCommuneActionEvent : InstantActionEvent { }

/// <summary>Stun spell — spawns a spell item in the caster's hand.</summary>
public sealed partial class CultStunActionEvent : InstantActionEvent { }

/// <summary>Shadow Shackles — spawns a spell item in the caster's hand.</summary>
public sealed partial class CultShacklesActionEvent : InstantActionEvent { }

/// <summary>Teleport — teleports cultist to a teleport rune.</summary>
public sealed partial class CultTeleportActionEvent : InstantActionEvent { }

/// <summary>EMP — large area electromagnetic pulse.</summary>
public sealed partial class CultEmpActionEvent : InstantActionEvent { }

/// <summary>Twisted Construction — spawns a spell item in the caster's hand.</summary>
public sealed partial class CultTwistedConstructionActionEvent : InstantActionEvent { }

/// <summary>Summon Dagger — spawns a new ritual dagger in hand.</summary>
public sealed partial class CultSummonDaggerActionEvent : InstantActionEvent { }

/// <summary>Summon Equipment — full cult combat gear.</summary>
public sealed partial class CultSummonEquipmentActionEvent : InstantActionEvent { }

/// <summary>Conceal Presence — hide/reveal nearby runes and structures.</summary>
public sealed partial class CultConcealPresenceActionEvent : InstantActionEvent { }

/// <summary>Blood Rites — multi-function blood gathering/healing/attack.</summary>
public sealed partial class CultBloodRitesActionEvent : InstantActionEvent { }

/// <summary>Recall active blood spear to hand.</summary>
public sealed partial class CultRecallBloodSpearActionEvent : InstantActionEvent { }

[Serializable, NetSerializable]
public enum CultBloodRitesMode : byte
{
    Gather = 0,
    Heal = 1,
    Recharge = 2,
    Orb = 3,
    Spear = 4,
}

// ─────────────────── Dark Spirit Actions ────────────────────

/// <summary>Dark Spirit: return to the original body (100 stamina damage on landing).</summary>
public sealed partial class CultDarkSpiritReturnActionEvent : InstantActionEvent { }

/// <summary>Dark Spirit: commune with the cult.</summary>
public sealed partial class CultDarkSpiritCommuneActionEvent : InstantActionEvent { }

// ─────────────────── Rune Drawing ────────────────────

/// <summary>Fired after dagger DoAfter completes to actually spawn a rune.</summary>
[Serializable, NetSerializable]
public sealed partial class DrawRuneDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public string? StringData;

    /// <summary>Player-provided label for teleport runes.</summary>
    [DataField]
    public string? RuneLabel;
}

// ─────────────────── UI / Selection ────────────────────

[Serializable, NetSerializable]
public enum CultRuneDrawBuiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CultSelectRuneMessage : BoundUserInterfaceMessage
{
    public string RuneId { get; }
    public string? Label { get; }
    public CultSelectRuneMessage(string runeId, string? label = null) { RuneId = runeId; Label = label; }
}

[Serializable, NetSerializable]
public sealed class CultTeleportSelectMessage : BoundUserInterfaceMessage
{
    public NetEntity RuneEntity { get; }
    public CultTeleportSelectMessage(NetEntity runeEntity) => RuneEntity = runeEntity;
}

//  Blood Magic 

/// <summary>Blood Magic  opens spell selection window.</summary>
public sealed partial class CultBloodMagicActionEvent : InstantActionEvent { }

/// <summary>Fired after spell preparation DoAfter completes.</summary>
[Serializable, NetSerializable]
public sealed partial class PrepareSpellDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public string? SpellId;
}

[Serializable, NetSerializable]
public enum CultBloodMagicBuiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CultSpellSelectedMessage : BoundUserInterfaceMessage
{
    public string SpellId { get; }
    public CultSpellSelectedMessage(string spellId) => SpellId = spellId;
}

[Serializable, NetSerializable]
public sealed class CultBloodMagicSelectState : BoundUserInterfaceState;

[Serializable, NetSerializable]
public sealed class CultBloodMagicSwapState : BoundUserInterfaceState
{
    public string NewSpellId { get; }
    public List<string> PreparedSpells { get; }
    public CultBloodMagicSwapState(string newSpellId, List<string> preparedSpells)
    {
        NewSpellId = newSpellId;
        PreparedSpells = new List<string>(preparedSpells);
    }
}

[Serializable, NetSerializable]
public sealed class CultSpellSwapMessage : BoundUserInterfaceMessage
{
    public string OldSpellId { get; }
    public string NewSpellId { get; }
    public CultSpellSwapMessage(string oldSpellId, string newSpellId)
    {
        OldSpellId = oldSpellId;
        NewSpellId = newSpellId;
    }
}

// ─── Commune ─────────────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public enum CultCommuneBuiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CultCommuneTextMessage : BoundUserInterfaceMessage
{
    public string Text { get; }
    public CultCommuneTextMessage(string text) => Text = text;
}

// ─── Teleport BUI ─────────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public enum CultTeleportBuiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CultTeleportBuiState : BoundUserInterfaceState
{
    public List<(NetEntity Entity, string Tag)> Runes { get; }
    public CultTeleportBuiState(List<(NetEntity, string)> runes) => Runes = runes;
}

[Serializable, NetSerializable]
public enum CultBloodRitesBuiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CultBloodRitesBuiState : BoundUserInterfaceState
{
    public CultBloodRitesMode SelectedMode { get; }
    public int Charges { get; }

    public CultBloodRitesBuiState(CultBloodRitesMode selectedMode, int charges)
    {
        SelectedMode = selectedMode;
        Charges = charges;
    }
}

[Serializable, NetSerializable]
public sealed class CultBloodRitesChoiceMessage : BoundUserInterfaceMessage
{
    public CultBloodRitesMode Mode { get; }

    public CultBloodRitesChoiceMessage(CultBloodRitesMode mode)
    {
        Mode = mode;
    }
}


//  Runed Metal Construction BUI 

[Serializable, NetSerializable]
public enum CultConstructionBuiKey
{
    Key,
}

// ─── Structure Product BUI (Altar / Forge / Archives) ────────────────────────

[Serializable, NetSerializable]
public enum CultStructureBuiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CultStructureBuiState : BoundUserInterfaceState
{
    public List<(string ItemId, string LocKey)> Items { get; }

    public CultStructureBuiState(List<(string, string)> items) => Items = items;
}

[Serializable, NetSerializable]
public sealed class CultStructureCreateMessage : BoundUserInterfaceMessage
{
    public string ItemId { get; }

    public CultStructureCreateMessage(string itemId) => ItemId = itemId;
}

// ─── Spirit Realm BUI ────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public enum CultSpiritRealmBuiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CultSpiritRealmChoiceMessage : BoundUserInterfaceMessage
{
    /// <summary>true = Summon Homunculi, false = Ascend as Dark Spirit</summary>
    public bool IsHomunculi { get; }
    public CultSpiritRealmChoiceMessage(bool isHomunculi) => IsHomunculi = isHomunculi;
}
