using System;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace PerkShop.Utilities;

internal static class InventoryHelper
{
    public static int Count(Entity characterEntity, PrefabGUID itemPrefab)
    {
        var em = Core.EntityManager;
        if (!InventoryUtilities.TryGetInventoryEntity(em, characterEntity, out var inv)) return 0;
        if (!em.HasComponent<InventoryBuffer>(inv)) return 0;

        var buffer = em.GetBuffer<InventoryBuffer>(inv);
        int total = 0;
        for (int i = 0; i < buffer.Length; i++)
            if (buffer[i].ItemType.GuidHash == itemPrefab.GuidHash)
                total += buffer[i].Amount;
        return total;
    }

    public static bool TryRemove(Entity characterEntity, PrefabGUID itemPrefab, int amount)
    {
        var em = Core.EntityManager;
        if (!InventoryUtilities.TryGetInventoryEntity(em, characterEntity, out var inv)) return false;
        if (!em.HasComponent<InventoryBuffer>(inv)) return false;

        var buffer = em.GetBuffer<InventoryBuffer>(inv);
        int remaining = amount;
        for (int i = buffer.Length - 1; i >= 0 && remaining > 0; i--)
        {
            var slot = buffer[i];
            if (slot.ItemType.GuidHash != itemPrefab.GuidHash || slot.Amount <= 0) continue;
            int take = math.min(slot.Amount, remaining);
            slot.Amount -= take;
            remaining -= take;
            if (slot.Amount <= 0)
            {
                slot.ItemType = new PrefabGUID(0);
                slot.Amount = 0;
            }
            buffer[i] = slot;
        }
        return remaining == 0;
    }
}
