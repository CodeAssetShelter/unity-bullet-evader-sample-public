using Fusion;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using BulletPatterns;

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

// 패턴명, 오프셋으로 조합하는 패턴명은 여기에 기재
// Normal = 외접원의 호에서 랜덤하게 생성하는 탄
// 0 = 일반

// Spread = 탄 하나가 다른 탄을 5~8방향으로 생성하는 멀티샷
// 백자리 = 탄 갯수
// 이하 자리 = 0~45도 사이의 숫자, 초기 탄 생성 회전각

// 공사중
// Fan = 한 꼭짓점에서 height 만큼의 긴 탄막 범위 생성 후, 부채꼴로 이동
// offset 을 8비트로 분해 후, 언패킹 한 결과값을 List<bool> 로 반환
// 2비트 단위 : lu, ru, rd, ld 순으로 최대 4개 상태값 가짐

// 공사예정
// Winder
// Fan 과 비슷한 구조를 가지고 패턴 생성예정
// Winder 는 BattleGaregga 참조

// Homing = 삭제, 탄이 로컬이므로 호밍은 두 클라 사이 기대위치값이 매우 크게 어긋날 수 있음

public struct BulletSpawnData
{
    public ushort bulletId;
    public CompressedVector2 position;
    public CompressedVector2 direction;
    public byte patternIndex;
    public byte patternOffset; // 0~255 까지 세부 패턴 수치를 구분할 변수, 각 패턴 인덱스당 개별 오프셋 가짐
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
        StartCoroutine(CorSendBulletScheduler());
    }

    public Queue<BulletSpawnData> m_BulletSpawnDataQueue = new();

    public void AddBulletData(BulletSpawnData _data)
    {
        m_BulletSpawnDataQueue.Enqueue(_data);
    }

    IEnumerator CorSendBulletScheduler()
    {
        WaitForSeconds wait = new WaitForSeconds(0.15f);

        while (true)
        {
            yield return wait;

            var list = m_BulletSpawnDataQueue.DequeueSafe(30);
            if (list != null && list.Count > 0)
            {
                RPC_SpawnBullets(CommonUtil.EncodeBulletSpawn(list));
            }
        }
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

            //var pattern = Random.Range(0, (int)BulletPattern.State_Count);
            var pattern = (int)BulletPattern.Normal;
            byte offset = 0;


            #region 공사중 Fan, Winder
            // 이후 난이도 보정 여기서 처리
            //if (pattern == (int)BulletPattern.Fan || pattern == (int)BulletPattern.Winder)
            //{
            //    List<ShotDir> dirs = new List<ShotDir>()
            //    {
            //        (ShotDir)Random.Range(0, (int)ShotDir.StateCount),
            //        (ShotDir)Random.Range(0, (int)ShotDir.StateCount),
            //        (ShotDir)Random.Range(0, (int)ShotDir.StateCount),
            //        (ShotDir)Random.Range(0, (int)ShotDir.StateCount)
            //    };
            //    offset = CornerShotCodec.Encode(dirs[0], dirs[1], dirs[2], dirs[3]);
            //}
            #endregion

            data.Add(new BulletSpawnData
            {
                bulletId = GenerateBulletId(),
                position = CompressedVector2.FromVector2(pos),
                direction = CompressedVector2.FromVector2(direction),
                //patternIndex = (byte)(Random.Range(0, (int)BulletPattern.State_Count))
                patternIndex = (byte)pattern,
                patternOffset = offset
            });


            //Debug.Log((BulletPattern)data.Last().patternIndex);
            foreach (var item in data)
            {
                m_BulletSpawnDataQueue.Enqueue(item);
            }
            //RPC_SpawnBullets(BulletPacketEncoder.EncodeBulletSpawn(data));
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
        int sizePer = 12;
        int count = length / sizePer;

        for (int i = 0; i < count; i++)
        {
            ushort bulletId = BitConverter.ToUInt16(payload, offset); offset += 2;
            ushort posX = BitConverter.ToUInt16(payload, offset); offset += 2;
            ushort posY = BitConverter.ToUInt16(payload, offset); offset += 2;
            ushort dirX = BitConverter.ToUInt16(payload, offset); offset += 2;
            ushort dirY = BitConverter.ToUInt16(payload, offset); offset += 2;
            byte pattern = payload[offset++];
            byte patternOffset = payload[offset++];

            Vector2 pos = new CompressedVector2 { x = posX, y = posY }.ToVector2();
            Vector2 dir = new CompressedVector2 { x = dirX, y = dirY }.ToVector2();

            //Debug.LogWarning(patternOffset);
            SpawnBullet(bulletId, pos, dir, (BulletPattern)pattern, patternOffset);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_ReleaseBullet(ushort _bulletId)
    {
        m_LocalObjectPool.ReleaseBulletById(_bulletId);
    }

    public void SpawnBullet(ushort bulletId, Vector3 position, Vector2 direction, BulletPattern pattern, byte patternOffset)
    {
        GameObject bulletObj = LocalObjectPool.Instance.Get(m_BulletPrefab.name, position, Quaternion.identity);
        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet == null)
        {
            Debug.LogError("Bullet component missing");
            return;
        }
        bullet.SetId(bulletId);

        #region Fan 공사중
        // Fan 일 때 패턴
        //if (pattern == BulletPattern.Fan)
        //// patternOffset 을 먼저 디코딩
        //var data = BulletPatterns.CornerShotCodec.Decode(patternOffset);

        //// lu, ru, rd, ld 순
        //for (int i = 0; i < data.Count; i++)
        //{
        //    var pivot = data[i];
        //    if (pivot == BulletPatterns.ShotDir.None)
        //        continue;

        //    Vector2 start, end;
        //    // 시계냐 반시계냐에 따라서 각 꼭짓점에서 생성해야하는 탄환 막대의 위치가 달라짐
        //    switch (pivot)
        //    {
        //        case BulletPatterns.ShotDir.CW:
        //            (start, end) = i switch
        //            {
        //                0 => CommonUtil.GetCornerLinePosition(Vector2.up, Vector2.one),
        //                1 => CommonUtil.GetCornerLinePosition(Vector2.one, Vector2.right),
        //                2 => CommonUtil.GetCornerLinePosition(Vector2.right, Vector2.zero),
        //                3 => CommonUtil.GetCornerLinePosition(Vector2.zero, Vector2.up),
        //                _ => (Vector2.zero, Vector2.zero)
        //            };
        //            break;
        //        case BulletPatterns.ShotDir.CCW:
        //            (start, end) = i switch
        //            {
        //                0 => CommonUtil.GetCornerLinePosition(Vector2.up, Vector2.zero),
        //                1 => CommonUtil.GetCornerLinePosition(Vector2.one, Vector2.up),
        //                2 => CommonUtil.GetCornerLinePosition(Vector2.right, Vector2.one),
        //                3 => CommonUtil.GetCornerLinePosition(Vector2.zero, Vector2.right),
        //                _ => (Vector2.zero, Vector2.zero)
        //            };
        //            break;
        //    }
        //}
        #endregion

        bullet.SetPattern(pattern, direction, 1f);
    }

    public ushort GenerateBulletId()
    {
        m_BulletId++;
        if (m_BulletId == 0) m_BulletId = 1;
        return m_BulletId;
    }
}

namespace BulletPatterns
{
    // 1) 코너 위치
    public enum Corner : byte
    {
        LU = 0,   // Left-Up  (bit 0–1)
        RU = 1,   // Right-Up (bit 2–3)
        RD = 2,   // Right-Down(bit 4–5)
        LD = 3    // Left-Down(bit 6–7)
    }

    // 2) 발사 방향(2비트)
    public enum ShotDir : byte
    {
        None = 0, // 00
        CW = 1, // 01 (Clockwise)
        CCW = 2,  // 10 (Counter-clockwise)
        // 11 = Reserved (특수 패턴 확장용)
        StateCount
    }

    /// <summary>
    /// 2비트 × 4코너 패킹 유틸
    /// </summary>
    public static class CornerShotCodec
    {
        /*──────────────────────────────────────────────
         *  비트 배치 (LSB=0, MSB=7)
         *  7   6   5   4   3   2   1   0
         *  LD  LD  RD  RD  RU  RU  LU  LU
         *──────────────────────────────────────────────*/

        #region ── 인코딩 / 디코딩 ───────────────────────
        /// <summary>4코너 상태 → 1byte로 인코딩</summary>
        public static byte Encode(ShotDir lu, ShotDir ru, ShotDir rd, ShotDir ld)
        {
            return (byte)((byte)lu
                 | (byte)((byte)ru << 2)
                 | (byte)((byte)rd << 4)
                 | (byte)((byte)ld << 6));
        }

        /// <summary>1byte → 4코너 상태 디코딩</summary>
        /// lu, ru, rd, ld 순
        public static List<ShotDir> Decode(byte value)
        {
            ShotDir lu = (ShotDir)(value & 0b00000011);
            ShotDir ru = (ShotDir)((value >> 2) & 0b00000011);
            ShotDir rd = (ShotDir)((value >> 4) & 0b00000011);
            ShotDir ld = (ShotDir)((value >> 6) & 0b00000011);
            return new List<ShotDir>() { lu, ru, rd, ld };
        }
        #endregion

        #region ── 개별 코너 접근 ─────────────────────────
        /// <summary>특정 코너 상태 읽기</summary>
        public static ShotDir Get(byte packet, Corner corner)
        {
            int shift = ((int)corner) * 2;
            return (ShotDir)((packet >> shift) & 0b11);
        }

        /// <summary>특정 코너 상태 설정 후 새 패킷 반환</summary>
        public static byte Set(byte packet, Corner corner, ShotDir dir)
        {
            int shift = ((int)corner) * 2;
            packet &= (byte)~(0b11 << shift);                  // 자리 클리어
            packet |= (byte)(((byte)dir & 0b11) << shift);     // 새 값 삽입
            return packet;
        }
        #endregion

        #region ── 디버그 헬퍼(선택) ──────────────────────
        /// <summary>바이트를 '01011001' 문자열로 반환</summary>
        public static string ToBitString(byte packet)
            => Convert.ToString(packet, 2).PadLeft(8, '0');

        /// <summary>콘솔/Unity 로그용 예시</summary>
        [RuntimeInitializeOnLoadMethod]
        private static void _Demo()
        {
            // A) 초기화: 모두 None
            byte pk = Encode(ShotDir.None, ShotDir.None, ShotDir.None, ShotDir.None);

            // B) LU=시계, RD=반시계로 변경
            pk = Set(pk, Corner.LU, ShotDir.CW);
            pk = Set(pk, Corner.RD, ShotDir.CCW);

            Debug.Log($"Packet bits : {ToBitString(pk)}");      // 00100001
            Debug.Log($"LU = {Get(pk, Corner.LU)}");            // CW
            Debug.Log($"RU = {Get(pk, Corner.RU)}");            // None
            Debug.Log($"RD = {Get(pk, Corner.RD)}");            // CCW
            Debug.Log($"LD = {Get(pk, Corner.LD)}");            // None
        }
        #endregion
    }
}