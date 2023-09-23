namespace Sia;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

public class SystemLibrary : IAddon
{
    public class Entry
    {
        public IReadOnlyDictionary<Scheduler, Scheduler.TaskGraphNode> TaskGraphNodes => _taskGraphNodes;
        internal Dictionary<Scheduler, Scheduler.TaskGraphNode> _taskGraphNodes = new();
    }

    private record MatchAnyEventListener<TSystem>(
        TSystem System, World World, Scheduler Scheduler, Group Group,
        HashSet<Type> TriggerTypes) : IEventListener
        where TSystem : ISystem
    {
        public bool OnEvent<TEvent>(in EntityRef target, in TEvent e)
            where TEvent : IEvent
        {
            var type = typeof(TEvent);
            if (type == typeof(WorldEvents.Remove)) {
                Group.Remove(target);
            }
            else if (TriggerTypes.Contains(e.GetType())) {
                if (System.OnTriggerEvent(World, Scheduler, target, e)) {
                    Group.Add(target);
                }
            }
            return false;
        }
    }

    private record MatchAnyFilterableEventListener<TSystem>(
        TSystem System, World World, Scheduler Scheduler, Group<EntityRef> Group,
        HashSet<Type> TriggerTypes, HashSet<Type> FilterTypes) : IEventListener
        where TSystem : ISystem
    {
        public bool OnEvent<TEvent>(in EntityRef target, in TEvent e)
            where TEvent : IEvent
        {
            var type = typeof(TEvent);
            if (type == typeof(WorldEvents.Remove)) {
                Group.Remove(target);
            }
            else if (TriggerTypes.Contains(type)) {
                if (System.OnTriggerEvent(World, Scheduler, target, e)) {
                    Group.Add(target);
                }
            }
            else if (FilterTypes.Contains(type)) {
                if (System.OnFilterEvent(World, Scheduler, target, e)) {
                    Group.Remove(target);
                }
            }
            return false;
        }
    }

    private record TargetEventListener<TSystem>(
        TSystem System, World World, Scheduler Scheduler, Group<EntityRef> Group,
        HashSet<Type> TriggerTypes) : IEventListener
        where TSystem : ISystem
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool OnEvent<TEvent>(in EntityRef target, in TEvent e)
            where TEvent : IEvent
        {
            var type = typeof(TEvent);
            if (type == typeof(WorldEvents.Remove)) {
                Group.Remove(target);
            }
            else if (TriggerTypes.Contains(e.GetType())) {
                if (System.OnTriggerEvent(World, Scheduler, target, e)) {
                    Group.Add(target);
                }
            }
            return false;
        }
    }

    private record TargetFilterableEventListener<TSystem>(
        TSystem System, World World, Scheduler Scheduler, Group Group,
        HashSet<Type> TriggerTypes, HashSet<Type> FilterTypes) : IEventListener
        where TSystem : ISystem
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool OnEvent<TEvent>(in EntityRef target, in TEvent e)
            where TEvent : IEvent
        {
            var type = typeof(TEvent);
            if (type == typeof(WorldEvents.Remove)) {
                Group.Remove(target);
            }
            else if (TriggerTypes.Contains(type)) {
                if (System.OnTriggerEvent(World, Scheduler, target, e)) {
                    Group.Add(target);
                }
            }
            else if (FilterTypes.Contains(type)) {
                if (System.OnFilterEvent(World, Scheduler, target, e)) {
                    Group.Remove(target);
                }
            }
            return false;
        }
    }

    public delegate SystemHandle SystemRegisterer(
        SystemLibrary lib, Scheduler scheduler, Scheduler.TaskGraphNode[]? dependedTasks = null);

    private World? _world;
    private readonly Dictionary<Type, Entry> _systemEntries = new();

    private readonly static ConcurrentDictionary<Type, SystemRegisterer> s_systemRegisterers = new();

    public void OnInitialize(World world)
    {
        _world = world;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Ensure<TSystem>()
        where TSystem : ISystem, new()
        => s_systemRegisterers.TryAdd(typeof(TSystem),
            static (lib, scheduler, dependedTasks) => lib.Register<TSystem>(scheduler, dependedTasks));

    public static SystemRegisterer GetRegisterer(Type systemType)
        => s_systemRegisterers[systemType];

    public static SystemRegisterer GetRegisterer<TSystem>()
        where TSystem : ISystem
        => s_systemRegisterers[typeof(TSystem)];

    public Entry Get<TSystem>() where TSystem : ISystem
        => Get(typeof(TSystem));

    public Entry Get(Type systemType)
        => _systemEntries[systemType];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Entry Acquire(Type systemType)
    {
        if (!_systemEntries.TryGetValue(systemType, out var instance)) {
            instance = new();
            _systemEntries.Add(systemType, instance);
        }
        return instance;
    }

    public SystemHandle Register<TSystem>(Scheduler scheduler, Scheduler.TaskGraphNode[]? dependedTasks = null)
        where TSystem : ISystem, new()
    {
        var system = new TSystem();
        var world = _world!;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AddDependedSystemTasks(ISystemUnion systemTypes, List<Scheduler.TaskGraphNode> result)
        {
            foreach (var systemType in systemTypes.Types) {
                var sysEntry = Acquire(systemType);
                if (!sysEntry._taskGraphNodes.TryGetValue(scheduler, out var taskNode)) {
                    throw new InvalidSystemDependencyException(
                        $"Failed to register system: Depended system '{this}' is not registered.");
                }
                result.Add(taskNode);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        SystemHandle[] RegisterChildren(
            ITypeUnion children, Scheduler.TaskGraphNode taskNode)
        {
            var types = children.Types;
            int count = types.Length;
            var handles = new SystemHandle[count];
            var childDependedTasks = new[] { taskNode };

            for (int i = 0; i != count; ++i) {
                var childSysType = types[i];
                handles[i] = GetRegisterer(childSysType)(this, scheduler, childDependedTasks);
            }

            return handles;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void DoRegisterSystem(Entry sysEntry, Scheduler.TaskGraphNode task)
        {
            if (!sysEntry._taskGraphNodes.TryAdd(scheduler, task)) {
                throw new SystemAlreadyRegisteredException(
                    "Failed to register system: system already registered in World and Scheduler pair");
            }
            system.Initialize(world, scheduler);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void DoUnregisterSystem(Entry sysEntry, Scheduler.TaskGraphNode task)
        {
            if (!sysEntry._taskGraphNodes.Remove(scheduler, out var removedTask)) {
                throw new ObjectDisposedException("System has been disposed");
            }
            if (removedTask != task) {
                throw new InvalidOperationException("Internal error: removed task is not the task to be disposed");
            }
            system.Uninitialize(world, scheduler);
        }

        var sysEntry = Acquire(system.GetType());

        var dependedTasksResult = new List<Scheduler.TaskGraphNode>();
        if (dependedTasks != null) {
            dependedTasksResult.AddRange(dependedTasks);
        }

        if (system.Dependencies != null) {
            AddDependedSystemTasks(system.Dependencies, dependedTasksResult);
        }

        Scheduler.TaskGraphNode? task;
        SystemHandle[]? childrenHandles;

        var matcher = system.Matcher;
        var children = system.Children;

        if (matcher == null || matcher == Matchers.None) {
            task = scheduler.CreateTask(dependedTasksResult);
            task.UserData = this;

            DoRegisterSystem(sysEntry, task);
            childrenHandles = children != null ? RegisterChildren(children, task) : null;

            return new SystemHandle(
                system, task,
                handle => {
                    DoUnregisterSystem(sysEntry, task);

                    if (childrenHandles != null) {
                        for (int i = childrenHandles.Length - 1; i >= 0; --i) {
                            childrenHandles[i].Dispose();
                        }
                    }
                    scheduler.RemoveTask(handle.Task);
                });
        }

        Func<bool> taskFunc;
        Action disposeFunc;

        var trigger = system.Trigger;
        var filter = system.Filter;
        var dispatcher = world.Dispatcher;

        if (trigger == null && filter == null) {
            void EntityHandler(in EntityRef entity)
                => system.Execute(world, scheduler, entity);

            if (matcher == Matchers.Any) {
                taskFunc = () => {
                    system.BeforeExecute(world, scheduler);
                    world.ForEach(EntityHandler);
                    system.AfterExecute(world, scheduler);
                    return false;
                };
                disposeFunc = () => {};
            }
            else {
                var query = world.Query(matcher);
                taskFunc = () => {
                    system.BeforeExecute(world, scheduler);
                    query.ForEach(EntityHandler);
                    system.AfterExecute(world, scheduler);
                    return false;
                };
                disposeFunc = query.Dispose;
            }
        }
        else {
            var group = new Group();

            taskFunc = () => {
                system.BeforeExecute(world, scheduler);
                int count = group.Count;
                if (count != 0) {
                    for (int i = 0; i < count; ++i) {
                        system.Execute(world, scheduler, group[i]);
                        count = group.Count;
                    }
                    group.Clear();
                }
                system.AfterExecute(world, scheduler);
                return false;
            };

            if (trigger != null && filter != null) {
                var triggerTypes = new HashSet<Type>(trigger.Types);
                var filterTypes = new HashSet<Type>(filter.Types);

                foreach (var filterType in filterTypes) {
                    triggerTypes.Remove(filterType);
                }

                if (matcher == Matchers.Any) {
                    var listener = new MatchAnyFilterableEventListener<TSystem>(
                        System: system,
                        World: world,
                        Scheduler: scheduler,
                        Group: group,
                        TriggerTypes: triggerTypes,
                        FilterTypes: filterTypes);

                    dispatcher.Listen(listener);
                    disposeFunc = () => dispatcher.Unlisten(listener);
                }
                else {
                    disposeFunc = RegisterReactiveListener(
                        dispatcher, triggerTypes, world.Query(matcher),
                        new TargetFilterableEventListener<TSystem>(
                            System: system,
                            World: world,
                            Scheduler: scheduler,
                            Group: group,
                            TriggerTypes: triggerTypes,
                            FilterTypes: filterTypes));
                }
            }
            else if (trigger != null) {
                var triggerTypes = new HashSet<Type>(trigger.Types);

                if (matcher == Matchers.Any) {
                    var listener = new MatchAnyEventListener<TSystem>(
                        System: system,
                        World: world,
                        Scheduler: scheduler,
                        Group: group,
                        TriggerTypes: triggerTypes);

                    dispatcher.Listen(listener);
                    disposeFunc = () => dispatcher.Unlisten(listener);
                }
                else {
                    disposeFunc = RegisterReactiveListener(
                        dispatcher, triggerTypes, world.Query(matcher),
                        new TargetEventListener<TSystem>(
                            System: system,
                            World: world,
                            Scheduler: scheduler,
                            Group: group,
                            TriggerTypes: triggerTypes));
                }
            }
            else {
                throw new InvalidSystemAttributeException(
                    "Failed to register system: system must have non-null trigger when filter is specified");
            }
        }

        task = scheduler.CreateTask(taskFunc, dependedTasksResult);
        task.UserData = this;

        DoRegisterSystem(sysEntry, task);
        childrenHandles = children != null ? RegisterChildren(children, task) : null;

        SystemHandle? handle = null;

        void OnWorldDisposed(World world) => handle!.Dispose();
        world.OnDisposed += OnWorldDisposed;

        handle = new SystemHandle(
            system, task,
            handle => {
                world.OnDisposed -= OnWorldDisposed;

                DoUnregisterSystem(sysEntry, task);
                disposeFunc();

                if (childrenHandles != null) {
                    for (int i = childrenHandles.Length - 1; i >= 0; --i) {
                        childrenHandles[i].Dispose();
                    }
                }
                scheduler.RemoveTask(handle.Task);
            });

        return handle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool OnlyHasAddEventTrigger(HashSet<Type> triggerTypes)
        => triggerTypes.Count == 1 && triggerTypes.Contains(typeof(WorldEvents.Add));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Action RegisterReactiveListener<TListener>(
        WorldDispatcher dispatcher, HashSet<Type> triggerTypes, World.EntityQuery query, TListener listener)
        where TListener : IEventListener
    {
        if (OnlyHasAddEventTrigger(triggerTypes)) {
            void OnEntityCreated(in EntityRef target)
                => listener.OnEvent(target, WorldEvents.Add.Instance);

            query.OnEntityHostAdded += host => host.OnEntityCreated += OnEntityCreated;
            query.OnEntityHostRemoved += host => host.OnEntityReleased -= OnEntityCreated;

            return () => {
                foreach (var host in query.Hosts) {
                    host.OnEntityReleased -= OnEntityCreated;
                }
                query.Dispose();
            };
        }
        else {
            void OnEntityCreated(in EntityRef target)
                => dispatcher.Listen(target, listener);

            query.OnEntityHostAdded += host => host.OnEntityCreated += OnEntityCreated;
            query.OnEntityHostRemoved += host => host.OnEntityReleased -= OnEntityCreated;

            return () => {
                query.ForEach((in EntityRef entity) => {
                    dispatcher.Unlisten(entity, listener);
                });
                foreach (var host in query.Hosts) {
                    host.OnEntityReleased -= OnEntityCreated;
                }
                query.Dispose();
            };
        }
    }
}