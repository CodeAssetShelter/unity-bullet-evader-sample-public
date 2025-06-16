using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class ExplosionSetter : MonoBehaviour
{
    [SerializeField] private Animator m_Animator;

    [SerializeField] private Vector2 m_AnimatorSpeed;
    [SerializeField] private Vector2 m_ScaleRange;
    [SerializeField] private Vector2 m_ExplosionRange;
    public void OnEnable()
    {
        float xGap = Random.Range(m_ExplosionRange.x, m_ExplosionRange.y);
        float yGap = Random.Range(m_ExplosionRange.x, m_ExplosionRange.y);

        transform.Translate(xGap, yGap, 0);

        float newScale = Random.Range(m_ScaleRange.x, m_ScaleRange.y);
        transform.localScale = new Vector2(newScale, newScale);

        var soundType = Random.Range(0, 2);
        SoundManager.Instance.PlayMusic((SoundManager.SoundType) soundType);

        m_Animator.speed = Random.Range(m_AnimatorSpeed.x, m_AnimatorSpeed.y);
        m_Animator.SetFloat("Blend", Random.Range(0, 1.0f));

        Debug.Log($"{transform.position} // {xGap}, {yGap}");
    }

    public void EndAnim()
    {
        LocalObjectPool.Instance.Release(gameObject);
    }
}
