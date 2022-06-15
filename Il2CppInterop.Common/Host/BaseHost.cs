namespace Il2CppInterop.Common.Host;

public abstract class BaseHost : IDisposable
{
    private static BaseHost s_instance;

    protected static T GetInstance<T>() where T : BaseHost
    {
        if (s_instance is not T host)
        {
            throw new InvalidOperationException($"{typeof(T).Name} is not yet initialized. Call {typeof(T).Name}.Create and {typeof(T).Name}.Start first.");
        }
        return host;
    }

    protected static void SetInstance<T>(T instance) where T : BaseHost
    {
        if (s_instance is not null and not T)
        {
            s_instance.Dispose();
            s_instance = null;
        }
        s_instance = instance;
    }

    private readonly Dictionary<Type, IHostComponent> _components = new();

    public void AddComponent<TComponent>(TComponent component) where TComponent : IHostComponent
    {
        _components.Add(typeof(TComponent), component);
    }

    public TComponent GetComponent<TComponent>() where TComponent : IHostComponent
    {
        if (!_components.TryGetValue(typeof(TComponent), out var component))
            throw new InvalidOperationException($"The host does not have component {typeof(TComponent).FullName} registered.");
        return (TComponent)component;
    }

    public virtual void Start()
    {
        foreach (var component in _components.Values)
            component.Start();
    }

    public virtual void Dispose()
    {
        foreach (var component in _components.Values)
            component.Dispose();
        _components.Clear();
    }
}
