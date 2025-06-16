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
        // ���� ó��: ť�� null�̰ų� ������� ���
        if (queue == null || queue.Count == 0)
            return null;

        // ��û�� ������ ť�� �ִ� ���� ���� ������ ������ ��ŭ�� ��������
        int count = Mathf.Min(_count, queue.Count);

        List<T> result = new List<T>(count);
        while (count-- > 0)
        {
            result.Add(queue.Dequeue());
        }

        return result;
    }
}
