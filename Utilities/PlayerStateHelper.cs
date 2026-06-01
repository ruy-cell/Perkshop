using ProjectM;
using Stunlock.Core;
using Unity.Entities;

namespace PerkShop.Utilities;

internal static class PlayerStateHelper
{
    private static readonly PrefabGUID[] CombatBuffs =
    {
        new(581443919), // Buff_InCombat
        new(697095869), // Buff_InCombat_PvPVampire
        new(698151145)  // Buff_InCombat_Contest
    };

    public static bool Exists(Entity entity)
        => entity != Entity.Null && Core.EntityManager.Exists(entity);

    public static bool IsInCombat(Entity characterEntity)
    {
        var em = Core.EntityManager;
        if (!Exists(characterEntity)) return false;

        foreach (var buff in CombatBuffs)
            if (BuffUtility.HasBuff(em, characterEntity, buff))
                return true;

        return false;
    }
}
