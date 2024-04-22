namespace Sia;

public sealed class ArrayBufferStorage<T>(int initialCapacity)
    : BufferStorage<T, ArrayBuffer<T>>(new(initialCapacity))
    where T : struct
{
    public ArrayBufferStorage() : this(0) {}

    public override void CreateSiblingStorage<U>(IStorageHandler<U> handler)
        => handler.Handle<ArrayBufferStorage<U>>(new(initialCapacity));
}