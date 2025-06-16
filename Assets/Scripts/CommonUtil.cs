using System.Collections.Generic;
using UnityEngine;

public static class CommonUtil
{
    public static Vector2 GetRandomCornerPosition()
    {
        Vector2 newVector;
        newVector.x = Random.value > 0.49f ? 1 : 0;
        newVector.y = Random.value > 0.49f ? 1 : 0;
        Vector2 pivot = Camera.main.ViewportToWorldPoint(newVector);
        return pivot;
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
}
