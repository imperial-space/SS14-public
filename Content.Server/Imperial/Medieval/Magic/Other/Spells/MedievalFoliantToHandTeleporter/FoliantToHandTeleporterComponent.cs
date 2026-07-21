namespace Content.Server.Imperial.Medieval.Magic.MedievalFoliantToHandTeleporter;
/// <summary>
/// Компонент телепортирует предмет по UID к пользователю в свободную руку при активации триггера.
/// </summary>
/// <remarks>
/// Вообще планируется использовать этот компонент для телепорта не только гримуара, но и других предметов.
/// </remarks>
[RegisterComponent, Access(typeof(FoliantToHandTeleporterSystem))]
public sealed partial class FoliantToHandTeleporterComponent : Component
{
    [ViewVariables]
    public EntityUid? ItemUid;

    [DataField]
    public string? KeyIn = "UseInHand";
}
