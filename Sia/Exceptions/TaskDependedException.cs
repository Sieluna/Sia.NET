namespace Sia;

[Serializable]
public class TaskDependedException : SchedulerException
{
    public TaskDependedException() { }
    public TaskDependedException(string message) : base(message) { }
    public TaskDependedException(string message, Exception inner) : base(message, inner) { }
    protected TaskDependedException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
}