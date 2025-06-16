using System.Collections.Generic;
using UnityEngine;

public class LocalObjectPool : MonoBehaviour
{
    public static LocalObjectPool Instance { get; private set; }

    private Dictionary<string, Queue<GameObject>> m_Pools = new();
    private Dictionary<string, GameObject> m_PrefabMap = new();
    private Dictionary<ushort, GameObject> m_ActiveBulletsById = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void RegisterPrefab(GameObject prefab, int prewarmCount = 0)
    {
        string key = prefab.name;

        if (m_PrefabMap.ContainsKey(key)) return;

        m_PrefabMap[key] = prefab;
        m_Pools[key] = new Queue<GameObject>();

        for (int i = 0; i < prewarmCount; i++)
        {
            var obj = Instantiate(prefab);
            obj.name = key;
            obj.SetActive(false);
            m_Pools[key].Enqueue(obj);
        }

        Debug.Log($"{prefab.name} registered");
    }

    public GameObject Get(string key, Vector3 position, Quaternion rotation)
    {
        if (!m_Pools.ContainsKey(key))
        {
            Debug.LogError($"Pool not found: {key}");
            return null;
        }

        GameObject obj = m_Pools[key].Count > 0 ? m_Pools[key].Dequeue() : Instantiate(m_PrefabMap[key]);
        obj.name = key;
        obj.transform.SetPositionAndRotation(position, rotation);
        obj.SetActive(true);

        return obj;
    }

    public void Release(GameObject obj)
    {
        string key = obj.name;
        obj.SetActive(false);

        if (!m_Pools.ContainsKey(key))
        {
            Debug.LogWarning($"Trying to release object with no pool: {key}");
            Destroy(obj);
            return;
        }

        m_Pools[key].Enqueue(obj);
    }

    public void ReleaseBulletById(ushort id)
    {
        if (m_ActiveBulletsById.TryGetValue(id, out var obj))
        {
            Release(obj);
        }
        else
        {
            Debug.LogWarning($"No active bullet found with ID {id}");
        }
    }
}
