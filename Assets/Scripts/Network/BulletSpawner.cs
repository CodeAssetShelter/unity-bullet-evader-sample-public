using Fusion;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public enum BulletPacketType : byte
{
    BulletSpawn = 1
}

public struct CompressedVector2
{
    public ushort x;
    public ushort y;

    private const float scale = 100f;

    public static CompressedVector2 FromVector2(Vector2 v)
    {
        return new CompressedVector2
        {
            x = CompressFloatToUShort(v.x),
            y = CompressFloatToUShort(v.y)
        };
    }

    public Vector2 ToVector2()
    {
        return new Vector2(
            DecompressUShortToFloat(x),
            DecompressUShortToFloat(y)
        );
    }

    private static ushort CompressFloatToUShort(float value)
    {
        int encoded = Mathf.RoundToInt(value * scale) + 32768;
        return (ushort)Mathf.Clamp(encoded, 0, 65535);
    }

    private static float DecompressUShortToFloat(ushort value)
    {
        return (value - 32768) / scale;
    }
}

public struct BulletSpawnData
{
    public ushort bulletId;
    public CompressedVector2 position;
    public CompressedVector2 direction;
    public byte patternIndex;
}

public class BulletSpawner : NetworkBehaviour
{
    public static BulletSpawner Instance;

    [SerializeField] private GameObject m_BulletPrefab;
    [SerializeField] private ushort m_BulletId = 0;
    [SerializeField] private LocalObjectPool m_LocalObjectPool;

    public override void Spawned()
    {
        base.Spawned();
        StartCoroutine(CorTest());
        Instance = Instance != null ? Instance : this;
    }

    private void Awake()
    {
        Instance = Instance != null ? Instance : this;

    }

    public void Start()
    {
        m_LocalObjectPool = gameObject.AddComponent<LocalObjectPool>();
        m_LocalObjectPool.RegisterPrefab(m_BulletPrefab);
        StartCoroutine(CorTest());
    }

    IEnumerator CorTest()
    {
        var wait = new WaitForSeconds(0.5f);
        yield return new WaitUntil(() => SpawnManager.Instance.GetRandomPlayerTransform() != null);

        while (true)
        {
            Transform player = SpawnManager.Instance.GetRandomPlayerTransform();
            if (player == null)
            {
                yield return wait;
                continue;
            }

            Vector3 centerWorld = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0));
            centerWorld.z = 0;
            Vector3 edgeWorld = Camera.main.ViewportToWorldPoint(new Vector3(1f, 1f, 0));
            edgeWorld.z = 0;
            float radius = Vector3.Distance(centerWorld, edgeWorld) + 3f;

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector3 pos = centerWorld + (Vector3)(dir * radius);

            Vector3 lockOn = Random.value >= 0.5f ? Vector3.zero : Random.insideUnitSphere * 5;
            Vector3 target = player.position + lockOn;
            Vector2 direction = (target - pos).normalized;

            List<BulletSpawnData> data = new();
            data.Add(new BulletSpawnData
            {
                bulletId = GenerateBulletId(),
                position = CompressedVector2.FromVector2(pos),
                direction = CompressedVector2.FromVector2(direction),
                //patternIndex = (byte)(Random.Range(0, (int)BulletPattern.State_Count))
                patternIndex = (byte)BulletPattern.Fan
            });

            //Debug.Log((BulletPattern)data.Last().patternIndex);
            RPC_SpawnBullets(BulletPacketEncoder.EncodeBulletSpawn(data));
            yield return wait;
        }
    }
    public T GetRandomEnumValueUnity<T>() where T : Enum
    {
        var values = Enum.GetValues(typeof(T));
        return (T)values.GetValue(Random.Range(0, values.Length));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SpawnBullets(byte[] payload)
    {
        int offset = 0;
        BulletPacketType type = (BulletPacketType)payload[offset++];
        ushort length = BitConverter.ToUInt16(payload, offset); offset += 2;

        if (type == BulletPacketType.BulletSpawn)
            DecodeAndSpawnBullets(payload, offset, length);
    }

    private void DecodeAndSpawnBullets(byte[] payload, int offset, int length)
    {
        int sizePer = 7 + 2 + 2 + 2 + 2; // 15 bytes
        int count = length / sizePer;

        for (int i = 0; i < count; i++)
        {
            ushort bulletId = BitConverter.ToUInt16(payload, offset); offset += 2;
            ushort posX = BitConverter.ToUInt16(payload, offset); offset += 2;
            ushort posY = BitConverter.ToUInt16(payload, offset); offset += 2;
            ushort dirX = BitConverter.ToUInt16(payload, offset); offset += 2;
            ushort dirY = BitConverter.ToUInt16(payload, offset); offset += 2;
            byte pattern = payload[offset++];

            Vector2 pos = new CompressedVector2 { x = posX, y = posY }.ToVector2();
            Vector2 dir = new CompressedVector2 { x = dirX, y = dirY }.ToVector2();

            SpawnBullet(bulletId, pos, dir, (BulletPattern)pattern);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_ReleaseBullet(ushort _bulletId)
    {
        m_LocalObjectPool.ReleaseBulletById(_bulletId);
    }

    public void SpawnBullet(ushort bulletId, Vector3 position, Vector2 direction, BulletPattern pattern)
    {
        GameObject bulletObj = LocalObjectPool.Instance.Get(m_BulletPrefab.name, position, Quaternion.identity);
        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet == null)
        {
            Debug.LogError("Bullet component missing");
            return;
        }
        bullet.SetId(bulletId);
        bullet.SetPattern(pattern, direction, 1f);
    }

    public ushort GenerateBulletId()
    {
        m_BulletId++;
        if (m_BulletId == 0) m_BulletId = 1;
        return m_BulletId;
    }
}

public static class BulletPacketEncoder
{
    public static byte[] EncodeBulletSpawn(List<BulletSpawnData> bullets)
    {
        const int bulletSize = 15;
        int payloadSize = bullets.Count * bulletSize;
        int totalSize = 1 + 2 + payloadSize;

        if (payloadSize > ushort.MaxValue)
        {
            Debug.LogError("Payload too large");
            return Array.Empty<byte>();
        }

        byte[] buffer = new byte[totalSize];
        int offset = 0;

        buffer[offset++] = (byte)BulletPacketType.BulletSpawn;
        BitConverter.GetBytes((ushort)payloadSize).CopyTo(buffer, offset); offset += 2;

        foreach (var b in bullets)
        {
            BitConverter.GetBytes(b.bulletId).CopyTo(buffer, offset); offset += 2;
            BitConverter.GetBytes(b.position.x).CopyTo(buffer, offset); offset += 2;
            BitConverter.GetBytes(b.position.y).CopyTo(buffer, offset); offset += 2;
            BitConverter.GetBytes(b.direction.x).CopyTo(buffer, offset); offset += 2;
            BitConverter.GetBytes(b.direction.y).CopyTo(buffer, offset); offset += 2;
            buffer[offset++] = b.patternIndex;
        }

        return buffer;
    }
}