using ExitGames.Client.Photon;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public static class CommonUtil
{
    private static Camera g_Cam = null;

    public static Camera GetMainCamera()
    {
        g_Cam = g_Cam != null ? g_Cam : Camera.main;
        return g_Cam;
    }

    public static Vector2 GetRandomCornerPosition()
    {
        Vector2 newVector;
        newVector.x = Random.value > 0.5f ? 1 : 0;
        newVector.y = Random.value > 0.5f ? 1 : 0;
        Vector2 pivot = GetMainCamera().ViewportToWorldPoint(newVector);
        return pivot;
    }

    public enum LinePositionType
    {
        UP, DOWN, LEFT, RIGHT
    }
    /// <summary>
    /// 뷰포트의 양 꼭짓점을 지정해서 받아오는 함수
    /// </summary>
    /// <returns></returns>
    public static (Vector2 p1, Vector2 p2) GetCornerLinePosition(Vector2 _start, Vector2 _end)
    {
        var cam = GetMainCamera();          // 한 번만 가져옴

        return (cam.ViewportToWorldPoint(_start),
                cam.ViewportToWorldPoint(_end));
    }

    public static List<T> DequeueSafe<T>(this Queue<T> queue, int _count)
    {
        // 예외 처리: 큐가 null이거나 비어있을 경우
        if (queue == null || queue.Count == 0)
            return null;

        // 요청된 수보다 큐에 있는 원소 수가 적으면 가능한 만큼만 가져오기
        int count = Mathf.Min(_count, queue.Count);

        List<T> result = new List<T>(count);
        while (count-- > 0)
        {
            result.Add(queue.Dequeue());
        }

        return result;
    }

    /// <summary>
    /// byte(0~255)를 고정 길이 List<bool>로 변환합니다.
    /// 인덱스 0 = 2⁰(LSB) … 인덱스 (totalBits-1) = MSB
    /// 실제 값에 없는 상위 비트는 false 로 채웁니다.
    /// totalBits 는 1~8 사이로만 지정하세요.
    /// </summary>
    public static List<bool> DecodeVariableBits(byte value, int totalBits = 8)
    {
        if (totalBits < 1 || totalBits > 8)
            throw new ArgumentOutOfRangeException(nameof(totalBits), "totalBits must be 1–8 for a byte.");

        var result = new List<bool>(totalBits);

        // totalBits 만큼 반복: 존재하는 비트 → 실제 값, 나머지 → false
        for (int i = 0; i < totalBits; i++)
            result.Add(((value >> i) & 1) == 1);      // 상위 비트가 없으면 자동으로 0(false)

        return result;   // 항상 Count == totalBits
    }

    public static byte[] EncodeBulletSpawn(List<BulletSpawnData> bullets)
    {
        const int bulletSize = 12;
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
            buffer[offset++] = b.patternOffset;
        }

        return buffer;
    }
}
