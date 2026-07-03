using System.Collections.Generic;
using UnityEngine;

namespace TK.Core.Utilities
{
    public class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _pool;

        public ObjectPool(T prefab, Transform parent, int initialSize = 0)
        {
            _prefab = prefab;
            _parent = parent;
            _pool = new Stack<T>(initialSize);

            for (var i = 0; i < initialSize; i++)
                _pool.Push(CreateInstance());
        }

        public T Get()
        {
            var instance = _pool.Count > 0 ? _pool.Pop() : CreateInstance();
            instance.gameObject.SetActive(true);
            return instance;
        }

        public void Return(T instance)
        {
            instance.gameObject.SetActive(false);
            instance.transform.SetParent(_parent);
            _pool.Push(instance);
        }

        public void Clear()
        {
            while (_pool.Count > 0)
            {
                var instance = _pool.Pop();
                if (instance) Object.Destroy(instance.gameObject);
            }
        }

        private T CreateInstance()
        {
            var instance = Object.Instantiate(_prefab, _parent);
            instance.gameObject.SetActive(false);
            return instance;
        }
    }
}