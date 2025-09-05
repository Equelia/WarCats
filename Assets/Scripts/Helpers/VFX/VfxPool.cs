// VfxPool.cs
using System.Collections.Generic;
using UnityEngine;

public static class VfxPool
{
	private class Pool
	{
		public readonly GameObject prefab;
		public readonly Stack<GameObject> stack = new Stack<GameObject>(16);
		public readonly Transform root;

		public Pool(GameObject prefab)
		{
			this.prefab = prefab;
			root = new GameObject($"[POOL] {prefab.name}").transform;
			Object.DontDestroyOnLoad(root.gameObject);
		}
	}

	private static readonly Dictionary<GameObject, Pool> _pools = new();

	public static GameObject Get(GameObject prefab, Vector3 pos, Quaternion rot)
	{
		if (prefab == null) return null;

		if (!_pools.TryGetValue(prefab, out var pool))
		{
			pool = new Pool(prefab);
			_pools.Add(prefab, pool);
		}

		GameObject go = pool.stack.Count > 0 ? pool.stack.Pop() : Object.Instantiate(prefab, pool.root);
		var t = go.transform;
		t.SetPositionAndRotation(pos, rot);
		go.SetActive(true);
		return go;
	}

	public static void Release(GameObject instance)
	{
		if (!instance) return;
		var parent = instance.transform.parent;
		if (parent == null || !parent.name.StartsWith("[POOL]"))
		{
			Object.Destroy(instance);
			return;
		}
		instance.SetActive(false);
		var rootName = parent.name;
		instance.transform.SetParent(parent, false);

		// Найти исходный prefab по корню пула
		foreach (var kv in _pools)
		{
			if (kv.Value.root == parent)
			{
				kv.Value.stack.Push(instance);
				return;
			}
		}
		// если не нашли — уничтожаем
		Object.Destroy(instance);
	}
}