using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles crowd-player interaction, player inputs, and musical skill level.
/// </summary>
public class Player : MonoBehaviour
{
    private static List<Player> alivePlayers = new List<Player>();

    [SerializeField]
    private GameObject[] playerVisualPrefabs;

    private GameObject visuals;
    private Animator animator;

    #region Mosh Pits
    [Header ("Mosh Pits")]

    [SerializeField, Tooltip("How many grid cells is our search area for creating a mosh pit?")]
    private int kernelSize;
    [SerializeField, Tooltip("How large should created mosh pits be?")]
    private float pitRadius;
    [SerializeField, Tooltip("How long should created mosh pits last?")]
    private float pitDuration;
    [SerializeField, Tooltip("How long after creating a pit are we blocked from creating more?")]
    private float pitCooldownTime;
    // the last time we created a pit
    private float lastPitCreationTime;

    // are we currently invulnerable to mosh pits?
    private bool invulnerableToPits;

    // both the following must be satisfied to start a pit
    [SerializeField, Tooltip("Start a pit if the average magnitude in the kernel is higher than this")]
    private float magnitudeToStartPit;
    [SerializeField, Tooltip("Start a pit if the average vector's magnitude in the kernel is less than this")]
    private float chaosToStartPit;

    #endregion

    #region Waves
    [Header("Waves")]

    [SerializeField, Tooltip("How long are we resilient to waves after creating a wave ourself?")]
    private float staunchitudeTime;
    // stores how resistant we are to waves currently (from 0 to 1)
    private float staunchitude;

    [SerializeField, Tooltip("The magnitude of our waves")]
    private float waveSize;
    [SerializeField, Tooltip("This is how much a wave pushes us")]
    private float pushInfluence;

    #endregion

    [Header("General Settings")]
    [SerializeField, Tooltip("At max skills, we move this speed")]
    private float maxSpeed;
    [SerializeField, Tooltip("At min skills, we move this speed")]
    private float minSpeed;

    [Space(6)]
    [SerializeField, Tooltip("Skills at which max speed is reached")]
    private float maxSpeedSkills;
    [SerializeField, Tooltip("Skills at which min speed is reached")]
    private float minSpeedSkills;

    [Space(6)]
    [SerializeField, Tooltip("Top out at this skill level")]
    internal float maxSkills;
    [SerializeField, Tooltip("If we reach this skill level, the crowd drops us")]
    internal float minSkills;

    // last movement input
    private Vector2 lastMove;
    // our current maximum speed
    private float speed;
    // how good we are doing
    private float musicSkills;

    internal float GetSkill()
    {
        return musicSkills;
    }

    [Space(6)]
    [SerializeField, Tooltip("How much hype we pump into the crowd when we move")]
    private float hypeAmount;

    // current number of lives
    private int lives;
    // are we dead?
    private bool dead;
    // are we out of lives?
    private bool OutOfLives
    {
        get
        {
            return lives == 0;
        }
    }
    // when did we die?
    private float deathTime;
    [SerializeField, Tooltip("If multiple lives are enabled, how long after death do we respawn?")]
    private float respawnTime;

    // which player are we?
    internal int playerNum;
    // our current respawn point
    internal Transform respawn;

    // our reference to the crowd (for code brevity)
    private Crowd crowd;
    // our current position in crowd-space (currently 1-to-1)
    private Vector2 crowdPosition;

    [SerializeField]
    private NoteDisplay noteDisplayPrefab;
    // our note display
    private NoteDisplay myDisplay;

    [SerializeField]
    private Spotlight spotlightPrefab;
    // our spotlight on the palyer
    private Spotlight spotlight;

    internal void Init()
    {
        crowd = Crowd.Instance;
        crowd.crowdUpdate += CrowdUpdate;

        visuals = Instantiate(playerVisualPrefabs[playerNum]);
        animator = visuals.GetComponent<Animator>();

        spotlight = Instantiate(spotlightPrefab);
        spotlight.target = this.transform;
        spotlight.SetColor(playerNum);

        myDisplay = Instantiate(noteDisplayPrefab);
        myDisplay.player = this;
        myDisplay.SetFretboardSprite(playerNum);

        transform.position = respawn.transform.position;
        alivePlayers.Add(this);
    }

    private Vector2 GetInput()
    {
        return Vector2.zero;
    }

    private void SetDisplayPos(bool snap)
    {
        float constDist = 5;

        Vector3 myScreenPosition = Camera.main.WorldToScreenPoint(transform.position);
        Vector3 center = new Vector3(Screen.width / 2, Screen.height / 2, 0);

        Vector2 vector = myScreenPosition - center;
        vector.Normalize();

        float angle = Mathf.Atan2(vector.y, vector.x);

        float x = Mathf.Clamp(Mathf.Cos(angle) * Screen.width + Screen.width / 2, 0.0f, Screen.width);
        float y = Mathf.Clamp(Mathf.Sin(angle) * Screen.height + Screen.height / 2, 0.0f, Screen.height);

        Vector3 outward = (new Vector3(x, y, center.z) - center);
        Vector3 screenEdgePos = Camera.main.ScreenToWorldPoint(new Vector3(x, y, constDist) - outward / 3.5f);// myScreenPosition + outward * 100);// transform.position + Vector3.right + Vector3.forward * 1.4f + Vector3.up;
        Vector3 closeFollowPos = Camera.main.ScreenToWorldPoint(new Vector3(myScreenPosition.x, myScreenPosition.y, constDist) + outward.normalized * 100);// myScreenPosition + outward * 100);// transform.position + Vector3.right + Vector3.forward * 1.4f + Vector3.up;

        if (Vector3.Distance(closeFollowPos, center) > Vector3.Distance(screenEdgePos, center))
        {
            if (snap)
            {
                myDisplay.transform.position = closeFollowPos;
            }
            else
            {
                myDisplay.targetPosition = closeFollowPos;
            }
        }
        else
        {
            if (snap)
            {
                myDisplay.transform.position = closeFollowPos;
            }
            else
            {
                myDisplay.targetPosition = closeFollowPos;
            }
        }
    }

    private void Update()
    {
        // update our display's position
        SetDisplayPos(false);

        // check to see if our display is blocking any players
        if (!dead)
        {
            Rect myDisplayScreenRect = new Rect(Camera.main.WorldToScreenPoint(myDisplay.targetPosition), new Vector2(100, 100));

            bool isOccluding = false;
            foreach (Player player in alivePlayers)
            {
                if (myDisplayScreenRect.Contains(player.transform.position))
                {
                    myDisplay.PlayerIsOccluding();
                    isOccluding = true;
                    break;
                }
            }

            if (!isOccluding)
            {
                myDisplay.NoPlayersOccluding();
            }
        }

        // update our speed based on our skill
        speed = minSpeed + (maxSpeed - minSpeed) * Mathf.InverseLerp(minSpeedSkills, maxSpeedSkills, musicSkills);


        if (!dead)
        {
            crowdPosition = crowd.GetCrowdPosition(transform.position.x, transform.position.z);

            if (true)
            {
                // move around based on input
                lastMove = GetInput();
                transform.position += speed * lastMove.x * Vector3.right
                                    + speed * lastMove.y * Vector3.forward;

                // die in a mosh pit
                if (crowd.IsPit(crowdPosition.x, crowdPosition.y) && !invulnerableToPits && !crowd.IsStage(crowdPosition.x, crowdPosition.y))
                {
                    Die();
                }

                // die if you fall off the edge
                if (crowdPosition.x < 1 || crowdPosition.x > crowd.fieldSize - 2 || crowdPosition.y < 1 || crowdPosition.y > crowd.fieldSize - 2)
                {
                    Die();
                }

                // die if your skills is too low
                if (musicSkills <= minSkills)
                {
                    Die();
                }
            }

            // decrease staunchitude over time
            staunchitude = Mathf.Clamp(staunchitude - 1f / staunchitudeTime * Time.deltaTime, 0, 1);
        }
        else
        {
            // respawn logic
            if (Time.time - deathTime > respawnTime)
            {
                if (lives > 0)
                {
                    Respawn();
                }
            }
        }
    }

    private void CrowdUpdate()
    {
        if (!dead)
        {
            // get pushed by the crowd
            Vector2 crowdPush = crowd.GetMove(crowdPosition.x, crowdPosition.y);
            transform.position += new Vector3(crowdPush.x, 0, crowdPush.y) * pushInfluence * (1 - staunchitude);

            // add hype from our movement
            crowd.AddHype(crowdPosition.x, crowdPosition.y, lastMove * hypeAmount);

            // analyze a kernel around the player
            float averageMag = 0;
            Vector2 averageVec = Vector2.zero;
            for (int dx = -kernelSize; dx <= kernelSize; dx++)
            {
                for (int dy = -kernelSize; dy <= kernelSize; dy++)
                {
                    averageMag += crowd.GetHype(crowdPosition.x + dx, crowdPosition.y + dy).magnitude;
                    averageVec += crowd.GetHype(crowdPosition.x + dx, crowdPosition.y + dy);
                }
            }
            averageMag = averageMag / (kernelSize * kernelSize);
            averageVec = averageVec / (kernelSize * kernelSize);

            if (Time.time - lastPitCreationTime > pitCooldownTime && averageMag > 1 && averageVec.magnitude < chaosToStartPit)
            {
                //pit starts
                Debug.Log("starting pit");
                crowd.StartPit(crowdPosition.x, crowdPosition.y, pitRadius, pitDuration);

                Vector2 push = (pitRadius + 1) * crowd.GetHype(crowdPosition.x, crowdPosition.y).normalized;
                StartCoroutine(PushOut(push.x * Vector3.right + push.y * Vector3.forward));
                invulnerableToPits = true;

                lastPitCreationTime = Time.time;
            }
        }
    }

    /// <summary>
    /// Attempts to make the player send a movement wave out through the crowd
    /// </summary>
    /// <returns>Whether or not the wave was successfully created</returns>
    public bool CreateWave()
    {
        // can't make waves if dead or on a stage
        if (!dead && !crowd.IsStage(crowdPosition.x, crowdPosition.y))
        {
            // start a wave
            crowd.AddMove(crowdPosition.x, crowdPosition.y, waveSize);
            // don't be affected by crowd movement right after a wave
            staunchitude = 1;

            // animator signal
            animator.SetTrigger("Wave");

            return true;
        }
        return false;
    }

    /// <summary>
    /// Causes the player to die
    /// </summary>
    internal void Die()
    {
        dead = true;
        deathTime = Time.time;
        lives -= 1;

        alivePlayers.Remove(this);

        // display disappears
        myDisplay.PlayerDied();
        // turn off spotlight
        spotlight.gameObject.SetActive(false);

        // animator death signal
        animator.SetTrigger("Die");

        // TODO character disappears, death effect
    }

    /// <summary>
    /// Causes the player to respawn
    /// </summary>
    internal void Respawn()
    {
        dead = false;
        // skill is zeroed out
        musicSkills = 0;

        alivePlayers.Add(this);

        // go to respawn point
        transform.position = respawn.transform.position;
        // display re-appears
        myDisplay.PlayerRespawned();
        // turn on spotlight
        spotlight.gameObject.SetActive(true);

        // animator signal
        animator.SetTrigger("Respawn");
        
        // TODO character re-appears
    }

    /// <summary>
    /// Coroutine that smoothly pushes the player out of a mosh pit they just started
    /// </summary>
    /// <param name="movement">The translation that should be performed</param>
    private IEnumerator PushOut(Vector3 movement)
    {
        Vector3 originalPos = transform.position;

        float duration = 0.2f;

        float time = 0;
        while (time < duration)
        {
            transform.position = Vector3.Lerp(originalPos, originalPos + movement, time / duration);
            time += Time.deltaTime;
            yield return null;
        }

        invulnerableToPits = false;
    }
}
