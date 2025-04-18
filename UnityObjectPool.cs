using System;
using System.Collections.Generic;

public class UnityObjectPool<T> where T : UnityEngine.Object
{
    #region Inner Classes
    protected class ObjectPoolEntry
    {
        public Queue<T> Instances = new Queue<T>();
        public string Name;
    }
    #endregion

    #region Fields
    private readonly Dictionary<string, ObjectPoolEntry> _pathPool = new Dictionary<string, ObjectPoolEntry>();
    private readonly Dictionary<T, ObjectPoolEntry> _activeInstances = new Dictionary<T, ObjectPoolEntry>();
    private readonly Func<string, T> _factory;
    private readonly Action<T> _onGet;
    private readonly Action<T> _onReturn;
    private readonly Action<T> _onClear;
    #endregion

    #region Constructors
    public UnityObjectPool(Func<string, T> factory, Action<T> onGet = null, Action<T> onReturn = null, Action<T> onClear = null)
    {
        _factory = factory;
        _onGet = onGet;
        _onReturn = onReturn;
        _onClear = onClear;
    }
    #endregion

    #region Public Methods
    public T Get(string name)
    {
        return GetObject(name);
    }

    public void Release(T instance)
    {
        if (_activeInstances.TryGetValue(instance, out ObjectPoolEntry pool))
        {
            _onReturn?.Invoke(instance);
            pool.Instances.Enqueue(instance);
            _activeInstances.Remove(instance);
        }
    }

    public void ReleaseAll()
    {
        var activeInstances = new List<T>(_activeInstances.Keys);
        foreach (T instance in activeInstances)
        {
            Release(instance);
        }
    }

    public void Clear()
    {
        ReleaseAll();

        if (_onClear != null)
        {
            foreach (var entry in _pathPool)
            {
                ObjectPoolEntry pool = entry.Value;
                while (pool.Instances.Count > 0)
                {
                    T obj = pool.Instances.Dequeue();
                    if (obj != null)
                    {
                        _onClear?.Invoke(obj);
                    }
                }
            }
        }

        _pathPool.Clear();
        _activeInstances.Clear();
    }

    public void PrewarmPool(string prefabName, int count, Action<T> onInstanceCreated = null)
    {
        if (!_pathPool.TryGetValue(prefabName, out ObjectPoolEntry pool))
        {
            pool = new ObjectPoolEntry { Name = prefabName };
            _pathPool.Add(prefabName, pool);
        }

        for (int i = 0; i < count; i++)
        {
            T instance = _factory(prefabName);
            if (instance != null)
            {
                onInstanceCreated?.Invoke(instance);
                pool.Instances.Enqueue(instance);
            }
        }
    }

    public int GetInactiveCount(string prefabName)
    {
        if (_pathPool.TryGetValue(prefabName, out ObjectPoolEntry pool))
        {
            return pool.Instances.Count;
        }
        return 0;
    }

    public int GetActiveCount()
    {
        return _activeInstances.Count;
    }

    public int GetTotalCount(string prefabName)
    {
        int inactive = GetInactiveCount(prefabName);
        int active = 0;
        foreach (var activePool in _activeInstances.Values)
        {
            if (activePool.Name == prefabName)
            {
                active++;
            }
        }
        return inactive + active;
    }
    #endregion

    #region Private Methods
    private T GetObject(string prefabName)
    {
        if (!_pathPool.TryGetValue(prefabName, out ObjectPoolEntry pool))
        {
            pool = new ObjectPoolEntry { Name = prefabName };
            _pathPool.Add(prefabName, pool);
        }
        return GetObject(pool);
    }

    private T GetObject(ObjectPoolEntry pool)
    {
        T obj = default(T);
        if (pool.Instances.Count > 0)
        {
            obj = pool.Instances.Dequeue();
        }
        else
        {
            obj = _factory(pool.Name);
        }
        _onGet?.Invoke(obj);
        _activeInstances[obj] = pool;
        return obj;
    }
    #endregion
}
