#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

namespace Sia;

using System.Runtime.CompilerServices;

internal static class EntityExtensionsCommon
{
    public unsafe struct EntityRecordData<TResult>
    {
        public EntityRecorder<TResult> Recorder;
        public TResult* Pointer;
        public int* Index;
    }

    public unsafe struct EntityRecordData<TData, TResult>
    {
        public TData UserData;
        public EntityRecorder<TData, TResult> Recorder;
        public TResult* Pointer;
        public int* Index;
    }

    public unsafe struct CompRecordData<C1, TResult>
    {
        public ComponentRecorder<C1, TResult> Recorder;
        public TResult* Pointer;
        public int* Index;
    }

    public unsafe struct CompRecordData<C1, C2, TResult>
    {
        public ComponentRecorder<C1, C2, TResult> Recorder;
        public TResult* Pointer;
        public int* Index;
    }

    public unsafe struct CompRecordData<C1, C2, C3, TResult>
    {
        public ComponentRecorder<C1, C2, C3, TResult> Recorder;
        public TResult* Pointer;
        public int* Index;
    }

    public unsafe struct CompRecordData<C1, C2, C3, C4, TResult>
    {
        public ComponentRecorder<C1, C2, C3, C4, TResult> Recorder;
        public TResult* Pointer;
        public int* Index;
    }

    public unsafe struct CompRecordData<C1, C2, C3, C4, C5, TResult>
    {
        public ComponentRecorder<C1, C2, C3, C4, C5, TResult> Recorder;
        public TResult* Pointer;
        public int* Index;
    }

    public unsafe struct CompRecordData<C1, C2, C3, C4, C5, C6, TResult>
    {
        public ComponentRecorder<C1, C2, C3, C4, C5, C6, TResult> Recorder;
        public TResult* Pointer;
        public int* Index;
    }

    public unsafe struct DataCompRecordData<TData, C1, TResult>
    {
        public TData UserData;
        public DataComponentRecorder<TData, C1, TResult> Recorder;
        public TResult* Pointer;
        public int* Index;
    }

    public unsafe struct DataCompRecordData<TData, C1, C2, TResult>
    {
        public TData UserData;
        public DataComponentRecorder<TData, C1, C2, TResult> Recorder;
        public TResult* Pointer;
        public int* Index;
    }

    public unsafe struct DataCompRecordData<TData, C1, C2, C3, TResult>
    {
        public TData UserData;
        public DataComponentRecorder<TData, C1, C2, C3, TResult> Recorder;
        public TResult* Pointer;
        public int* Index;
    }

    public unsafe struct DataCompRecordData<TData, C1, C2, C3, C4, TResult>
    {
        public TData UserData;
        public DataComponentRecorder<TData, C1, C2, C3, C4, TResult> Recorder;
        public TResult* Pointer;
        public int* Index;
    }

    public unsafe struct DataCompRecordData<TData, C1, C2, C3, C4, C5, TResult>
    {
        public TData UserData;
        public DataComponentRecorder<TData, C1, C2, C3, C4, C5, TResult> Recorder;
        public TResult* Pointer;
        public int* Index;
    }

    public unsafe struct DataCompRecordData<TData, C1, C2, C3, C4, C5, C6, TResult>
    {
        public TData UserData;
        public DataComponentRecorder<TData, C1, C2, C3, C4, C5, C6, TResult> Recorder;
        public TResult* Pointer;
        public int* Index;
    }
}

#pragma warning restore CS8500