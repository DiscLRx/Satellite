using Data;


namespace Server;

public class RuntimeData(Instance instance, Action saveChange)
{
    public Instance Instance { get; set; } = instance;

    public Action SaveChange { get; } = saveChange;
}