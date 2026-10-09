using Content.Shared.Actions;

namespace Content.Shared.Imperial.Blob;

// Кнопки HUD оверманда SS13 (_onclick/hud/screen_objects/blob.dm) и клики (_onclick/overmind.dm).

/// <summary>jump_to_core: до размещения — «Place Blob Core».</summary>
public sealed partial class BlobJumpToCoreActionEvent : InstantActionEvent;

/// <summary>jump_to_node.</summary>
public sealed partial class BlobJumpToNodeActionEvent : InstantActionEvent;

/// <summary>blobbernaut: из фабрики под камерой.</summary>
public sealed partial class BlobCreateBlobbernautActionEvent : InstantActionEvent;

/// <summary>resource_blob.</summary>
public sealed partial class BlobCreateResourceActionEvent : InstantActionEvent;

/// <summary>node_blob.</summary>
public sealed partial class BlobCreateNodeActionEvent : InstantActionEvent;

/// <summary>factory_blob.</summary>
public sealed partial class BlobCreateFactoryActionEvent : InstantActionEvent;

/// <summary>readapt_strain.</summary>
public sealed partial class BlobReadaptStrainActionEvent : InstantActionEvent;

/// <summary>relocate_core.</summary>
public sealed partial class BlobRelocateCoreActionEvent : InstantActionEvent;

/// <summary>blobpop: носитель выпускает блоба.</summary>
public sealed partial class BlobPopActionEvent : InstantActionEvent;
