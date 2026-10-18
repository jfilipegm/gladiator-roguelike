using System.Collections.Generic;
using UnityEngine;

namespace Gladiator.Core
{
    /// <summary>Simple GameObject pool for low-count runtime objects (VFX instances, UI elements). Prewarm at load.</summary>
    public sealed class PrefabPool
    {
        readonly GameObject _prefab;
        readonly Transform _parent;
        readonly Stack<GameObject> _free;

        public PrefabPool(GameObject prefab, Transform parent, int prewarm)
        {
            _prefab = prefab;
            _parent = parent;
            _free = new Stack<GameObject>(prewarm);
            for (int i = 0; i < prewarm; i++) _free.Push(Create());
        }

        GameObject Create()
        {
            var go = Object.Instantiate(_prefab, _parent);
            go.SetActive(false);
            return go;
        }

        public GameObject Get()
        {
            var go = _free.Count > 0 ? _free.Pop() : Create();
            go.SetActive(true);
            return go;
        }

        public void Release(GameObject go)
        {
            go.SetActive(false);
            _free.Push(go);
        }
    }
}
