using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls the crowd simulation (both waves and hype)
/// </summary>
public class Crowd : MonoBehaviour
{
    private static Crowd _Instance;
    internal static Crowd Instance
    {
        get
        {
            return _Instance;
        }
    }

    #region Crowd

    [Header("Crowd Vis")]
    [SerializeField, Tooltip("Prefabs will be spawned in large grid")]
    private CrowdMember crowdMemberPrefab;
    [SerializeField, Tooltip("Number of crowd members to a side")]
    private int crowdSize;
    private CrowdMember[,] crowdVis;

    [Header("Crowd Sim")]
    [SerializeField, Tooltip("Each grid cell in the simulation will corespond to this many units")]
    private int crowdSpaceScaler = 1;
    [SerializeField, Tooltip("The simulation will use a grid with this many cells to a side")]
    internal int fieldSize;

    [Space(6)]
    [SerializeField, Tooltip("How fast a crowd movement wave decays in amplitude as it moves outward")]
    private float waveDecay;
    [SerializeField, Tooltip("Rate at which the movement vector in a grid cell decays.")]
    private float moveDecay = 0.8f;
    private Vector2[,] moveField;

    [Space(6)]
    [SerializeField, Tooltip("Hype vectors will never exceed this number in magnitude.")]
    internal float maxHype;
    [SerializeField, Tooltip("Each simulation step, hype is passed forward with this decay rate.")]
    private float hypeTransmissionDecay;
    [SerializeField, Tooltip("Rate at which hype in a grid cell decays.")]
    private float hypeDecay;
    private Vector2[,] hypeField;

    [Space(6)]
    [SerializeField, Tooltip("The crowd simulation is updated at this time interval (seconds).")]
    private float crowdUpdateInterval;
    /// <summary>
    /// This event is fired every 
    /// </summary>
    internal event Action crowdUpdate;

    private float lastCrowdUpdate;


    #endregion

    #region Mosh Pits

    [Header("Mosh Pits")]
    [SerializeField]
    private GameObject pitVisualPrefab;

    private struct Pit
    {
        // coordinates in crowd space
        public float x;
        public float y;
        public float radius;
        public float stopTime;
        public GameObject visuals;
    }
    private List<Pit> pits;

    internal event Action<float, float, float, float> pitStart;

    #endregion

    #region Stages

    [Header("Stages")]
    [SerializeField]
    private Transform stageVisPrefab;

    [Serializable]
    private struct Stage
    {
        // coordinates in crowd space
        public float x1;
        public float y1;
        public float x2;
        public float y2;
    }
    [SerializeField]
    private Stage[] stages;

    #endregion

    private void Awake()
    {
        _Instance = this;
    }

    private void Start()
    {
        pits = new List<Pit>();

        // init simulation vector fields
        moveField = new Vector2[fieldSize, fieldSize];
        hypeField = new Vector2[fieldSize, fieldSize];

        // add crowd member visuals
        crowdVis = new CrowdMember[crowdSize, crowdSize];
        if (crowdMemberPrefab != null)
        {
            for (int x = 1; x < crowdSize - 1; x++)
            {
                for (int y = 1; y < crowdSize - 1; y++)
                {
                    crowdVis[x, y] = Instantiate(crowdMemberPrefab);
                    crowdVis[x, y].SetPosition(x, y);
                    crowdVis[x, y].transform.SetParent(this.transform);
                }
            }
        }
        else
        {
            Debug.LogWarning("Stage vis prefab is null");
        }

        // add stage visuals
        if (stageVisPrefab != null)
        {
            foreach (Stage stage in stages)
            {
                float x1 = stage.x1;
                float x2 = stage.x2;
                float y1 = stage.y1;
                float y2 = stage.y2;
                Transform stageVis = Instantiate(stageVisPrefab);
                stageVis.position = new Vector3(x1 + (x2 - x1) / 2f, 0, y1 + (y2 - y1) / 2f);
                stageVis.localScale = new Vector3(x2 - x1, 1, y2 - y1);
            }
        }
        else
        {
            Debug.LogWarning("Stage vis prefab is null");
        }
    }

    /// <summary>
    /// Converts from world space to crowd space
    /// </summary>
    /// <param name="x">A world space x coordinate</param>
    /// <param name="z">A world space z coordinate</param>
    /// <returns>A crowd space position</returns>
    public Vector2 GetCrowdPosition(float x, float z)
    {
        return new Vector2(x / crowdSpaceScaler, z / crowdSpaceScaler);
    }

    /// <summary>
    /// Starts a mosh pit. Informs CrowdMembers and Players.
    /// </summary>
    /// <param name="x">X position in crowd space</param>
    /// <param name="y">Y position in crowd space</param>
    /// <param name="radius">radius of the pit in crowd space units</param>
    /// <param name="duration">duration of the pit in seconds</param>
    internal void StartPit(float x, float y, float radius, float duration)
    {
        Pit newPit = new Pit();
        newPit.x = x;
        newPit.y = y;
        newPit.radius = radius;
        newPit.stopTime = Time.time + duration;

        if (pitVisualPrefab != null)
        {
            newPit.visuals = Instantiate(pitVisualPrefab);
            newPit.visuals.transform.position = new Vector3(x, 0, y);
        }
        else
        {
            Debug.LogWarning("Mosh pit vis prefab is null");
        }

        pits.Add(newPit);
        pitStart.Invoke(x, y, radius, duration);
    }

    /// <summary>
    /// Checks to see if the provided crowd-space coordinate is inside an active mosh pit
    /// </summary>
    /// <param name="x">X position in crowd space</param>
    /// <param name="y">Y position in crowd space</param>
    /// <returns>true if the given coordinate is in a mosh pit</returns>
    internal bool IsPit(float x, float y)
    {
        foreach (Pit pit in pits)
        {
            if (Vector2.Distance(new Vector2(pit.x, pit.y), new Vector2(x, y)) < pit.radius)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks to see if the provided crowd-space coordinate is on a stage
    /// </summary>
    /// <param name="x">X position in crowd space</param>
    /// <param name="y">Y position in crowd space</param>
    /// <returns>true if the given coordinate is on a stage</returns>
    internal bool IsStage(float x, float y)
    {
        foreach (Stage stage in stages)
        {
            if (x > stage.x1 && x < stage.x2 && y > stage.y1 && y < stage.y2)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Pumps hype into the crowd at this location.
    /// </summary>
    /// <param name="x">X position in crowd space</param>
    /// <param name="y">Y position in crowd space</param>
    /// <param name="hype">The hype vector to be added.</param>
    internal void AddHype(float x, float y, Vector2 hype)
    {
        // don't add hype outside of field bounds
        if (x >= 0 && x <= fieldSize - 1 && y >= 0 && y <= fieldSize - 1)
        {
            int xf = Mathf.FloorToInt(x);
            int yf = Mathf.FloorToInt(y);
            int xc = Mathf.CeilToInt(x);
            int yc = Mathf.CeilToInt(y);

            float xFrac = Mathf.Repeat(x, 1);
            float yFrac = Mathf.Repeat(y, 1);

            // add hype to nearest cells, scaled appropriately (I think I did inverse bilinear interpolation correctly)
            hypeField[xc, yc] = hype * Mathf.Lerp(1, 0, xFrac) * Mathf.Lerp(1, 0, yFrac);
            hypeField[xc, yf] = hype * Mathf.Lerp(1, 0, xFrac) * Mathf.Lerp(0, 1, yFrac);
            hypeField[xf, yc] = hype * Mathf.Lerp(0, 1, xFrac) * Mathf.Lerp(1, 0, yFrac);
            hypeField[xf, yf] = hype * Mathf.Lerp(0, 1, xFrac) * Mathf.Lerp(0, 1, yFrac);
        }
    }

    /// <summary>
    /// Gets the hype vector at a given location through bilinear interpolation
    /// </summary>
    /// <param name="x">X position in crowd space</param>
    /// <param name="y">Y position in crowd space</param>
    /// <returns>The interpolated hype at that position</returns>
    internal Vector2 GetHype(float x, float y)
    {
        // don't allow people to sample outside of the field bounds
        if (x >= 0 && x <= fieldSize - 1 && y >= 0 && y <= fieldSize - 1)
        {
            int x1 = Mathf.FloorToInt(x);
            int x2 = Mathf.CeilToInt(x);
            int y1 = Mathf.FloorToInt(y);
            int y2 = Mathf.CeilToInt(y);
            Vector2 horzLerp1 = Vector2.Lerp(hypeField[x1, y1], hypeField[x2, y1], x - x1);
            Vector2 horzLerp2 = Vector2.Lerp(hypeField[x1, y2], hypeField[x2, y2], x - x1);
            return Vector2.Lerp(horzLerp1, horzLerp2, y - y1);
        }
        return Vector2.zero;
    }
    
    /// <summary>
    /// Starts a movement wave in the crowd.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="moveAmount"></param>
    /// <param name="bias"></param>
    /// <param name="biasx"></param>
    /// <param name="biasy"></param>
    internal void AddMove(float x, float y, float moveAmount, bool bias = false, float biasx = 0, float biasy = 0)
    {
        if (x >= 1 && x <= fieldSize - 2 && y >= 1 && y <= fieldSize - 2)
        {
            int xi = Mathf.RoundToInt(x);
            int yi = Mathf.RoundToInt(y);

            //Vector2 

            moveField[xi + 1, yi] += Vector2.right * moveAmount;
            moveField[xi - 1, yi] += Vector2.left * moveAmount;
            moveField[xi, yi - 1] += Vector2.down * moveAmount;
            moveField[xi, yi + 1] += Vector2.up * moveAmount;
            moveField[xi + 1, yi + 1] += (Vector2.right + Vector2.up).normalized * moveAmount;
            moveField[xi - 1, yi - 1] += (Vector2.left + Vector2.down) * moveAmount;
            moveField[xi + 1, yi - 1] += (Vector2.right + Vector2.down) * moveAmount;
            moveField[xi - 1, yi + 1] += (Vector2.left + Vector2.up) * moveAmount;

        }
    }


    /// <summary>
    /// Gets the movement vector at a given location through bilinear interpolation
    /// </summary>
    /// <param name="x">X position in crowd space</param>
    /// <param name="y">Y position in crowd space</param>
    /// <returns>The interpolated movement vector at that position</returns>
    internal Vector2 GetMove(float x, float y)
    {
        if (x >= 1 && x <= fieldSize - 2 && y >= 1 && y <= fieldSize - 2)
        {
            int x1 = Mathf.FloorToInt(x);
            int x2 = Mathf.CeilToInt(x);
            int y1 = Mathf.FloorToInt(y);
            int y2 = Mathf.CeilToInt(y);
            Vector2 horzLerp1 = Vector2.Lerp(moveField[x1, y1], moveField[x2, y1], x - x1);
            Vector2 horzLerp2 = Vector2.Lerp(moveField[x1, y2], moveField[x2, y2], x - x1);
            return Vector2.Lerp(horzLerp1, horzLerp2, y - y1);
        }
        return Vector2.zero;
    }

    private void Update()
    {
        if (Time.time - lastCrowdUpdate > crowdUpdateInterval)
        {
            lastCrowdUpdate = Time.time;

            // cull expired pits
            List<Pit> donepits = new List<Pit>();
            foreach (Pit pit in pits)
            {
                if (pit.stopTime < Time.time)
                {
                    donepits.Add(pit);
                }
            }
            foreach (Pit pit in donepits)
            {
                Destroy(pit.visuals);
                pits.Remove(pit);
            }

            // let's update our field
            Vector2[,] newField = new Vector2[fieldSize, fieldSize];
            Vector2[,] newHype = new Vector2[fieldSize, fieldSize];

            // iterate across the crowd field
            for (int x = 0; x < fieldSize; x++)
            {
                for (int y = 0; y < fieldSize; y++)
                {
                    // decay hype in-place
                    newHype[x, y] += Vector2.ClampMagnitude(hypeField[x, y] * (1f - hypeDecay), maxHype);
                    newField[x, y] += moveField[x, y] * (1f - moveDecay);

                    // only transmit from interior grid cells
                    if (x > 0 && x < fieldSize - 1 && y > 0 && y < fieldSize - 1)
                    {
                        // iterate across our 8 neighbors
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                // skip the center (us) - this check is not necessary but it makes it clearer to me
                                if (!(dx == 0 && dy == 0))
                                {
                                    // transmit our vector to them, but only in the direction of our vector
                                    float dotproduct = Vector2.Dot(moveField[x, y], new Vector2(dx, dy).normalized);
                                    if (dotproduct > 0)
                                    {
                                        // we transmit our decayed wave strength in the direction of the wave
                                        Vector2 waveTransmission = moveField[x, y] * waveDecay * Mathf.Pow(Mathf.InverseLerp(0, moveField[x, y].magnitude, dotproduct), 2);
                                        // wave transmission is deflected by hype
                                        newField[x + dx, y + dy] += waveTransmission * (1 - Mathf.InverseLerp(0, maxHype, hypeField[x + dx, y + dy].magnitude));
                                        // add deflected wave to the appropriate cell
                                        newField[x + (int)Mathf.Sign(hypeField[x + dx, y + dy].x), y + (int)Mathf.Sign(hypeField[x + dx, y + dy].y)] += hypeField[x + dx, y + dy].normalized * waveTransmission.magnitude * Mathf.InverseLerp(0, maxHype, hypeField[x + dx, y + dy].magnitude);
                                    }

                                    // transmit our hype in the direction of the vector (but it doesn't get deflected by anything)
                                    float hypedotproduct = Vector2.Dot(hypeField[x, y], new Vector2(dx, dy).normalized);
                                    if (hypedotproduct > 0)
                                    {
                                        Vector2 hypeTransmission = hypeField[x, y] * hypeTransmissionDecay * Mathf.Pow(Mathf.InverseLerp(0, hypeField[x, y].magnitude, hypedotproduct), 1);
                                        newHype[x + dx, y + dy] += hypeTransmission;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // mosh pits and stages anchor against movement waves
            for (int x = 1; x < fieldSize - 1; x++)
            {
                for (int y = 1; y < fieldSize - 1; y++)
                {
                    if (IsPit(x, y) || IsStage(x, y))
                    {
                        newField[x, y] = Vector2.zero;
                    }
                }
            }

            // transfer over the new values
            moveField = newField;
            hypeField = newHype;

            // fire the update event
            if (crowdUpdate != null)
            {
                crowdUpdate.Invoke();
            }
        }
    }

}
