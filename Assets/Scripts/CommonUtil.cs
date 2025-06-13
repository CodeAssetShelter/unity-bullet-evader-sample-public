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
}
