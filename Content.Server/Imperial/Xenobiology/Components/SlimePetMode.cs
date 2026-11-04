namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>Режим команд питомца (pet_command): idle/free/follow/attack.</summary>
public enum SlimePetMode : byte
{
    Free,
    Stay,
    Follow,
    Attack,
}
