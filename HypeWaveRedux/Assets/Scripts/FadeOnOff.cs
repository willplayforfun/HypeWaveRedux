using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fades a UI graphic in and then out
/// </summary>
public class FadeOnOff : MonoBehaviour
{
    [SerializeField, Tooltip("Reference to the graphic that should be faded")]
    private Image image;

    [SerializeField, Tooltip("Time before the fade in starts")]
    private float delay;

    [SerializeField, Tooltip("Time to fade in")]
    private float fadeInTime;

    [SerializeField, Tooltip("Time the graphics stays visible")]
    private float visibleTime;

    [SerializeField, Tooltip("Time to fade out")]
    private float fadeOutTime;

    [SerializeField, Tooltip("Should the graphic fade out?")]
    private bool fadeOff = true;

    private void Start()
    {
        // invisible at start
        if(image != null)
        {
            image.color = Color.clear;
        }

        StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        yield return new WaitForSeconds(delay);

        if (image != null)
        {
            // fade in
            float time = 0;
            while (time < fadeInTime)
            {
                image.color = Color.Lerp(new Color(1, 1, 1, 0), Color.white, time / fadeInTime);

                time += Time.deltaTime;
                yield return null;
            }

            image.color = Color.white;

            // stay visible
            yield return new WaitForSeconds(visibleTime);

            // fade out
            if (fadeOff)
            {
                time = 0;
                while (time < fadeOutTime)
                {
                    image.color = Color.Lerp(Color.white, new Color(1, 1, 1, 0), time / fadeOutTime);

                    time += Time.deltaTime;
                    yield return null;
                }

                image.color = Color.clear;
            }
        }
    }
}
