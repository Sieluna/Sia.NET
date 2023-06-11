namespace Sia;

using System.Runtime.CompilerServices;

public record struct EntityRef(
    IntPtr Pointer, EntityDescriptor Descriptor)
{
    public unsafe EntityRef Create<TEntity>(ref TEntity entity)
        => new EntityRef {
            Pointer = (IntPtr)Unsafe.AsPointer(ref entity),
            Descriptor = EntityDescriptor.Get<TEntity>()
        };

    public unsafe EntityRef Create<TEntity>(TEntity* entity)
        where TEntity : unmanaged
        => new EntityRef {
            Pointer = (IntPtr)entity,
            Descriptor = EntityDescriptor.Get<TEntity>()
        };
}

public static class EntityRefExtensions
{
    public unsafe static ref TComponent Get<TComponent>(this EntityRef entityRef)
        where TComponent : unmanaged
    {
        if (!entityRef.Descriptor.TryGetOffset<TComponent>(out var offset)) {
            throw new ComponentNotFoundException("Component not found: " + typeof(TComponent));
        }
        return ref *(TComponent*)(entityRef.Pointer + offset);
    }

    public unsafe static void* UnsafeGet(this EntityRef entityRef, Type componentType, int componentTypeIndex)
    {
        if (!entityRef.Descriptor.UnsafeTryGetOffset(componentType, componentTypeIndex, out var offset)) {
            throw new ComponentNotFoundException("Component not found: " + componentType);
        }
        return (void*)(entityRef.Pointer + offset);
    }

    public unsafe static ref TComponent GetOrNullRef<TComponent>(this EntityRef entityRef)
        where TComponent : unmanaged
    {
        if (!entityRef.Descriptor.TryGetOffset<TComponent>(out var offset)) {
            return ref Unsafe.NullRef<TComponent>();
        }
        return ref *(TComponent*)(entityRef.Pointer + offset);
    }

    public unsafe static void* UnsafeGetOrNullPointer(this EntityRef entityRef, Type componentType, int componentTypeIndex)
    {
        if (!entityRef.Descriptor.UnsafeTryGetOffset(componentType, componentTypeIndex, out var offset)) {
            return (void*)0;
        }
        return (void*)(entityRef.Pointer + offset);
    }
}