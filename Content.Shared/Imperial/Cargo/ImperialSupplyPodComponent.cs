using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

/// <summary>/obj/structure/closet/supplypod: капсула снабжения.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialSupplyPodComponent : Component
{
    /// <summary>bluespace: после выгрузки исчезает в искрах.</summary>
    [DataField, AutoNetworkedField]
    public bool Bluespace;

    /// <summary>delays[POD_FALLING], секунды.</summary>
    [DataField]
    public float Falling = 0.4f;

    /// <summary>delays[POD_OPENING].</summary>
    [DataField]
    public float Opening = 3f;

    /// <summary>delays[POD_LEAVING].</summary>
    [DataField]
    public float Leaving = 3f;

    /// <summary>explosionSize: лёгкий радиус и вспышка (0,0,2,3 у обычной, 0,0,1,2 у блюспейс).</summary>
    [DataField]
    public int LightRange = 2;

    [DataField]
    public int FlashRange = 3;

    /// <summary>Угол прилёта (70–110°), задаётся при запуске.</summary>
    [DataField, AutoNetworkedField]
    public float Angle = 90;

    /// <summary>Когда капсула коснётся земли (для анимации падения на клиенте).</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan LandTime;

    [ViewVariables]
    public TimeSpan? OpenAt;

    [ViewVariables]
    public TimeSpan? LeaveAt;

    [ViewVariables]
    public bool Landed;
}
