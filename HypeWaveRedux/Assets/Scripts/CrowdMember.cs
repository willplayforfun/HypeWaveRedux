using UnityEngine;

/// <summary>
/// Represents one visual member in the crowd.
/// </summary>
public class CrowdMember : MonoBehaviour
{
    // our private reference to the crowd
    private Crowd crowd;
    // our fixed position in crowd-space, for sampling the crowd fields
    private Vector2 crowdPos;

    [SerializeField, Tooltip("Value between 0 and 1 that thins the crowd in mosh pits")]
    private float moshPitReduction = 0f;

    [Header("Mosh Pit Animation")]
    [SerializeField]
    private Sprite[] pitSprites;

    [SerializeField, Tooltip("Min time between mosh pit sprite switches")]
    private float minPitSpriteCyclePeriod = 0.25f;
    [SerializeField, Tooltip("Max time between mosh pit sprite switches")]
    private float maxPitSpriteCyclePeriod = 0.4f;

    // time when the pit we are in ends
    private float pitEndsTime;
    // the last time we changed our pit sprite
    private float lastAnimateTime;
    // the current index of our pit sprite
    private int currentPitSpriteIndex;

    [Header("Normal Animation")]
    [SerializeField]
    private Sprite[] firstSprites;
    [SerializeField]
    private Sprite[] secondSprites;

    [SerializeField, Tooltip("Min time between normal sprite switches")]
    private float minSpriteCyclePeriod = 0.25f;
    [SerializeField, Tooltip("Max time between normal sprite switches")]
    private float maxSpriteCyclePeriod = 0.3f;
    
    // which set of sprites are we normally using?
    private int normalSpriteIndex;
    // are we on the first sprite set?
    private bool firstSpriteSet;

    [SerializeField]
    private SpriteRenderer spriteRenderer;

    [Header("Relocation")]
    [SerializeField, Tooltip("How often we relocate in our spot")]
    public float relocatePeriod = 0.1f;
    [SerializeField, Tooltip("Multiplier for relocatePeriod when at min hype")]
    private float minRelocatePeriodModifier = 1f;
    [SerializeField, Tooltip("Multiplier for relocatePeriod when at max hype")]
    private float maxRelocatePeriodModifier = 0.1f;
    // the last time we relocated
    private float lastRelocate;
    // where we are currently relocating to
    private Vector3 relocateTarget;
    // our position before relocation; to stay anchored to roughly one spot
    private Vector3 centerPosition;

    [SerializeField, Tooltip("Lerp speed at min hype")]
    private float minHypeLerpSpeed = 0.2f;
    [SerializeField, Tooltip("Lerp speed at max hype")]
    private float maxHypeLerpSpeed = 0.02f;

    private void Start()
    {
        crowd = Crowd.Instance;
        crowd.crowdUpdate += UpdateState;
        crowd.pitStart += PitStarts;

        spriteRenderer.transform.localScale = Vector3.one * 1.5f;
        spriteRenderer.transform.rotation = Quaternion.AngleAxis(50f, Vector3.right);

        normalSpriteIndex = UnityEngine.Random.Range(0, firstSprites.Length);
    }


    // This fires every time we should change 
    // what we are doing based on the crowd field
    private void UpdateState()
    {
        // the magnitude of the hype vector is our hype level
        Vector2 hype = crowd.GetHype(crowdPos.x, crowdPos.y); 
        float hypeLevel = hype.magnitude;

        // how far between min and max hype are we?
        float hypeLerp = Mathf.Sqrt(hypeLevel / crowd.maxHype);

        // do animation
        if (Time.time > pitEndsTime)
        {
            // cycle through normal sprites
            if (Time.time - lastAnimateTime > UnityEngine.Random.Range(minSpriteCyclePeriod, maxSpriteCyclePeriod))
            {
                lastAnimateTime = Time.time;

                if (firstSpriteSet)
                {
                    firstSpriteSet = false;
                    spriteRenderer.sprite = firstSprites[normalSpriteIndex];
                }
                else
                {
                    firstSpriteSet = true;
                    spriteRenderer.sprite = secondSprites[normalSpriteIndex];
                }
            }
        }
        else
        {
            // cycle through pit sprites
            if (Time.time - lastAnimateTime > UnityEngine.Random.Range(minPitSpriteCyclePeriod, maxPitSpriteCyclePeriod))
            {
                lastAnimateTime = Time.time;

                currentPitSpriteIndex = (currentPitSpriteIndex + 1) % pitSprites.Length;
                spriteRenderer.sprite = pitSprites[currentPitSpriteIndex];
            }
        }

        // relocation logic: note, this is black magic concieved while in a fugue state.
        // DO NOT ATTEMPT TO MODIFY WHILE SANE.
        if (Time.time - lastRelocate > relocatePeriod * Mathf.Lerp(minRelocatePeriodModifier, maxRelocatePeriodModifier, hypeLerp))
        {
            lastRelocate = Time.time;
            float deviation1 = 0;
            deviation1 += UnityEngine.Random.Range(0f, 1f);
            float deviation2 = 0;
            int n = 10;
            for (int i = 0; i < n; i++)
            {
                deviation2 += UnityEngine.Random.Range(-1f, 1f);
            }
            deviation2 = deviation2 / n;

            relocateTarget = centerPosition + Mathf.Lerp(0.5f, 1f, hypeLerp) * (Quaternion.AngleAxis(360 * deviation1, Vector3.up) * Vector3.right + deviation2 * Vector3.right);
        }

        // lerp to relocation position
        transform.position = Vector3.Lerp(transform.position, relocateTarget, Time.deltaTime / Mathf.Lerp(minHypeLerpSpeed, maxHypeLerpSpeed, hypeLerp));

        // faster lerp to crowd wave height
        transform.position = new Vector3(transform.position.x, 
            Mathf.Lerp(transform.position.y, crowd.GetMove(crowdPos.x, crowdPos.y).magnitude * 2f * Mathf.Lerp(1, 1.5f, hypeLerp), Time.deltaTime / 0.03f), 
                                         transform.position.z);

    }

    /// <summary>
    /// This sets our crowd-space position
    /// </summary>
    /// <param name="x">x position</param>
    /// <param name="y">y position</param>
    public void SetPosition(int x, int y)
    {
        crowdPos = new Vector2(x, y);
        centerPosition = new Vector3(x, 0, y);
        transform.position = centerPosition;
    }

    /// <summary>
    /// This is called via an event in Crowd, and tells the CrowdMember that a new mosh pit
    /// has started. We need to make sure we are in it, then store the information, as we 
    /// will not recieve notice when the mosh pits dies out
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="radius"></param>
    /// <param name="duration"></param>
    private void PitStarts(float x, float y, float radius, float duration)
    {
        if (Vector2.Distance(crowdPos, new Vector2(x, y)) < radius)
        {
            pitEndsTime = Time.time + duration;

            currentPitSpriteIndex = UnityEngine.Random.Range(0, pitSprites.Length);
            spriteRenderer.sprite = pitSprites[currentPitSpriteIndex];

            if (UnityEngine.Random.Range(0f, 1f) < moshPitReduction)
            {
                spriteRenderer.enabled = false;
            }
        }
    }
}
