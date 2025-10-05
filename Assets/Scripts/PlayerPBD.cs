using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// PlayerPBD.cs
// Position-Based Dynamics (simple Verlet) implementation of a "3-mass" player:
// - torso, left foot, right foot (particles)
// - distance constraints between torso<->feet
// - ground collisions per-particle
// - simple input: move feet left/right, jump (impulse on torso)
// - optional auto-creation of visual spheres for debugging
// Attach to an empty GameObject. Tweak parameters in inspector.

public class PlayerPBD : MonoBehaviour
{
    [Header("General")]
    public int playerId = 1; // 1 or 2
    public float groundY = 0f;
    public float gravity = -20f;
    public float dt = 0.02f; // this player uses FixedUpdate() so keep Time.fixedDeltaTime similar

    [Header("Particles")]
    public float torsoRadius = 0.35f;
    public float footRadius = 0.22f;
    public float torsoMass = 1.0f; // mass used only for impulse scaling
    public float footMass = 0.6f;

    [Header("Initial layout")]
    public Vector2 initialTorsoRel = new Vector2(0f, 1.0f); // relative to this.transform.position
    public Vector2 initialLeftRel = new Vector2(-0.35f, 0f);
    public Vector2 initialRightRel = new Vector2(0.35f, 0f);

    [Header("Constraints")]
    public float legStiffness = 0.9f; // 0..1 how strongly constraints are satisfied each iteration
    public int solverIterations = 6;   // constraint solver iterations per step

    [Header("Controls")]
    public float footMoveForce = 12f; // how strongly feet move towards input direction
    public float maxFootVel = 6f;
    public float jumpImpulse = 6.5f; // impulse applied to torso on jump
    public float jumpBufferTime = 0.12f;
    public float coyoteTime = 0.10f;

    [Header("Visuals (optional)")]
    public Transform torsoVisual;
    public Transform leftFootVisual;
    public Transform rightFootVisual;
    public bool autoCreateVisuals = true;

    // internal particle state (Verlet)
    struct Particle { public Vector2 pos; public Vector2 prev; public float radius; public float mass; }
    Particle torso, leftFoot, rightFoot;

    // rest lengths
    float leftLegRest, rightLegRest, feetRest;

    // input helpers
    bool jumpRequested = false;
    float jumpBufferCounter = 0f;
    float coyoteCounter = 0f;

    void Awake()
    {
        // ensure we operate on fixed dt
        dt = Time.fixedDeltaTime;
    }

    void Start()
    {
        Vector2 worldOrigin = (Vector2)transform.position;
        torso.pos = worldOrigin + initialTorsoRel;
        leftFoot.pos = worldOrigin + initialLeftRel;
        rightFoot.pos = worldOrigin + initialRightRel;

        // initialize prev positions (start at rest)
        torso.prev = torso.pos;
        leftFoot.prev = leftFoot.pos;
        rightFoot.prev = rightFoot.pos;

        torso.radius = torsoRadius; torso.mass = torsoMass;
        leftFoot.radius = footRadius; leftFoot.mass = footMass;
        rightFoot.radius = footRadius; rightFoot.mass = footMass;

        leftLegRest = Vector2.Distance(torso.pos, leftFoot.pos);
        rightLegRest = Vector2.Distance(torso.pos, rightFoot.pos);
        feetRest = Vector2.Distance(leftFoot.pos, rightFoot.pos);

        if (autoCreateVisuals) CreateVisualsIfMissing();
        UpdateVisualsImmediate();
    }

    void Update()
    {
        // capture jump in Update to avoid losing short presses
        if (playerId == 1)
        {
            if (Input.GetKeyDown(KeyCode.W)) { jumpRequested = true; jumpBufferCounter = jumpBufferTime; }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.UpArrow)) { jumpRequested = true; jumpBufferCounter = jumpBufferTime; }
        }

        // optional: could capture directional input here if you prefer
    }

    void FixedUpdate()
    {
        // step size
        float stepDt = Time.fixedDeltaTime;

        // perform Verlet integration for each particle
        VerletIntegrate(ref torso, stepDt);
        VerletIntegrate(ref leftFoot, stepDt);
        VerletIntegrate(ref rightFoot, stepDt);

        // simple ground detection for coyote time: if any foot touching ground, reset coyote
        bool anyFootOnGround = ParticleOnGround(leftFoot) || ParticleOnGround(rightFoot) || ParticleOnGround(torso);
        if (anyFootOnGround) coyoteCounter = coyoteTime; else coyoteCounter = Mathf.Max(0f, coyoteCounter - stepDt);

        // solve constraints multiple times
        for (int i = 0; i < solverIterations; i++)
        {
            SolveConstraints();
            // ground collisions also inside solver to avoid penetration accumulation
            SolveGroundCollision(ref torso);
            SolveGroundCollision(ref leftFoot);
            SolveGroundCollision(ref rightFoot);
        }

        // apply simple input forces to feet by modifying positions (Verlet-friendly)
        ApplyFootControl(stepDt);

        // execute jump if requested and allowed (buffer + coyote)
        if (jumpBufferCounter > 0f) jumpBufferCounter -= stepDt; // decay buffer
        if (jumpRequested)
        {
            if (coyoteCounter > 0f)
            {
                // perform jump: add upward impulse to torso (modify prev to create velocity)
                ApplyImpulseToParticle(ref torso, Vector2.up * jumpImpulse);
                jumpRequested = false;
                jumpBufferCounter = 0f;
                coyoteCounter = 0f;
            }
            else if (jumpBufferCounter <= 0f)
            {
                // buffer expired
                jumpRequested = false;
            }
        }

        // update visuals
        UpdateVisuals();
    }

    void VerletIntegrate(ref Particle p, float dtLocal)
    {
        Vector2 accel = new Vector2(0f, gravity);
        Vector2 temp = p.pos;
        p.pos += (p.pos - p.prev) + accel * dtLocal * dtLocal;
        p.prev = temp;
    }

    bool ParticleOnGround(Particle p)
    {
        return p.pos.y <= groundY + p.radius + 0.001f;
    }

    void SolveConstraints()
    {
        // torso <-> leftFoot
        SolveDistanceConstraint(ref torso, ref leftFoot, leftLegRest, legStiffness);
        // torso <-> rightFoot
        SolveDistanceConstraint(ref torso, ref rightFoot, rightLegRest, legStiffness);
        // left <-> right (keeps feet from collapsing)
        SolveDistanceConstraint(ref leftFoot, ref rightFoot, feetRest, legStiffness * 0.7f);

        // optional: keep torso above feet a bit (soft constraint)
        // ensure torso is at least some height above average feet
        float minTorsoHeight = 0.6f; // tweak as desired
        float feetAvgY = (leftFoot.pos.y + rightFoot.pos.y) * 0.5f;
        if (torso.pos.y < feetAvgY + minTorsoHeight)
        {
            torso.pos.y = Mathf.Lerp(torso.pos.y, feetAvgY + minTorsoHeight, 0.6f);
        }
    }

    void SolveDistanceConstraint(ref Particle A, ref Particle B, float restLength, float stiffness)
    {
        Vector2 delta = B.pos - A.pos;
        float dist = delta.magnitude;
        if (dist == 0f) return;
        float diff = (dist - restLength) / dist;

        // distribute correction by inverse mass (simple: mass weight)
        float invMassA = 1f / Mathf.Max(0.0001f, A.mass);
        float invMassB = 1f / Mathf.Max(0.0001f, B.mass);
        float invSum = invMassA + invMassB;
        if (invSum == 0f) return;

        Vector2 correction = delta * diff * stiffness;
        A.pos += correction * (invMassA / invSum);
        B.pos -= correction * (invMassB / invSum);
    }

    void SolveGroundCollision(ref Particle p)
    {
        float minY = groundY + p.radius;
        if (p.pos.y < minY)
        {
            p.pos.y = minY;
            // remove downward velocity by syncing prev pos y
            p.prev.y = p.pos.y;

            // apply friction to horizontal movement (simple)
            float friction = 0.15f;
            Vector2 vel = (p.pos - p.prev) / dt;
            vel.x *= (1f - friction);
            p.prev.x = p.pos.x - vel.x * dt;
        }
    }

    void ApplyFootControl(float stepDt)
    {
        float inputX = 0f;
        if (playerId == 1)
        {
            inputX = (Input.GetKey(KeyCode.D) ? 1f : 0f) + (Input.GetKey(KeyCode.A) ? -1f : 0f);
        }
        else
        {
            inputX = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) + (Input.GetKey(KeyCode.LeftArrow) ? -1f : 0f);
        }

        // Move feet horizontally by nudging their positions (Verlet friendly)
        Vector2 desiredNudge = new Vector2(inputX * footMoveForce * stepDt, 0f);

        // Apply to both feet with slight offset (you may want only one foot depending on animation)
        if (inputX != 0f)
        {
            // limit resulting velocity
            ApplyPositionNudge(ref leftFoot, desiredNudge, maxFootVel: maxFootVel);
            ApplyPositionNudge(ref rightFoot, desiredNudge, maxFootVel: maxFootVel);
        }
    }

    void ApplyPositionNudge(ref Particle p, Vector2 nudge, float maxFootVel)
    {
        // Convert nudge into a change of previous position (so it affects velocity)
        Vector2 vel = (p.pos - p.prev) / dt;
        vel += nudge / p.mass; // small acceleration effect
        vel.x = Mathf.Clamp(vel.x, -maxFootVel, maxFootVel);
        p.prev = p.pos - vel * dt;
    }

    // apply an impulse (in velocity units) to a particle (Verlet-friendly)
    private void ApplyImpulseToParticle(ref Particle p, Vector2 impulse)
    {
        // impulse: change in momentum -> deltaV = impulse / m
        Vector2 deltaV = impulse / p.mass;
        // convert deltaV to prev position change: prev = pos - (vel + deltaV) * dt
        Vector2 vel = (p.pos - p.prev) / dt;
        vel += deltaV;
        p.prev = p.pos - vel * dt;
    }

    // wrapper for external calls (e.g. ball) to apply impulse to torso
    public void ApplyImpulseToTorso(Vector2 impulse)
    {
        ApplyImpulseToParticle(ref torso, impulse);
    }

    // visuals
    void CreateVisualsIfMissing()
    {
        if (torsoVisual == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "TorsoVis_" + name;
            go.transform.localScale = Vector3.one * torsoRadius * 2f;
            go.transform.parent = transform;
            torsoVisual = go.transform;
            Destroy(go.GetComponent<Collider>());
        }
        if (leftFootVisual == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "LeftFootVis_" + name;
            go.transform.localScale = Vector3.one * footRadius * 2f;
            go.transform.parent = transform;
            leftFootVisual = go.transform;
            Destroy(go.GetComponent<Collider>());
        }
        if (rightFootVisual == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "RightFootVis_" + name;
            go.transform.localScale = Vector3.one * footRadius * 2f;
            go.transform.parent = transform;
            rightFootVisual = go.transform;
            Destroy(go.GetComponent<Collider>());
        }
    }

    void UpdateVisuals()
    {
        if (torsoVisual != null) torsoVisual.position = new Vector3(torso.pos.x, torso.pos.y, torsoVisual.position.z);
        if (leftFootVisual != null) leftFootVisual.position = new Vector3(leftFoot.pos.x, leftFoot.pos.y, leftFootVisual.position.z);
        if (rightFootVisual != null) rightFootVisual.position = new Vector3(rightFoot.pos.x, rightFoot.pos.y, rightFootVisual.position.z);
    }

    void UpdateVisualsImmediate()
    {
        UpdateVisuals();
    }
}
