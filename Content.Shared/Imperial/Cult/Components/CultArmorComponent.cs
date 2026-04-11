using Robust.Shared.GameObjects;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>
/// Маркер культового доспеха. Наличие этого компонента на предмете одежды
/// активирует магический щит на носителе при надевании.
/// </summary>
[RegisterComponent]
public sealed partial class CultArmorComponent : Component
{
	/// <summary>
	/// Текущее число зарядов щита, сохранённое на самом доспехе.
	/// Это предотвращает баг с восстановлением щита при переэкипировке.
	/// </summary>
	[DataField]
	public int StoredShieldCharges = 3;
}
