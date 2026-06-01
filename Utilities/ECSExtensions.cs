using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Unity.Entities;

namespace PerkShop;

public static class ECSExtensions
{
    public unsafe static T Read<T>(this Entity entity) where T : struct
    {
        var ct = new ComponentType(Il2CppType.Of<T>());
        void* raw = Core.EntityManager.GetComponentDataRawRO(entity, ct.TypeIndex);
        return Marshal.PtrToStructure<T>(new IntPtr(raw));
    }
}
