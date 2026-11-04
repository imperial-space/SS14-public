using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.BSA;

/// <summary>«Complete Deployment»: собрать пушку из привязанных частей.</summary>
[NetSerializable, Serializable]
public sealed class BSABuildMessage : BoundUserInterfaceMessage;
