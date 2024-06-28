using System;
using Fusion;
using UnityEngine;
using UnityEngine.XR.Content.Interaction;

public class KartController : KartComponent
{
    public new SphereCollider collider;
    public DriftTier[] driftTiers;
    [SerializeField] private Axis tireYawAxis = Axis.Y;

    public Transform model;
    public Transform tireFL, tireFR, tireYawFL, tireYawFR, tireBL, tireBR;

    public float maxSpeedNormal;
    public float maxSpeedBoosting;
    public float reverseSpeed;
    public float acceleration;
    public float deceleration;

    [Tooltip("X-Axis: steering\nY-Axis: velocity\nCoordinate space is normalized")]
    public AnimationCurve steeringCurve = AnimationCurve.Linear(0, 0, 1, 1);

    public float maxSteerStrength = 35;
    public float steerAcceleration;
    public float steerDeceleration;
    public Vector2 driftInputRemap = new Vector2(0.5f, 1f);
    public float hopSteerStrength;
    public float speedToDrift;
    public float driftRotationLerpFactor = 10f;

    public Rigidbody Rigidbody;

    private float boostDuration = 3.0f; // Defina o valor conforme necessário
    private float boostCooldown = 5.0f;

    public bool IsBumped => !BumpTimer.ExpiredOrNotRunning(Runner);
    public bool IsBackfire => !BackfireTimer.ExpiredOrNotRunning(Runner);
    public bool IsHopping => !HopTimer.ExpiredOrNotRunning(Runner);
    public bool CanDrive = true;
    //public bool CanDrive => HasStartedRace && !HasFinishedRace && !IsSpinout && !IsBumped && !IsBackfire;

    public float BoostTime => BoostEndTick == -1 ? 0f : (BoostEndTick - Runner.Tick) * Runner.DeltaTime;
    private float RealSpeed => transform.InverseTransformDirection(Rigidbody.velocity).z;
    public bool IsDrifting => IsDriftingLeft || IsDriftingRight;
    public bool IsBoosting => BoostTierIndex != 0;
    public bool IsOffroad => IsGrounded && GroundResistance >= 0.2f;
    public float DriftTime => (Runner.Tick - DriftStartTick) * Runner.DeltaTime;

    [Networked] public float MaxSpeed { get; set; }

    [Networked]
    public int BoostTierIndex { get; set; }

    [Networked] public TickTimer BoostpadCooldown { get; set; }

    [Networked]
    public int DriftTierIndex { get; set; } = -1;

    [Networked] public NetworkBool IsGrounded { get; set; }
    [Networked] public float GroundResistance { get; set; }
    [Networked] public int BoostEndTick { get; set; } = -1;

    [Networked]
    public NetworkBool IsSpinout { get; set; }

    [Networked] public float TireYaw { get; set; }
    [Networked] public RoomPlayer RoomUser { get; set; }
    [Networked] public NetworkBool IsDriftingLeft { get; set; }
    [Networked] public NetworkBool IsDriftingRight { get; set; }
    [Networked] public int DriftStartTick { get; set; }

    [Networked]
    public TickTimer BackfireTimer { get; set; }

    [Networked]
    public TickTimer BumpTimer { get; set; }

    [Networked]
    public TickTimer HopTimer { get; set; }

    [Networked] public float AppliedSpeed { get; set; }

    [Networked] private KartInput.NetworkInputData Inputs { get; set; }

    public event Action<int> OnDriftTierIndexChanged;
    public event Action<int> OnBoostTierIndexChanged;
    public event Action<bool> OnSpinoutChanged;
    public event Action<bool> OnBumpedChanged;
    public event Action<bool> OnHopChanged;
    public event Action<bool> OnBackfiredChanged;

    [Networked] private float SteerAmount { get; set; }
    [Networked] private int AcceleratePressedTick { get; set; }
    [Networked] private bool IsAccelerateThisFrame { get; set; }

    private ChangeDetector _changeDetector;

    private static void OnIsBackfireChangedCallback(KartController changed) =>
        changed.OnBackfiredChanged?.Invoke(changed.IsBackfire);

    private static void OnIsBumpedChangedCallback(KartController changed) =>
        changed.OnBumpedChanged?.Invoke(changed.IsBumped);

    private static void OnIsHopChangedCallback(KartController changed) =>
        changed.OnHopChanged?.Invoke(changed.IsHopping);

    private static void OnSpinoutChangedCallback(KartController changed) =>
        changed.OnSpinoutChanged?.Invoke(changed.IsSpinout);

    private static void OnDriftTierIndexChangedCallback(KartController changed) =>
        changed.OnDriftTierIndexChanged?.Invoke(changed.DriftTierIndex);

    private static void OnBoostTierIndexChangedCallback(KartController changed) =>
        changed.OnBoostTierIndexChanged?.Invoke(changed.BoostTierIndex);

    public XRLever lever; // Referência à alavanca
    public XRKnob steeringWheel; // Referência ao volante
    public float maxSteeringAngle = 45f; // Máximo ângulo de rotação do volante

    private void Awake()
    {
        collider = GetComponent<SphereCollider>();
    }


    public override void Spawned()
    {
        base.Spawned();
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        MaxSpeed = maxSpeedNormal;
    }

    private void Update()
    {
        GroundNormalRotation();
        UpdateTireRotation();
        if (CanDrive)
            Move();
        else
            RefreshAppliedSpeed();

        HandleStartRace();
        //SpinOut();
        Boost();
        Drift();
        Steer();
        UpdateTireYaw();
        UseItems();
    }

    private void OnCollisionStay(Collision collision)
    {
        //
        // OnCollisionEnter and OnCollisionExit are not reliable when trying to predict collisions, however we can
        // use OnCollisionStay reliably. This means we have to make sure not to run code every frame
        //

        var layer = collision.gameObject.layer;

        // We don't want to run any of this code if we're already in the process of bumping
        if (IsBumped) return;

      /*  if (layer == GameManager.GroundLayer) return;
        if (layer == GameManager.KartLayer && collision.gameObject.TryGetComponent(out KartEntity otherKart))
        {
            //
            // Collision with another kart - if we are going slower than them, then we should bump!  
            //

            if (AppliedSpeed < otherKart.Controller.AppliedSpeed)
            {
                BumpTimer = TickTimer.CreateFromSeconds(Runner, 0.4f);
            }
        }
        else
        {
            //
            // Collision with a wall of some sort - We should get the angle impact and apply a force backwards, only if 
            // we are going above 'speedToDrift' speed.
            //
            if (RealSpeed > speedToDrift)
            {
                var contact = collision.GetContact(0);
                var dot = Mathf.Max(0.25f, Mathf.Abs(Vector3.Dot(contact.normal, Rigidbody.transform.forward)));
                Rigidbody.AddForceAtPosition(contact.normal * AppliedSpeed * dot, contact.point, ForceMode.VelocityChange);

                BumpTimer = TickTimer.CreateFromSeconds(Runner, 0.8f * dot);
            }
        }*/
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

       
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(BoostTierIndex):
                    OnBoostTierIndexChangedCallback(this);
                    break;
                case nameof(DriftTierIndex):
                    OnDriftTierIndexChangedCallback(this);
                    break;
                case nameof(IsSpinout):
                    OnSpinoutChangedCallback(this);
                    break;
                case nameof(BackfireTimer):
                    OnIsBackfireChangedCallback(this);
                    break;
                case nameof(BumpTimer):
                    OnIsBumpedChangedCallback(this);
                    break;
                case nameof(HopTimer):
                    OnIsHopChangedCallback(this);
                    break;
            }
        }
    }

    private void HandleStartRace()
    {
        if (!CanDrive)
        {
            // Block user input until the race starts
            RefreshAppliedSpeed();
            Rigidbody.velocity = Vector3.zero;
            Rigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void Move()
    {
        var targetSpeed = GetTargetSpeed();
        AppliedSpeed = Mathf.MoveTowards(AppliedSpeed, targetSpeed, GetAccelerationRate());
        Rigidbody.velocity = AppliedSpeed * transform.forward;
    }

    private float GetTargetSpeed()
    {
        // Use lever position to determine if accelerating or not
        if (lever.value == false)
        {
            return 0;
        }

        var maxSpeed = IsBoosting ? maxSpeedBoosting : maxSpeedNormal;
        return maxSpeed;
    }

    private float GetAccelerationRate()
    {
        // Use lever position to determine acceleration rate
        var direction = lever.value == true ? 1 : -1;
        return acceleration * direction * Time.deltaTime;
    }

    private void Steer()
    {
        var steerInput = GetSteerAmount();

        if (steerInput != 0)
        {
            steer = Mathf.MoveTowards(steer, steerAmount, 0.07f);
        }
        else
        {
            steer = Mathf.MoveTowards(steer, steerAmount, 0.07f);
        }

        var steerAngle = steer * 45;
        transform.Rotate(Vector3.up, steerAngle * Time.deltaTime);
    }
    private float steerAmount;
    private float steer;
    public float GetSteerAmount()
    {
        steerAmount= steeringWheel.value;
        steer = Mathf.MoveTowards(steer, steerAmount, 0.07f);
        return steer;
    }

    private float GetSteerInput()
    {
        var normalizedRotation = Mathf.InverseLerp(steeringWheel.minAngle, steeringWheel.maxAngle, steeringWheel.value*10);
        var steerInput = Mathf.Lerp(-1f, 1f, normalizedRotation);
        return steerInput;
    }

    private void Boost()
    {
        /*if (BoostpadCooldown.ExpiredOrNotRunning(Runner))
        {
            BoostTierIndex++;
            BoostEndTick = Runner.Tick + (int)(boostDuration / Runner.DeltaTime);
            BoostpadCooldown = TickTimer.CreateFromSeconds(Runner, boostCooldown);
        }

        if (BoostEndTick != -1 && Runner.Tick > BoostEndTick)
        {
            BoostEndTick = -1;
            BoostTierIndex = 0;
        }*/
    }

    private void Drift()
    {
        if (lever.transform.localPosition.x >= driftInputRemap.x)
        {
            DriftTierIndex = Mathf.FloorToInt(DriftTime / driftTiers[DriftTierIndex].boostDuration);
        }
        else
        {
            DriftTierIndex = -1;
        }
    }

    private void UpdateTireYaw()
    {
        TireYaw = SteerAmount * maxSteerStrength;
    }

    private void UseItems()
    {
        // Lógica para uso de itens, se aplicável
    }

    private void GroundNormalRotation()
    {
        var wasOffroad = IsOffroad;

        IsGrounded = Physics.SphereCast(collider.transform.TransformPoint(collider.center), collider.radius - 0.1f,
            Vector3.down, out var hit, 0.3f, ~LayerMask.GetMask("Kart"));

        if (IsGrounded)
        {
            Debug.DrawRay(hit.point, hit.normal, Color.magenta);
            GroundResistance = hit.collider.material.dynamicFriction;

            model.transform.rotation = Quaternion.Lerp(
                model.transform.rotation,
                Quaternion.FromToRotation(model.transform.up * 2, hit.normal) * model.transform.rotation,
                7.5f * Time.deltaTime);
        }
        else
        {

        }

       
    }
    private void UpdateTireRotation()
    {
        var yawRotation = Quaternion.Euler(0, TireYaw, 0);

        tireYawFL.localRotation = yawRotation;
        tireYawFR.localRotation = yawRotation;
    }

    public void RefreshAppliedSpeed()
    {
        AppliedSpeed = 0;
    }
}

public enum Axis
{
    X,
    Y,
    Z
}

[Serializable]
public struct DriftTier
{
    public Color color;
    
    public float boostDuration;
    public float startTime;
}