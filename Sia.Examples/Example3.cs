namespace Sia.Examples;

using System.Numerics;
using Sia;

public static class QuaternionExtensions
{
    public const float TwoPI = MathF.PI * 2;
    public const float DegreeToRadian = TwoPI / 360;
    public const float RadianToDegree = 360 / TwoPI;

    public static Vector3 ToEulerAngles(this Quaternion q)
    {
        var angles = new Vector3();

        float sinrCosp = 2 * (q.W * q.X + q.Y * q.Z);
        float cosrCosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
        angles.X = MathF.Atan2(sinrCosp, cosrCosp);

        float sinp = 2 * (q.W * q.Y - q.Z * q.X);
        angles.Y = MathF.Abs(sinp) >= 1
            ? MathF.CopySign(MathF.PI / 2, sinp)
            : MathF.Asin(sinp);

        float sinyCosp = 2 * (q.W * q.Z + q.X * q.Y);
        float cosyCosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
        angles.Z = MathF.Atan2(sinyCosp, cosyCosp);

        return angles * RadianToDegree;
    }

    public static Quaternion ToQuaternion(this Vector3 v)
    {
        v *= DegreeToRadian;

        float cy = MathF.Cos(v.Z * 0.5f);
        float sy = MathF.Sin(v.Z * 0.5f);
        float cp = MathF.Cos(v.Y * 0.5f);
        float sp = MathF.Sin(v.Y * 0.5f);
        float cr = MathF.Cos(v.X * 0.5f);
        float sr = MathF.Sin(v.X * 0.5f);

        return new Quaternion {
            W = cr * cp * cy + sr * sp * sy,
            X = sr * cp * cy - cr * sp * sy,
            Y = cr * sp * cy + sr * cp * sy,
            Z = cr * cp * sy - sr * sp * cy
        };
    }
}

public static partial class Example3
{
    public partial record struct Position([SiaProperty] Vector3 Value);
    public partial record struct Rotation([SiaProperty] Quaternion Value)
    {
        public Rotation() : this(Quaternion.Identity) {}
    }
    public partial record struct Mover([SiaProperty] float Speed);
    public partial record struct Rotator([SiaProperty] Vector3 AngularSpeed);

    public sealed class Frame : IAddon
    {
        public float Delta { get; set; }
    }

    public sealed class PositionPrintSystem : SystemBase
    {
        public PositionPrintSystem()
        {
            Matcher = Matchers.From<TypeUnion<Position>>();
            Trigger = new EventUnion<WorldEvents.Add, Position.SetValue>();
        }

        public override void Execute(World world, Scheduler scheduler, IEntityQuery query)
        {
            Console.WriteLine("Start PositionPrintSystem");
            query.ForEach(static entity => {
                Console.WriteLine("\t" + entity.Get<Position>().Value);
            });
        }
    }

    public sealed class MoverUpdateSystem : SystemBase
    {
        public MoverUpdateSystem()
        {
            Matcher = Matchers.From<TypeUnion<Mover, Position, Rotation>>();
        }

        public override void Execute(World world, Scheduler scheduler, IEntityQuery query)
        {
            query.ForEach(world, static (world, entity) => {
                ref var mover = ref entity.Get<Mover>();
                ref var pos = ref entity.Get<Position>();
                ref var rot = ref entity.Get<Rotation>();

                var frame = world.GetAddon<Frame>();
                var newPos = pos.Value + Vector3.Transform(Vector3.UnitZ, rot.Value) * mover.Speed * frame.Delta;
                world.Modify(entity, new Position.SetValue(newPos));
            });
        }
    }

    public sealed class RotatorUpdateSystem : SystemBase
    {
        public RotatorUpdateSystem()
        {
            Matcher = Matchers.From<TypeUnion<Rotator, Rotation>>();
        }

        public override void Execute(World world, Scheduler scheduler, IEntityQuery query)
        {
            query.ForEach(world, static (world, entity) => {
                ref var rotator = ref entity.Get<Rotator>();
                ref var rot = ref entity.Get<Rotation>();

                var frame = world.GetAddon<Frame>();
                var newRot = rot.Value.ToEulerAngles() + rotator.AngularSpeed * frame.Delta;
                world.Modify(entity, new Rotation.SetValue(newRot.ToQuaternion()));
            });
        }
    }

    public static class TestObject
    {
        public static EntityRef Create(World world, Vector3 position)
        {
            return world.CreateInHashHost(Tuple.Create(
                new Position(position),
                new Rotation(),
                new Mover(5f),
                new Rotator(new(0, 5f, 0))
            ));
        }
    }

    public static void Run()
    {
        var world = new World();
        var scheduler = new Scheduler();

        world.RegisterSystem<PositionPrintSystem>(scheduler);
        world.RegisterSystem<MoverUpdateSystem>(scheduler);
        world.RegisterSystem<RotatorUpdateSystem>(scheduler);

        for (int i = 0; i != 50; ++i) {
            TestObject.Create(world,
                new Vector3(
                    Random.Shared.NextSingle() * 100 - 50, 0,
                    Random.Shared.NextSingle() * 100 - 50));
        }

        var frame = world.AcquireAddon<Frame>();
        frame.Delta = 0.5f;

        scheduler.Tick();
        scheduler.Tick();
        scheduler.Tick();
        scheduler.Tick();
    }
}