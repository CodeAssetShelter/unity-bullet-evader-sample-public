using Fusion;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using BulletPatterns;
using static Fusion.Sockets.NetBitBuffer;
using static UnityEditor.Progress;

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
    // C# 11 부터 생성자가능
    //public BulletSpawnData()
    //{

    //}

    public ushort bulletId;
    public CompressedVector2 position;
    public CompressedVector2 direction;
    public byte patternIndex;
    public byte patternOffset; // 0~255 까지 세부 패턴 수치를 구분할 변수, 각 패턴 인덱스당 개별 오프셋 가짐
}

public class BulletSpawner : NetworkBehaviour
{
    public static BulletSpawner Instance;

    [System.Serializable]
    public class PatternCoroutine
    {
        public PatternCoroutine(Func<IEnumerator> method)
            => _method = method ?? throw new ArgumentNullException(nameof(method));

        /* ───── 필드 ───── */
        readonly Func<IEnumerator> _method;   // null 방지
        Coroutine _coroutine;                 // 실행 중 핸들

        public bool IsRunning => _coroutine != null;

        /* ───── 실행 ───── */
        public void Start(MonoBehaviour host)
        {
            if (host == null) { Debug.LogError("Host is null"); return; }
            if (_coroutine != null) { Debug.LogWarning("Already running"); return; }

            _coroutine = host.StartCoroutine(_method());
        }

        /* ───── 중지 ───── */
        public void Stop(MonoBehaviour host)
        {
            if (_coroutine == null) { Debug.LogWarning("Not running"); return; }
            if (host == null) { Debug.LogWarning("Host is null"); return; }

            host.StopCoroutine(_coroutine);
            _coroutine = null;
        }
    }

    [SerializeField] private GameObject m_BulletPrefab;
    [SerializeField] private ushort m_BulletId = 0;
    [SerializeField] private LocalObjectPool m_LocalObjectPool;

    public Queue<BulletSpawnData> m_BulletSpawnDataQueue = new();

    // RPC 송신용
    private const float m_SendInterval = 0.03f;
    private float m_SendIntervalTimeStamp = 0;

    // 패턴 저장용
    private Dictionary<BulletPattern, PatternCoroutine> m_PatternConatiner = new();

    public override void Spawned()
    {
        base.Spawned();
        Instance = Instance != null ? Instance : this;
    }

    private void Awake()
    {
        Instance = Instance != null ? Instance : this;
        InitPatternContainer();
    }

    public void Start()
    {
        m_LocalObjectPool = gameObject.AddComponent<LocalObjectPool>();
        m_LocalObjectPool.RegisterPrefab(m_BulletPrefab);

        //RunPattern(BulletPattern.Fan);
        //RunPattern(BulletPattern.Normal);
        //RunPattern(BulletPattern.Winder);
        //RunPattern(BulletPattern.Spread);
    }

    #region Pattern Conatiner
    private void InitPatternContainer()
    {
        m_PatternConatiner.Add(BulletPattern.Normal, new(CorPatternNormal));
        m_PatternConatiner.Add(BulletPattern.Fan, new(CorPatternFan));
        m_PatternConatiner.Add(BulletPattern.Spread, new(CorPatternSpread));
        m_PatternConatiner.Add(BulletPattern.Winder, new(CorPatternWinder));
        m_PatternConatiner.Add(BulletPattern.Cage, new(CorPatternCage));
    }

    public void RunPattern(BulletPattern _pattern)
    {
        if (!m_PatternConatiner.TryGetValue(_pattern, out var corDat))
        {
            Debug.LogError($"{_pattern} is not registered.");
            return;
        }

        // 작동 중인 패턴 코루틴은 여기서 1차로 체크
        if (corDat.IsRunning)
            return;

        corDat.Start(this);
    }

    private void StopPattern(BulletPattern _pattern)
    {
        if (!m_PatternConatiner.TryGetValue(_pattern, out var corDat))
        {
            Debug.LogError($"{_pattern} is not registered.");
            return;
        }

        corDat.Stop(this);
    }

    #endregion

    #region Pattern Corountines
    /*── 일반탄 ─────────────────────────────────────────────────────────────*/
    private IEnumerator CorPatternNormal()
    {
        float interval = 2.5f;
        float timeStamp = 0f;

        float interval_min = 0.2f;

        yield return new WaitUntil(() => SpawnManager.Instance.GetRandomPlayerTransform() != null);

        while (true)
        {
            // interval 쪽으로 가중치를 둘 것 
            if (timeStamp < Mathf.Max(interval - (GameManager.Instance.GameLevel + 1), interval_min))
            {
                timeStamp += Time.deltaTime;
                yield return null;
                continue;
            }
            timeStamp = 0;

            Transform player = SpawnManager.Instance.GetRandomPlayerTransform();

            // interval 만큼 쉬기
            if (player == null)
            {
                yield return new WaitForSeconds(interval);
                continue;
            }

            var (pos, dir) = GetBulletVector(player.position);
            AddBulletData(pos, dir, BulletPattern.Normal);

            yield return null;
        }
    }

    /*── 확산탄 ─────────────────────────────────────────────────────────────*/
    /// <summary>
    /// 확산탄 패턴
    /// </summary>
    /// <returns></returns>
    /// 미세 설정 차이를 두기위해서 코루틴 분리
    private IEnumerator CorPatternSpread()
    {
        float interval = 5.0f;
        float timeStamp = 0f;

        float interval_min = 3.5f;

        float spreadIntervalMin = 1.0f;
        float spreadIntervalMax = 2.5f;

        yield return new WaitUntil(() => SpawnManager.Instance.GetRandomPlayerTransform() != null);

        while (true)
        {
            // interval 쪽으로 가중치를 둘 것 
            if (timeStamp < Mathf.Max(interval - (GameManager.Instance.GameLevel + 1), interval_min))
            {
                timeStamp += Time.deltaTime;
                yield return null;
                continue;
            }
            timeStamp = 0;

            Transform player = SpawnManager.Instance.GetRandomPlayerTransform();

            // interval 만큼 쉬기
            if (player == null)
            {
                yield return new WaitForSeconds(interval);
                continue;
            }

            var (pos, dir) = GetBulletVector(player.position);
            byte offset = (byte)(Mathf.Clamp
                (spreadIntervalMax - (GameManager.Instance.GameLevel - 1),
                spreadIntervalMin,
                spreadIntervalMax) * 10f); // 타이머 역할

            AddBulletData(pos, dir, BulletPattern.Spread, offset);
            yield return null;
        }
    }

    /// <summary>
    /// 확산탄 요청: _pos에서 원형으로 N방향 발사
    /// </summary>
    public void RequestSpreadShot(Vector2 _pos, Vector2 _dir)
    {
        if (!Runner.IsServer) return;

        // 1) 4 ~ 8갈래 중 하나 선택
        const int MIN_BRANCH = 4;
        const int MAX_BRANCH = 7;
        int branchCount = Random.Range(MIN_BRANCH, MAX_BRANCH + 1); // 4‥8

        // 2) 각도 간격과 시작 오프셋
        float stepAngle = 360f / branchCount; // 균등 분할
        float startOffset = stepAngle * 0.5f;   // _dir과 겹치지 않도록 반칸 이동

        // 3) 기준 벡터
        Vector2 baseDir = _dir.normalized;
        var pattern = (int)BulletPattern.Normal;
        byte offset = 1;

        // 4) 분할-회전하며 탄환 생성(실제 Spawn 코드는 직접 구현)
        for (int i = 0; i < branchCount; ++i)
        {
            float angle = startOffset + stepAngle * i;                 // 회전 각도
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * baseDir;   // 회전된 방향

            m_BulletSpawnDataQueue.Enqueue(
            new BulletSpawnData
            {
                bulletId = GenerateBulletId(),
                position = CompressedVector2.FromVector2(_pos),
                direction = CompressedVector2.FromVector2(dir),
                patternIndex = (byte)pattern,
                patternOffset = offset
            });
        }
    }


    /*── 가두기 ─────────────────────────────────────────────────────────────*/
    private IEnumerator CorPatternCage()
    {
        float interval = 5f;
        float timeStamp = 0f;

        float interval_min = 5f;

        yield return new WaitUntil(() => SpawnManager.Instance.GetRandomPlayerTransform() != null);

        while (true)
        {
            if (CoSpawnBulletWinder != null)
            {
                yield return null;
                continue;
            }

            // interval 쪽으로 가중치를 둘 것 
            if (timeStamp < Mathf.Max(interval - (GameManager.Instance.GameLevel + 1), interval_min))
            {
                timeStamp += Time.deltaTime;
                yield return null;
                continue;
            }
            timeStamp = 0;

            Transform player = SpawnManager.Instance.GetRandomPlayerTransform();

            // interval 만큼 쉬기
            if (player == null)
            {
                yield return new WaitForSeconds(interval);
                continue;
            }

            List<BulletSpawnData> data = new();

            var pattern = (int)BulletPattern.Winder;


            // 1) 네 모서리 활성 플래그(0/1) 준비
            List<ushort> activeData = new() { 0, 0, 0, 0 };   // 11시·1시·5시·7시

            // 2) 첫 번째 인덱스
            int idxA = Random.Range(0, 4);

            // 3) 두 번째 인덱스 (idxA와 겹치지 않도록)
            int idxB = (idxA + Random.Range(1, 4)) & 3;       // 1~3 더한 뒤 0~3 래핑

            // 4) 두 자리만 1로 설정
            activeData[idxA] = 1;
            activeData[idxB] = 1;

            // Linq
            //activeData = activeData
            // .Select(b => b                           // 이미 true ? 유지 : 랜덤
            //              ? true
            //              : Random.value > 0.5f)
            // .ToList();

            // activeData[] 는 0/1 · ushort -> float 로 캐스팅
            CompressedVector2 activeOffsetOne = CompressedVector2.FromVector2(
                new Vector2(activeData[0], activeData[1]));

            CompressedVector2 activeOffsetTwo = CompressedVector2.FromVector2(
                new Vector2(activeData[2], activeData[3]));


            // position 의 x, y 와
            // direction 의 x, y 순으로
            // 11시, 1시, 5시, 7시의 활성화 데이터를 담는다.

            // 0~4 + 4
            int seperateCount = Random.Range(4, 8);
            data.Add(new BulletSpawnData
            {
                bulletId = GenerateBulletId(),
                position = activeOffsetOne,
                direction = activeOffsetTwo,
                patternIndex = (byte)pattern,
                patternOffset = BitPackerUtil.PackWinderOffset(seperateCount, 0.1f)
            });


            foreach (var item in data)
            {
                m_BulletSpawnDataQueue.Enqueue(item);
            }

            yield return null;
        }
    }

    /*── 와인더 ─────────────────────────────────────────────────────────────*/
    private IEnumerator CorPatternWinder()
    {
        float interval = 5f;
        float timeStamp = 0f;

        float interval_min = 5f;

        yield return new WaitUntil(() => SpawnManager.Instance.GetRandomPlayerTransform() != null);

        while (true)
        {
            if (CoSpawnBulletWinder != null)
            {
                yield return null;
                continue;
            }

            // interval 쪽으로 가중치를 둘 것 
            if (timeStamp < Mathf.Max(interval - (GameManager.Instance.GameLevel + 1), interval_min))
            {
                timeStamp += Time.deltaTime;
                yield return null;
                continue;
            }
            timeStamp = 0;

            Transform player = SpawnManager.Instance.GetRandomPlayerTransform();

            // interval 만큼 쉬기
            if (player == null)
            {
                yield return new WaitForSeconds(interval);
                continue;
            }

            // 1) 네 모서리 활성 플래그(0/1) 준비
            List<ushort> activeData = new() { 0, 0, 0, 0 };   // 11시·1시·5시·7시

            // 2) 첫 번째 인덱스
            int idxA = Random.Range(0, 4);

            // 3) 두 번째 인덱스 (idxA와 겹치지 않도록)
            int idxB = (idxA + Random.Range(1, 4)) & 3;       // 1~3 더한 뒤 0~3 래핑

            // 4) 두 자리만 1로 설정
            activeData[idxA] = 1;
            activeData[idxB] = 1;

            // Linq
            //activeData = activeData
            // .Select(b => b                           // 이미 true ? 유지 : 랜덤
            //              ? true
            //              : Random.value > 0.5f)
            // .ToList();

            // activeData[] 는 0/1 · ushort -> float 로 캐스팅
            CompressedVector2 activeOffsetOne = CompressedVector2.FromVector2(
                new Vector2(activeData[0], activeData[1]));

            CompressedVector2 activeOffsetTwo = CompressedVector2.FromVector2(
                new Vector2(activeData[2], activeData[3]));


            // position 의 x, y 와
            // direction 의 x, y 순으로
            // 11시, 1시, 5시, 7시의 활성화 데이터를 담는다.

            // 0~4 + 4
            int seperateCount = Random.Range(4, 8);
            var offset = BitPackerUtil.PackWinderOffset(seperateCount, 0.1f);
            AddBulletData(activeOffsetOne, activeOffsetTwo, BulletPattern.Winder, offset);

            yield return null;
        }
    }

    /*── 날개접기 ─────────────────────────────────────────────────────────────*/
    IEnumerator CorPatternFan()
    {
        float interval = 25f;
        float timeStamp = 0f;

        float interval_min = 5f;

        yield return new WaitUntil(() => SpawnManager.Instance.GetRandomPlayerTransform() != null);

        while (true)
        {
            // interval 쪽으로 가중치를 둘 것 
            if (timeStamp < Mathf.Max(interval - (GameManager.Instance.GameLevel + 1), interval_min))
            {
                timeStamp += Time.deltaTime;
                yield return null;
                continue;
            }
            timeStamp = 0;

            Transform player = SpawnManager.Instance.GetRandomPlayerTransform();

            // interval 만큼 쉬기
            if (player == null)
            {
                yield return new WaitForSeconds(interval);
                continue;
            }

            // 방향 지정자
            List<ShotDir> dirs = new List<ShotDir>()
            {
                CommonUtil.GetRandomEnumValueUnity<ShotDir>(),
                CommonUtil.GetRandomEnumValueUnity<ShotDir>(),
                CommonUtil.GetRandomEnumValueUnity<ShotDir>(),
                CommonUtil.GetRandomEnumValueUnity<ShotDir>(),
            };

            var (pos, dir) = GetBulletVector(player.position);
            byte offset = BitPackerUtil.EncodeShotDir(dirs[0], dirs[1], dirs[2], dirs[3]);

            AddBulletData(pos, dir, BulletPattern.Fan, offset);

            yield return null;
        }
    }
    #endregion

    public int maxsender = 30;
    public override void FixedUpdateNetwork()
    {
        if (!Runner.IsServer) return;      // Host 전용

        m_SendIntervalTimeStamp += Runner.DeltaTime;    // Tick 간격 누적

        if (m_SendIntervalTimeStamp >= m_SendInterval)        // 0.15 s 이상?
        {
            m_SendIntervalTimeStamp = 0f;               // 잔여 오차 버리고 리셋

            // 큐에서 최대 30발 꺼내 RPC 전송
            var batch = m_BulletSpawnDataQueue.DequeueSafe(maxsender);

            if (batch == null || batch.Count == 0) return;

            const int MaxPerPacket = 41;               // 512 B 한계에 안전한 발수

            // ── 41발 단위로 분할 전송 ─────────────────────────────
            for (int i = 0; i < batch.Count; i += MaxPerPacket)
            {
                int len = Math.Min(MaxPerPacket, batch.Count - i);

                // List<T>.GetRange는 내부 배열 복사 1회라 부담이 적습니다.
                var slice = batch.GetRange(i, len);

                // RPC 호출, 횟수당 512B
                // 탄환 개당 20B
                RPC_SpawnBullets(CommonUtil.EncodeBulletSpawn(slice));
                //Debug.Log($"{i} 번째 잘림! {i}/{batch.Count}");
            }
        }
    }

    private void AddBulletData(Vector2 _pos, Vector2 _dir, BulletPattern _pattern, byte _pOffset = 0)
    {
        BulletSpawnData data = new()
        {
            bulletId = GenerateBulletId(),
            position = CompressedVector2.FromVector2(_pos),
            direction = CompressedVector2.FromVector2(_dir),
            patternIndex = (byte)_pattern,
            patternOffset = _pOffset
        };

        m_BulletSpawnDataQueue.Enqueue(data);
    }

    private void AddBulletData(CompressedVector2 _pos, CompressedVector2 _dir, BulletPattern _pattern, byte _pOffset = 0)
    {
        BulletSpawnData data = new()
        {
            bulletId = GenerateBulletId(),
            position = _pos,
            direction = _dir,
            patternIndex = (byte)_pattern,
            patternOffset = _pOffset
        };

        m_BulletSpawnDataQueue.Enqueue(data);
    }

    public void AddBulletData(BulletSpawnData _data)
    {
        m_BulletSpawnDataQueue.Enqueue(_data);
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

    // ──── 탄환 생성 함수 ────────────────────
    private List<Vector2> m_WinderStartPos = new(4);
    private GameObject bulletObj;
    private Bullet bullet;

    public void SpawnBullet(ushort bulletId, Vector3 position, Vector2 direction, BulletPattern pattern, byte patternOffset)
    {
        bulletObj = null;
        bullet = null;

        switch (pattern)
        {
            case BulletPattern.Normal:
                SpawnBulletNormal(bulletId, position, direction, pattern, patternOffset);
                break;
                // Spread 는 내부적으로 패턴 열거형만 다름
            case BulletPattern.Spread:
                SpawnBulletNormal(bulletId, position, direction, pattern, patternOffset);
                break;
            case BulletPattern.Fan:
                SpawnBulletFan(bulletId, position, direction, pattern, patternOffset);
                break;
            case BulletPattern.Winder:
                CoSpawnBulletWinder = StartCoroutine(CorSpawnBulletWinder(bulletId, position, direction, pattern, patternOffset));
                break;
            case BulletPattern.Cage:
                break;
            default:
                break;
        }
    }

    private void SpawnBulletNormal(ushort bulletId, Vector3 position, Vector2 direction, BulletPattern pattern, byte patternOffset)
    {
        bulletObj = LocalObjectPool.Instance.Get(m_BulletPrefab.name, position, Quaternion.identity);
        bullet = bulletObj.GetComponent<Bullet>();
        if (bullet == null)
        {
            Debug.LogError("Bullet component missing");
            return;
        }
        bullet.SetId(bulletId);
        bullet.SetPattern(pattern, direction, 1, patternOffset);
    }

    private void SpawnBulletFan(ushort bulletId, Vector3 position, Vector2 direction, BulletPattern pattern, byte patternOffset)
    {
        // patternOffset 을 먼저 디코딩
        var data = BitPackerUtil.DecodeShotDir(patternOffset);

        // lu, ru, rd, ld 순
        for (int i = 0; i < data.Count; i++)
        {
            var pivot = data[i];
            if (pivot == ShotDir.None)
                continue;

            Vector2 start, end, dir;
            float distance = Vector2.Distance
                (CommonUtil.GetMainCamera().ViewportToWorldPoint(Vector2.zero),
                 CommonUtil.GetMainCamera().ViewportToWorldPoint(Vector2.one));
            // 시계냐 반시계냐에 따라서 각 꼭짓점에서 생성해야하는 탄환 막대의 위치가 달라짐
            (start, dir) = i switch
            {
                // lu
                0 => ((Vector2)CommonUtil.GetMainCamera().ViewportToWorldPoint(Vector2.up),
                pivot == ShotDir.CW ? Vector2.right :
                pivot == ShotDir.CCW ? Vector2.down : Vector2.zero),
                // ru
                1 => ((Vector2)CommonUtil.GetMainCamera().ViewportToWorldPoint(Vector2.one),
                pivot == ShotDir.CW ? Vector2.down :
                pivot == ShotDir.CCW ? Vector2.left : Vector2.zero),
                // rd
                2 => ((Vector2)CommonUtil.GetMainCamera().ViewportToWorldPoint(Vector2.right),
                pivot == ShotDir.CW ? Vector2.left :
                pivot == ShotDir.CCW ? Vector2.up : Vector2.zero),
                // ld
                3 => ((Vector2)CommonUtil.GetMainCamera().ViewportToWorldPoint(Vector2.zero),
                pivot == ShotDir.CW ? Vector2.up :
                pivot == ShotDir.CCW ? Vector2.right : Vector2.zero),

                _ => (Vector2.zero, Vector2.zero),
            };

            end = start + (dir * distance);

            // 일단 7개 씩만
            List<Vector2> spawnPoints = CommonUtil.GetInnerPoints(start, end, 7);

            foreach (var spawnPoint in spawnPoints)
            {
                bulletObj = LocalObjectPool.Instance.Get(m_BulletPrefab.name, spawnPoint, Quaternion.identity);
                bullet = bulletObj.GetComponent<Bullet>();
                if (bullet == null)
                {
                    Debug.LogError("Bullet component missing");
                    return;
                }
                bullet.SetId(bulletId);

                // 각도 지정 필요 없음
                // direction 은 center 가 될 값을 가져간다
                bullet.SetPattern(pattern, start, 1f);
            }
        }
    }

    private Coroutine CoSpawnBulletWinder;

    // ─────────────────────────────────────────────────────
    //  Winder 3-Way 패턴  :  화면 4 꼭짓점 → 중앙 기준 3갈래 + 좌우 스윕
    // ─────────────────────────────────────────────────────
    private IEnumerator CorSpawnBulletWinder(
        ushort bulletId,
        Vector3 position,
        Vector2 direction,
        BulletPattern pattern,
        byte patternOffset)
    {
        // 1. 파라미터 언팩 ──────────────────────────────────────
        BitPackerUtil.UnpackWinderOffset(patternOffset,
                                         out int seperateCount,   // (사용처가 없으면 제거)
                                         out float shotDelay);      // 틱 간격

        /* 2. 활성 꼭짓점 수집 (GC 0) ─────────────────────────── */
        m_WinderStartPos.Clear();
        var cam = CommonUtil.GetMainCamera();

        if (position.x == 1) m_WinderStartPos.Add(cam.ViewportToWorldPoint(Vector2.up));     // 11시
        if (position.y == 1) m_WinderStartPos.Add(cam.ViewportToWorldPoint(Vector2.one));    //  1시
        if (direction.x == 1) m_WinderStartPos.Add(cam.ViewportToWorldPoint(Vector2.right));  //  5시
        if (direction.y == 1) m_WinderStartPos.Add(cam.ViewportToWorldPoint(Vector2.zero));   //  7시

        /* 3. shot 횟수 계산 ──────────────────────────────────── */
        //int shotTimes = 6 + 2 * Mathf.FloorToInt(GameManager.Instance.GameLevel);
        int shotTimes = 1;
        int shotStamp = 0;
        float shotSpeed = 1f;
        float shotSpeedMax = 5f;

        /* 4. 각도·스윕 상수 ──────────────────────────────────── */
        const int SHOT_PER_CORNER = 4;     // 3-Way
        const float FAN_TOTAL_DEG = 60f;   // ±30°
        const float SWEEP_TOTAL_DEG = 30f;   // 스윕 폭(±15°)
        const float HOLD_TIME = 5.0f;  // 고정 사격 시간(초)

        float halfFan = FAN_TOTAL_DEG * 0.5f;             // 30°
        float stepDeg = FAN_TOTAL_DEG / (SHOT_PER_CORNER - 1); // 30°/2
        float sweepHalf = SWEEP_TOTAL_DEG * 0.5f;             // 15°

        /* 5. 화면 중앙 월드 좌표 (고정 값) ───────────────────── */
        Vector3 center = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0));
        center.z = 0f;

        /* 6. 스윕용 삼각파 파라미터 ─────────────────────────── */
        float phase = 0f;
        float phaseStep = Mathf.PI * 2f / 80f;   // 80 틱 = 1 왕복
        float holdTimer = 0f;                    // 고정 사격 타이머

        //static Vector2 CornerBaseDir(Vector3 corner, Vector3 screenCenter)
        //{
        //    // x축 : 왼쪽 코너면 +1(→), 오른쪽 코너면 −1(←)
        //    // y축 : 아래  코너면 +1(↑), 위쪽    코너면 −1(↓)
        //    float signX = corner.x < screenCenter.x ? 1f : -1f;
        //    float signY = corner.y < screenCenter.y ? 1f : -1f;
        //    return new Vector2(signX, signY).normalized; // 정확히 45°
        //}

        /* 7. 메인 루프 ─────────────────────────────────────── */
        while (shotStamp < shotTimes)
        {
            bool sweepActive = holdTimer >= HOLD_TIME;            // 고정 ↔ 스윕 전환
            float sweepOffsetDeg = sweepActive
                                   ? Mathf.Sin(phase) * sweepHalf     // 스윕 중
                                   : 0f;                              // 고정 중

            bool sameSideY = true;
            if (m_WinderStartPos.Count == 2)
            {
                bool firstIsTop = m_WinderStartPos[0].y > center.y;
                bool secondIsTop = m_WinderStartPos[1].y > center.y;
                sameSideY = (firstIsTop == secondIsTop);
            }

            foreach (var startPos in m_WinderStartPos)
            {
                Vector2 baseDir = (center - (Vector3)startPos).normalized;
                //Vector2 baseDir = CornerBaseDir(startPos, center);

                // 위쪽 코너면 +1, 아래쪽 코너면 −1
                float cornerSignY = startPos.y > center.y ? 1f : -1f;

                // ① 같은 Y측이면 모두 +1, 서로 다른 Y측이면 위+1/아래−1
                float sign = sameSideY ? 1f : cornerSignY;

                // 3-Way 생성
                for (int i = 0; i < SHOT_PER_CORNER; ++i)
                {

                    // 변경
                    /* ① 기준 45°에서 시계방향(–)으로 시작 → 반시계방향(+)으로 끝 */
                    float offsetDeg = -halfFan                  // -30°  (오른쪽)
                                    + stepDeg * i              // -30, -10, +10, +30
                                    + sweepOffsetDeg;          // 스윕(좌↔우) 공통
                    offsetDeg *= sign;

                    float rad = offsetDeg * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(rad);
                    float sin = Mathf.Sin(rad);
                    Vector2 dir = new Vector2(
                                          cos * baseDir.x - sin * baseDir.y,
                                          sin * baseDir.x + cos * baseDir.y);

                    GameObject bulletObj = LocalObjectPool.Instance.Get(
                                                m_BulletPrefab.name,
                                                startPos,
                                                Quaternion.identity);

                    var bullet = bulletObj.GetComponent<Bullet>();
                    if (bullet == null) { Debug.LogError("Bullet component missing"); yield break; }

                    bullet.SetId(bulletId++);
                    bullet.SetPattern(pattern, dir, shotSpeed);
                }
            }

            /* 8. 틱 대기 및 타이머 업데이트 ─────────────────── */
            yield return new WaitForSeconds(shotDelay);

            if (!sweepActive)
            {
                holdTimer += shotDelay;          // 고정 단계 진행
                shotSpeed = Mathf.SmoothStep(1f, shotSpeedMax, Mathf.Clamp01(holdTimer / HOLD_TIME));
            }
            else
            {
                phase += phaseStep;              // 스윕 단계 진행
                if (phase >= Mathf.PI * 2f)      // 왕복 완료
                {
                    phase = 0f;
                    holdTimer = 0f;              // 다시 고정 단계
                    shotStamp++;
                }
            }
        }

        yield return new WaitForSeconds(15f);

        // 여기에 마지막 탄을 이용한 패턴 회수 넣을 것
        Debug.LogWarning($"End SpawnBulletWinder");
        CoSpawnBulletWinder = null;
    }

    public ushort GenerateBulletId()
    {
        m_BulletId++;
        if (m_BulletId == 0) m_BulletId = 1;
        return m_BulletId;
    }

    public (Vector2 pos, Vector2 dir) GetBulletVector(Vector3 _playerPos)
    {
        Vector3 centerWorld = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0));
        centerWorld.z = 0;
        Vector3 edgeWorld = Camera.main.ViewportToWorldPoint(new Vector3(1f, 1f, 0));
        edgeWorld.z = 0;
        float radius = Vector3.Distance(centerWorld, edgeWorld) + 3f;

        // 탄이 생성 될 위치
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector3 pos = centerWorld + (Vector3)(dir * radius);

        // 탄의 방향벡터
        Vector3 lockOn = Random.value >= 0.5f ? Vector3.zero : Random.insideUnitSphere * 5;
        Vector3 target = _playerPos + lockOn;
        Vector2 direction = (target - pos).normalized;

        return (pos, direction);
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

    // 발사 방향(2비트)
    public enum ShotDir : byte
    {
        None = 0, // 00
        CW = 1, // 01 (Clockwise)
        CCW = 2,  // 10 (Counter-clockwise)
        // 11 = Reserved (특수 패턴 확장용)
    }

    /// <summary>
    /// 2비트 × 4코너 패킹 유틸
    /// </summary>
    public static class BitPackerUtil
    {
        /*──────────────────────────────────────────────
         *  비트 배치 (LSB=0, MSB=7)
         *  7   6   5   4   3   2   1   0
         *  LD  LD  RD  RD  RU  RU  LU  LU
         *──────────────────────────────────────────────*/

        #region ── 인코딩 / 디코딩 ───────────────────────
        /// <summary>4코너 상태 → 1byte로 인코딩</summary>
        public static byte EncodeShotDir(ShotDir lu, ShotDir ru, ShotDir rd, ShotDir ld)
        {
            return (byte)((byte)lu
                 | (byte)((byte)ru << 2)
                 | (byte)((byte)rd << 4)
                 | (byte)((byte)ld << 6));
        }

        /// <summary>1byte → 4코너 상태 디코딩</summary>
        /// lu, ru, rd, ld 순
        public static List<ShotDir> DecodeShotDir(byte value)
        {
            ShotDir lu = (ShotDir)(value & 0b00000011);
            ShotDir ru = (ShotDir)((value >> 2) & 0b00000011);
            ShotDir rd = (ShotDir)((value >> 4) & 0b00000011);
            ShotDir ld = (ShotDir)((value >> 6) & 0b00000011);
            return new List<ShotDir>() { lu, ru, rd, ld };
        }

        /// <summary>
        /// 0~3(2 bit) 범위의 Enum들을 최대 4개까지 1 byte로 패킹
        /// </summary>
        /// <typeparam name="TEnum">byte 기반 Enum · 실제 값 0~3</typeparam>
        /// <param name="values">패킹할 Enum 목록(1~4개)</param>
        /// <returns>패킹된 1 byte</returns>
        public static byte PackEnum2Bits<TEnum>(params TEnum[] values)
            where TEnum : unmanaged, Enum
        {
            if (values == null || values.Length is < 1 or > 4)
                throw new ArgumentException("1~4개의 값만 허용됩니다.", nameof(values));

            byte result = 0;
            int shift = 0;

            foreach (ref readonly var v in values.AsSpan())
            {
                byte b = Unsafe.As<TEnum, byte>(ref Unsafe.AsRef(v));
                // (안전가드) 값이 0~3 범위인지 확인
                if (b > 3) throw new ArgumentOutOfRangeException(
                    nameof(values), $"Enum 값 {b}가 0~3 범위를 벗어납니다.");

                result |= (byte)(b << shift);
                shift += 2;
            }

            return result;
        }

        /// <summary>
        /// Pack2Bits 로 묶은 1 byte → Enum 배열로 복원
        /// </summary>
        public static TEnum[] UnpackEnum2Bits<TEnum>(byte packed, int count)
            where TEnum : unmanaged, Enum
        {
            if (count is < 1 or > 4)
                throw new ArgumentOutOfRangeException(nameof(count));

            TEnum[] dst = new TEnum[count];

            for (int i = 0; i < count; ++i)
            {
                byte val = (byte)((packed >> (i * 2)) & 0b11);
                dst[i] = Unsafe.As<byte, TEnum>(ref val);
            }
            return dst;
        }

        public static byte PackWinderOffset(int n, float m)
        /* n: 4~8, m: 0.02~1 */
        {
            // ── 1) n (4~7) 검사 후 2-bit로 인코딩 ───────────────────
            if (n is < 4 or > 7)
                throw new ArgumentOutOfRangeException(nameof(n), "n 은 4~7 범위여야 합니다.");

            byte nIdx = (byte)(n - 4);        // 0~3 → 2-bit

            // ── 2) m (0.10~1.00) → 6-bit로 양자화 ──────────────────
            m = Mathf.Clamp(m, 0.10f, 1.00f);
            float t = (m - 0.10f) / 0.90f;               // 0~1 정규화
            byte mIdx = (byte)Mathf.RoundToInt(t * 63f);   // 0~63 → 6-bit

            // ── 3) 비트 결합 : [ mIdx (6) | nIdx (2) ] ─────────────
            return (byte)((mIdx << 2) | nIdx);
        }

        public static void UnpackWinderOffset(byte packed, out int n, out float m)
        {
            int nIdx = packed & 0b0000_0011;     // 하위 2 bit, 0~3
            int mIdx = (packed >> 2) & 0b0011_1111; // 상위 6 bit, 0~63

            n = nIdx + 4;            // 4~8 복원
            m = 0.10f + (mIdx / 63f) * 0.90f; // 0.02~1 복원 (±0.0158)
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
            byte pk = EncodeShotDir(ShotDir.None, ShotDir.None, ShotDir.None, ShotDir.None);

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