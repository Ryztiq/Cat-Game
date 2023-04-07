using System.Collections;
using System.Collections.Generic;
using Pathfinding;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using System;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class EnemyState
{
    protected Enemy enemy;
    protected Vector3? target = null;

    private Waypoint waypoint;
    private float timeAtWaypoint;
    private float stopAndLookTimer;
    private Vector3 lookAtPosition;

    public EnemyState(Enemy enemy)
    { this.enemy = enemy; }
    
    public virtual void Init()
    {
        waypoint = null;
        timeAtWaypoint = 0f;
        target = null;
    }

    protected void SetWaypoint(Waypoint waypoint)
    {
        this.waypoint = waypoint;
        target = waypoint.transform.position;
    }

    protected void StopAndLook(Vector3 pos)
    {
        stopAndLookTimer = 0f;
        enemy.AI.isStopped = true;
        lookAtPosition = pos;
    }

    protected void RotateTowards(Vector3 pos)
    {
        enemy.AI.transform.rotation = Quaternion.Lerp(enemy.AI.transform.rotation, Quaternion.LookRotation(new Vector3(lookAtPosition.x, 0f, lookAtPosition.z) - new Vector3(enemy.transform.position.x, 0f, enemy.transform.position.z)), Time.deltaTime * 4.5f);
    }

    public virtual void Update() 
    {
        //prioritize target over waypoint
        if(target.HasValue)
        { enemy.AI.destination = target.Value; }
        else if(waypoint != null)
        { enemy.AI.destination = waypoint.transform.position; }

        if(enemy.AtDestination)
        {
            //we arrived at a waypoint. (target gets priority, must be null)
            if(waypoint != null && !target.HasValue)
            {
                timeAtWaypoint += Time.deltaTime;
                if(timeAtWaypoint >= waypoint.StopTime)
                {
                    timeAtWaypoint = 0f;
                    waypoint = null;
                    OnWaypointComplete();
                    return;
                }
            }

            //always reset the target
            target = null;
        }
        else
        { timeAtWaypoint = 0f; }

        //stop and look
        if(enemy.AI.isStopped)
        {
            RotateTowards(lookAtPosition);
            stopAndLookTimer += Time.deltaTime;
            if(stopAndLookTimer > enemy.StopAndLookTime)
            { enemy.AI.isStopped = false; }
        }

        if (enemy.AI.pathStatus != NavMeshPathStatus.PathComplete)
        { target = null; UnityEngine.Debug.LogError("Path invalid.  Weird mode"); return; }
    }

    public virtual void OnWaypointComplete() { }
    public virtual void SetAnimationState(Enemy enemy, Animator anim)
    { anim.SetBool("isWalking", enemy.Moving); }
    public virtual void OnDistract(Vector3 pos) { }
    public virtual void OnEnteredHidingSpotCallback(Enemy enemy, HidingSpot hidingSpot) { }
}

public class PatrollingState : EnemyState
{
    public float AwarenessValue { get; private set; }
    private float lastTimeSeenTimer;
    public bool GoingToWaypoint { get; private set; } = false;
    private float sittingTimer;
    private float sittingDuration;

    public PatrollingState(Enemy enemy) : base(enemy) { }

    public override void Init()
    {
        base.Init();
        lastTimeSeenTimer = 0f;
        AwarenessValue = 0f;
        sittingTimer = 0f;
        sittingDuration = Random.Range(enemy.SittingDownDurationMin, enemy.SittingDownDurationMax);
    }

    public override void Update()
    {
        base.Update();

        //Awareness
        if(enemy.SeesPlayer)
        {
            AwarenessValue += Time.deltaTime * (!PlayerUI.instance.EnemyOnScreen ? enemy.AwarenessMultiplier_BackTurned : 1f);
            lastTimeSeenTimer = 0f;
        }
        else
        {
            lastTimeSeenTimer += Time.deltaTime;
            if (lastTimeSeenTimer >= enemy.AwarenessDecreaseDelay)
            { AwarenessValue -= Time.deltaTime; }
        }

        AwarenessValue = Mathf.Clamp(AwarenessValue, 0f, enemy.Awareness_IdleState_Duration + enemy.Awareness_WarningState_Duration);

        //Behaviour
        if(!GoingToWaypoint)
        {
            if(enemy.AtDestination)
            {
                sittingTimer += Time.deltaTime; //sit and wait
                if (sittingTimer >= sittingDuration)
                {
                    sittingTimer = 0f;
                    sittingDuration = Random.Range(enemy.SittingDownDurationMin, enemy.SittingDownDurationMax);
                    SetWaypoint(enemy.Waypoints[Random.Range(0, enemy.Waypoints.Count)]); //go to random waypoint
                    GoingToWaypoint = true;
                }
            } 
        }

        //stop and face player
        if(enemy.SeesPlayer)
        {
            StopAndLook(enemy.PlayerTransform.position);
        }

        //Transition
        if(AwarenessValue >= enemy.Awareness_IdleState_Duration + enemy.Awareness_WarningState_Duration)
        { enemy.SetState(enemy.AggroState); }
    }

    public override void OnWaypointComplete()
    {
        //go back to start position
        GoingToWaypoint = false;
        target = enemy.StartPosition;
    }
}

public class AggroState : EnemyState
{
    public AggroState(Enemy enemy) : base(enemy) { }

    public event Action OnShoot;
}

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AudioPlayer))]
public class Enemy : MonoBehaviour
{
    public static Enemy instance; //singleton because multiple Enemies are not in the scope of this project

    public PatrollingState PatrollingState { get; private set; }
    public AggroState AggroState { get; private set; }

    public bool DebugMode;
    public Transform EyePosition;
    public List<Waypoint> Waypoints;
    public Waypoint GunWaypoint;
    public GameObject GunObject;
    public GameObject GunModelInScene;
    //Set enemy.GunModelInScene.GetComponent<MeshRenderer>().enabled = true; when granny puts the gun down!!!!
    public EnemyDebug DebugObject;
    public Animator Anim;
    [HideInInspector] public NavMeshAgent AI;
    public float SightDistance = 12f;
    public float AwarenessIncreaseRate = 0.5f;
    public float AwarenessDecreaseRate = 0.3f;
    public float AwarenessMultiplier_BackTurned = 0.6f;
    [Tooltip("How long to wait until the awareness starts decreasing")]
    public float AwarenessDecreaseDelay = 0.8f;
    public float Awareness_IdleState_Duration = 0.4f;
    public float Awareness_WarningState_Duration = 1.8f;
    public float SittingDownDurationMin = 13f;
    public float SittingDownDurationMax = 20f;
    public float StopAndLookTime = 2.5f;
    public float CloseDistance = 1.5f;
    public float TimeUntilShoot = 1.2f;
    public float FovDotProduct = 0.15f;
    //public float AwarenessMultiplierBackTurned = 0.5f;
    [Tooltip("How visible does the player need to be to be spotted [0.0-1.0]")]
    [Range(0f, 1f)]
    public float VisibilityThreshold = 0.5f;
    public float ReloadTime = 1.5f;
    [SerializeField] private LayerMask everythingBesidesEnemy;
    public Sound[] SpotPlayerSounds;
    public Sound[] GrabbingGunSounds;
    public Sound[] ChasingSounds;
    public Sound[] AlertedSounds;
    public Sound[] WatchingTVSounds;
    public Sound ShotgunSound_Reload;
    public Sound ShotgunSound_Fire;
    [HideInInspector] public AudioPlayer AudioPlayer;
    [HideInInspector] public EnemyState State { get; private set; }
    public bool Moving { get; private set; }
    public float PercentVisible { get; private set; }
    public Transform PlayerTransform { get; private set; }
    public bool SeesPlayer { get; private set; }
    public Vector3 StartPosition { get; private set; }
    public bool AtDestination { get { return !AI.hasPath || (AI.hasPath && AI.remainingDistance <= 0.1f); } }

    private bool raycastedToPlayer;
    private float sightDistanceTarget;
    private float sightDistance;
    private float sqrCloseDistance;

    private void Awake()
    {
        if (instance != null)
        { Destroy(this.gameObject); return; }
        instance = this;

        AI = GetComponent<NavMeshAgent>();

        PatrollingState = new PatrollingState(this);
        AggroState = new AggroState(this);
        State = PatrollingState;

        HidingSpot.OnEnteredHidingSpot += OnEnteredHidingSpotCallback;

        sqrCloseDistance = CloseDistance * CloseDistance;
    }

    private void OnEnteredHidingSpotCallback(HidingSpot hidingSpot)
    {
        State.OnEnteredHidingSpotCallback(this, hidingSpot);
    }

    private void Start()
    {
        PlayerTransform = FirstPersonController.instance.transform;
        DebugObject.gameObject.SetActive(DebugMode);
        AudioPlayer = GetComponent<AudioPlayer>();
        chasingRandomSound = new NonRepeatingSound(ChasingSounds);
        spotPlayerRandomSound = new NonRepeatingSound(SpotPlayerSounds);
        alertedRandomSound = new NonRepeatingSound(AlertedSounds);
        grabbingGunRandomSound = new NonRepeatingSound(GrabbingGunSounds);
        watchingTVRandomSound = new NonRepeatingSound(WatchingTVSounds);

        sightDistance = SightDistance;
        sightDistanceTarget = SightDistance;

        StartPosition = transform.position;

        State.Init();

        GunObject.SetActive(false);
    }

    private void Update()
    {
        SeesPlayer = PercentVisible >= VisibilityThreshold;

        //Automatically see player if theyre within a distance
        if (Vector3.SqrMagnitude(FirstPersonController.instance.MainCamera.transform.position - EyePosition.transform.position) <= sqrCloseDistance && !FirstPersonController.instance.Hiding)
        {
            PercentVisible = 1f;
            SeesPlayer = true;
        }

        Moving = !AI.isStopped && AI.velocity.sqrMagnitude > .1f;

        sightDistanceTarget = SightDistance;
        if (raycastedToPlayer)
        { sightDistanceTarget = SightDistance * 2f; } //give her more range once were spotted
        sightDistance = Mathf.Lerp(sightDistance, sightDistanceTarget, Time.deltaTime);

        State.Update();
        State.SetAnimationState(this, Anim);

        PlayQueuedVoicelines();
    }

    private void FixedUpdate()
    {
        raycastedToPlayer = RaycastToPlayer();
    }

    public void Distract(Vector3 position)
    {
        State.OnDistract(position);
    }

    Vector2 enemyPos2D, destinationPos2D;
    public void NavigateToPosition(Vector3 pos)
    {
        AI.destination = pos;
    }

    bool IsPlayerWithinFieldOfView()
    {
        return Vector3.Dot(transform.TransformDirection(Vector3.forward), (PlayerTransform.position - transform.position).normalized) >= FovDotProduct;
    }

    int raycastsHit;
    int framesHiding = 0;
    bool RaycastToPlayer()
    {
        PercentVisible = 0f;
        raycastsHit = 0;

        if (!IsPlayerWithinFieldOfView())
        { return false; }

        if (FirstPersonController.instance.Hiding)
        {
            framesHiding++;
            if (framesHiding > 3)
                return false;
        }
        else
        { framesHiding = 0; }

        bool hit = false;
        for (int i = 0; i < FirstPersonController.instance.VisibilityCheckPoints.Length; i++)
        {
            if (RaycastToPoint(FirstPersonController.instance.VisibilityCheckPoints[i].position))
            {
                hit = true;
                raycastsHit++;
            }
        }

        PercentVisible = (float)raycastsHit / (float)FirstPersonController.instance.VisibilityCheckPoints.Length;

        return hit;
    }

    RaycastHit hit;
    bool RaycastToPoint(Vector3 point)
    {
        bool hitPlayer = false;
        Vector3 direction = (point - EyePosition.position);

        if (Physics.Raycast(EyePosition.position, direction, out hit, sightDistance, everythingBesidesEnemy, QueryTriggerInteraction.Collide))
        {
            hitPlayer = hit.collider.CompareTag("Player");
            if (DebugMode)
            { Debug.DrawLine(EyePosition.position, hit.point, hitPlayer ? Color.green : Color.red, 0.25f); }

            if (!hitPlayer)
            { return false; }
        }
        else
        {
            if (!DebugMode)
            { return false; }

            Debug.DrawLine(EyePosition.position, EyePosition.position + direction * SightDistance, Color.red, 0.25f);
        }

        return hitPlayer;
    }
    public void SetState(EnemyState newState)
    {
        if (State == newState)
        { return; }
        State = newState;
        State.Init();
    }

    public enum VoiceLine { SpotPlayer, GrabbingGun, Chasing, Alerted, WatchingTV }
    private float lastTimePlayedVoiceline = -420f;
    private float voiceLineDuration;
    private float lastTimePlayedChasingVoiceline = -420f;
    private float chasingVoicelineDuration;
    private NonRepeatingSound chasingRandomSound;
    private NonRepeatingSound spotPlayerRandomSound;
    private NonRepeatingSound alertedRandomSound;
    private NonRepeatingSound grabbingGunRandomSound;
    private NonRepeatingSound watchingTVRandomSound;
    private Queue<Sound> voicelineQueue = new Queue<Sound>();

    public Sound PlayVoiceline(Sound sound)
    {
        if (Time.time - lastTimePlayedVoiceline < voiceLineDuration) //make sure we dont overlap voicelines
        {
            print("Queueing voiceline " + sound.name);
            voicelineQueue.Enqueue(sound);
            return null;
        }

        AudioPlayer.Play(sound);
        print("Playing voiceline " + sound.name);
        voiceLineDuration = sound.Clip.length;


        lastTimePlayedVoiceline = Time.time;
        return sound;
    }
    //returns a reference to the sound that was played
    public Sound PlayVoiceline(VoiceLine voiceLine)
    {
        NonRepeatingSound randomSound = null;
        switch (voiceLine)
        {
            case VoiceLine.SpotPlayer:
                randomSound = spotPlayerRandomSound;
                break;
            case VoiceLine.Chasing:
                randomSound = chasingRandomSound;
                break;
            case VoiceLine.Alerted:
                randomSound = alertedRandomSound;
                break;
            case VoiceLine.WatchingTV:
                randomSound = watchingTVRandomSound;
                break;
        }

        if (randomSound == null || randomSound.Sounds.Length == 0)
        { Debug.LogWarning("Random Sound null"); return null; }

        Sound sound = randomSound.Random();
        return PlayVoiceline(sound);
    }

    private void PlayQueuedVoicelines()
    {
        if (voicelineQueue.Count <= 0)
        { return; }

        if (Time.time - lastTimePlayedVoiceline >= voiceLineDuration)
        { PlayVoiceline(voicelineQueue.Dequeue()); }
    }

    //Editor Gizmo stuff
    private void OnDrawGizmosSelected()
    {
        return;
        if (Waypoints == null || Waypoints.Count == 0)
        { return; }
        for (int i = 0; i < Waypoints.Count; i++)
        {
            Waypoint s = Waypoints[i];
            Waypoint e = Waypoints[Mathf.Clamp(i + 1, 0, Waypoints.Count - 1)];
            Gizmos.color = Color.Lerp(Color.green, Color.red, ((float)i / Waypoints.Count));
            Gizmos.DrawLine(s.transform.position, e.transform.position);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(s.transform.position, 0.5f);
        }
    }

}