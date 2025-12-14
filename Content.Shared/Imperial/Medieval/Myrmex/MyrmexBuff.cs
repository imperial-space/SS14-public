using Robust.Shared.Serialization;
namespace Content.Shared.Imperial.Medieval.Myrmex;

[Serializable, NetSerializable]
[DataDefinition]
public sealed partial class MyrmexBuff
{
    [DataField]
    public float Health;

    [DataField]
    public float Damage;

    [DataField]
    public float Stamina;

    internal MyrmexBuff(float health, float damage, float stamina)
    {
        Health = health;
        Damage = damage;
        Stamina = stamina;
    }

    public MyrmexBuff()
    {

    }

    public static MyrmexBuff MultiplyBuffs(List<MyrmexBuff> buffs)
    {
        var multiplied = new MyrmexBuff(1, 1, 1);

        foreach (var buff in buffs)
        {
            multiplied.Health *= buff.Health;
            multiplied.Damage *= buff.Damage;
            multiplied.Stamina *= buff.Stamina;
        }

        return multiplied;
    }
}
