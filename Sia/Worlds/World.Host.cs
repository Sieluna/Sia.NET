namespace Sia;

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

public partial class World
{
    public event Action<IReactiveEntityHost>? OnEntityHostAdded;
    public event Action<IReactiveEntityHost>? OnEntityHostRemoved;

    public IReadOnlyList<IReactiveEntityHost> Hosts => _hosts.UnsafeRawValues;
    IReadOnlyList<IEntityHost> IEntityQuery.Hosts => _hosts.UnsafeRawValues;

    private readonly SparseSet<IReactiveEntityHost> _hosts = [];

    public bool TryGetHost<TEntity, TStorage>([MaybeNullWhen(false)] out WorldEntityHost<TEntity, TStorage> host)
        where TEntity : IHList
        where TStorage : IStorage<HList<Entity, TEntity>>, new()
    {
        ref var rawHost = ref _hosts.GetValueRefOrNullRef(
            WorldEntityHostIndexer<WorldEntityHost<TEntity, TStorage>>.Index);
        if (Unsafe.IsNullRef(ref rawHost)) {
            host = null;
            return false;
        }
        host = Unsafe.As<WorldEntityHost<TEntity, TStorage>>(rawHost);
        return true;
    }

    public WorldEntityHost<TEntity, TStorage> AddHost<TEntity, TStorage>()
        where TEntity : IHList
        where TStorage : IStorage<HList<Entity, TEntity>>, new()
    {
        ref var rawHost = ref _hosts.GetOrAddValueRef(
            WorldEntityHostIndexer<WorldEntityHost<TEntity, TStorage>>.Index, out bool exists);
        if (exists) {
            throw new ArgumentException("Host with the same type already exists");
        }
        var host = new WorldEntityHost<TEntity, TStorage>(this);
        OnEntityHostAdded?.Invoke(host);
        rawHost = host;
        return host;
    }

    public WorldEntityHost<TEntity, TStorage> AcquireHost<TEntity, TStorage>()
        where TEntity : IHList
        where TStorage : IStorage<HList<Entity, TEntity>>, new()
    {
        ref var rawHost = ref _hosts.GetOrAddValueRef(
            WorldEntityHostIndexer<WorldEntityHost<TEntity, TStorage>>.Index, out bool exists);
        if (exists) {
            return Unsafe.As<WorldEntityHost<TEntity, TStorage>>(rawHost);
        }
        var host = new WorldEntityHost<TEntity, TStorage>(this);
        OnEntityHostAdded?.Invoke(host);
        rawHost = host;
        return host;
    }

    public THost UnsafeAddRawHost<THost>(THost host)
        where THost : IReactiveEntityHost
    {
        ref var rawHost = ref _hosts.GetOrAddValueRef(
            WorldEntityHostIndexer<THost>.Index, out bool exists);
        if (exists) {
            throw new ArgumentException("Host with the same type already exists: " + typeof(THost));
        }
        rawHost = host;
        OnEntityHostAdded?.Invoke(host);
        return host;
    }

    public bool ReleaseHost<THost>()
        where THost : IEntityHost
    {
        if (_hosts.Remove(WorldEntityHostIndexer<THost>.Index, out var host)) {
            OnEntityHostRemoved?.Invoke(host);
            host.Dispose();
            return true;
        }
        return false;
    }

    public bool TryGetHost<THost>([MaybeNullWhen(false)] out THost host)
        where THost : IEntityHost
    {
        if (_hosts.TryGetValue(WorldEntityHostIndexer<THost>.Index, out var rawHost)) {
            host = (THost)rawHost;
            return true;
        }
        host = default;
        return false;
    }

    public bool ConainsHost<THost>()
        where THost : IEntityHost
        => _hosts.ContainsKey(WorldEntityHostIndexer<THost>.Index);
    
    public void ClearHosts()
    {
        var hosts = _hosts.UnsafeRawValues;
        for (int i = 0; i < hosts.Count; ++i) {
            var host = hosts[i];
            OnEntityHostRemoved?.Invoke(host);
            host.Dispose();
        }
        _hosts.Clear();
    }

    public int ClearEmptyHosts()
    {
        int[]? hostsToRemove = null;
        int count = 0;

        foreach (var (key, host) in _hosts) {
            if (host.Count == 0) {
                hostsToRemove ??= ArrayPool<int>.Shared.Rent(_hosts.Count);
                hostsToRemove[count] = key;
                count++;
            }
        }

        if (hostsToRemove != null) {
            try {
                for (int i = 0; i != count; ++i) {
                    _hosts.Remove(hostsToRemove[i], out var host);
                    OnEntityHostRemoved?.Invoke(host!);
                    host!.Dispose();
                }
            }
            finally {
                ArrayPool<int>.Shared.Return(hostsToRemove);
            }
        }

        return count;
    }
}