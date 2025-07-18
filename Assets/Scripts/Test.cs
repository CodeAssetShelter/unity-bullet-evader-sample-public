using System.Collections;
using UnityEngine;

public class Test : MonoBehaviour
{
    public GameObject explosion;

    private void OnEnable()
    {
        var p = GameObject.Find("Player");
        StartCoroutine(CorPlayDestroyAnim(p.transform));
    }

    IEnumerator CorPlayDestroyAnim(Transform _target)
    {
        if (_target == null) yield break;

        float timeStamp = 0;
        Vector2 pos = _target.position;


        while (timeStamp <= 20.0f)
        {
            if (_target != null)
            {
                pos = _target.position;
            }

            var explosion = Instantiate(this.explosion, pos, Quaternion.identity);
            explosion.SetActive(true);
            var soundType = Random.Range(0, 2);
            SoundManager.Instance.PlayMusic((SoundManager.SoundType)soundType);

            float interval = Random.Range(0, 0.2f);
            timeStamp += interval;
            yield return new WaitForSeconds(interval);
        }
    }
}
