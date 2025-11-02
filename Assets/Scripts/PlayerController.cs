using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Mecánica básica")]
    public float m = 3f;
    public float e = 0.15f;          // restitución baja para no botar
    public float jumpImpulse = 7f;
    public float suelo = 0f;         // Y del piso (mundo)
    public float g = 9.8f;

    [Header("Fricción")]
    public float friccionSuelo = 20f;     // freno en suelo (dinámica)
    public float airLinearDamping = 0.0f; // ≈0 para conservar impulso en el aire

    // -------- WOBBLE (lento y controlable) --------
    [Header("Wobble (oscilador)")]
    public float wobbleHzGround = 1.6f;
    public float wobbleHzAir = 1.8f;
    public float wobbleAmpHighGroundDeg = 34f;   // más inclinación visible
    public float wobbleAmpHighAirDeg = 28f;
    public float wobbleAmpIdleGroundDeg = 1.2f;
    public float wobbleAmpRise = 28f;
    public float wobbleAmpFall = 24f;
    public float wobbleActiveHold = 0.55f;

    [Header("Suavizado del ángulo")]
    public float angleSlewDegPerSec = 520f;

    [Header("Tilt boost / límites")]
    public float tiltAmplify = 1.35f;       // multiplica la inclinación siempre
    public float jumpTiltBoostMult = 1.5f;  // multiplicador temporal tras saltar
    public float jumpTiltBoostTime = 0.22f; // duración del boost
    public float airTiltGain = 1.15f;       // en el aire inclina un poco más
    public float maxTiltDeg = 60f;          // clamp de seguridad

    // --------- Desplazamiento basado en ROTACIÓN REAL ---------
    [Header("Salto lateral (usa transform.up.x)")]
    [Tooltip("Si tu escena invierte ejes (flip/escala negativa), marca esto.")]
    public bool invertLeanSign = false;

    [Tooltip("Fuerza lateral del salto (m/s). Sube si quieres aún más desplazamiento.")]
    public float jumpLateralStrength = 14f;

    [Tooltip("Curvatura |lean|^pow. 1.6–2.2: poco cerca de 0°, mucho en extremos.")]
    public float jumpLateralPow = 1.9f;

    [Tooltip("Empuje continuo en el aire tras saltar (m/s²)")]
    public float airFollowStrength = 10f;

    [Tooltip("Duración del follow-thrust (s)")]
    public float airFollowTime = 0.20f;

    [Tooltip("Límite de |vx| en el aire")]
    public float maxAirSpeed = 15f;

    [Header("Empuje al aterrizar")]
    [Tooltip("Multiplica |vy impacto|")]
    public float landSideGain = 0.55f;
    public float landPushMinSpeed = 2.2f;
    public float landPushCooldown = 0.18f;

    [Header("Auto-sleep (reposo real)")]
    public bool enableAutoSleep = true;
    public float sleepTime = 0.7f;
    public float sleepVelThreshold = 0.04f;
    public float sleepAngleThreshold = 2.5f;
    public float sleepReturnRate = 200f;

    [Header("Suelo (stabilidad extra)")]
    public float groundSnap = 0.03f;
    public float groundEnterMargin = 0.02f;
    public float groundExitMargin = 0.05f;
    public float minBounceSpeed = 1.6f;
    public float liftOffThreshold = 0.25f;
    public float staticFrictionVel = 0.12f;
    public float staticFrictionAccel = 120f;
    public float maxGroundSpeed = 7.5f;

    [Header("Estado (lectura)")]
    public Vector2 posicion;
    public Vector2 velocidad;
    public bool onGround;
    public bool jumpRequest;

    [Header("Input")]
    public int playerId = 1; // 1=W, 2=UpArrow

    [Header("Colisión (cápsula inclinada)")]
    public float colRadius = 0.40f;
    public float colHalfHeight = 1.00f;
    public Vector2 colCenterOffset = new Vector2(0.00f, 0.90f);

    // ============ PIERNA DE PATADA ============
    [Header("Pierna de patada")]
    [Tooltip("Activa la pierna que sube al saltar para patear la pelota.")]
    public bool enableKickLeg = true;

    [Tooltip("Offset del 'hip' respecto al centro de colisión (local al player). x=right, y=up")]
    public Vector2 legHipLocal = new Vector2(0.10f, 0.55f);

    [Tooltip("Largo de la pierna (m)")]
    public float legLength = 0.65f;

    [Tooltip("Radio del segmento (m) usado para colisión")]
    public float legRadius = 0.12f;

    [Tooltip("Ángulo de reposo de la pierna (deg, 0 = hacia +right del player)")]
    public float legRestAngleDeg = -10f;

    [Tooltip("Ángulo adicional en el swing de subida (deg)")]
    public float legSwingAngleDeg = 85f;

    [Tooltip("Tiempo de subida de la pierna (s)")]
    public float legSwingTime = 0.22f;

    [Tooltip("Tiempo de regreso (s)")]
    public float legRetractTime = 0.18f;

    [Tooltip("Restitución efectiva de la pierna (0–1). Más alto = patada más viva")]
    public float legKickRestitution = 0.85f;

    [Tooltip("Velocidad extra mínima (m/s) añadida al balón en la normal del impacto durante el swing")]
    public float legExtraKickSpeed = 4.5f;

    // Lectura/depuración de la pierna
    [HideInInspector] public Vector2 legHipWorld;     // pivote en mundo
    [HideInInspector] public Vector2 legTipWorld;     // punta en mundo
    [HideInInspector] public Vector2 legTipVelWorld;  // velocidad de la punta
    [HideInInspector] public float legAngleWorldDeg; // ángulo actual (mundo, relativo a right)

    // Internos wobble
    float phase, currentAmpDeg, visualAngleDeg, wDegPerSec, activeTimer;
    bool sleeping; float sleepTimer;

    // Internos piso & empujes
    float landPushTimer;
    float followTimer;   // tiempo restante de follow-thrust en aire
    float jumpLeanSign;  // dirección capturada al despegar
    float tiltBoostTimer;

    // Internos pierna
    float legTimer;            // cuenta regresiva del ciclo (s)
    float legPrevTipX, legPrevTipY;
    bool legActive;           // true mientras hay swing/retract
    bool legSwingPhase;       // true durante subida (fase de golpe)

    void Start()
    {
        posicion = transform.position;
        currentAmpDeg = wobbleAmpIdleGroundDeg;
        visualAngleDeg = 0f;
        phase = 0f;

        // init pierna
        legTimer = 0f;
        legActive = false;
        legSwingPhase = false;
        legPrevTipX = transform.position.x;
        legPrevTipY = transform.position.y;
    }

    void Update()
    {
        if (playerId == 1) { if (Input.GetKeyDown(KeyCode.W)) jumpRequest = true; }
        else { if (Input.GetKeyDown(KeyCode.UpArrow)) jumpRequest = true; }
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (landPushTimer > 0f) landPushTimer -= dt;
        if (followTimer > 0f) followTimer -= dt;
        if (tiltBoostTimer > 0f) tiltBoostTimer -= dt;

        // ---------- WOBBLE ----------
        bool lowSpeed = (Mathf.Abs(velocidad.x) < sleepVelThreshold && Mathf.Abs(velocidad.y) < sleepVelThreshold);
        bool lowAngle = Mathf.Abs(visualAngleDeg) < sleepAngleThreshold;

        if (jumpRequest || !onGround || !lowSpeed) sleeping = false;
        if (activeTimer > 0f) activeTimer -= dt;

        float targetAmp =
            sleeping ? 0f :
            (!onGround ? wobbleAmpHighAirDeg :
             (activeTimer > 0f ? wobbleAmpHighGroundDeg : wobbleAmpIdleGroundDeg));

        float ampRate = (targetAmp > currentAmpDeg) ? wobbleAmpRise : wobbleAmpFall;
        currentAmpDeg = Mathf.MoveTowards(currentAmpDeg, targetAmp, ampRate * dt);

        float hz = onGround ? wobbleHzGround : wobbleHzAir;
        if (!sleeping) phase += 2f * Mathf.PI * Mathf.Max(0.01f, hz) * dt;

        float amp = currentAmpDeg * (onGround ? 1f : airTiltGain);
        float targetAngle = amp * Mathf.Sin(phase);

        targetAngle *= tiltAmplify;

        if (tiltBoostTimer > 0f)
        {
            float s = Mathf.Clamp01(tiltBoostTimer / jumpTiltBoostTime); // 1→0
            float smooth = s * s * (3f - 2f * s);
            float factor = Mathf.Lerp(1f, jumpTiltBoostMult, smooth);
            targetAngle *= factor;
        }

        if (Mathf.Abs(wDegPerSec) > 0.01f)
        {
            targetAngle += wDegPerSec * 0.02f;
            wDegPerSec = Mathf.MoveTowards(wDegPerSec, 0f, 120f * dt);
        }

        if (enableAutoSleep && onGround && lowSpeed && lowAngle && currentAmpDeg <= wobbleAmpIdleGroundDeg + 0.2f)
        {
            sleepTimer += dt;
            if (sleepTimer >= sleepTime) sleeping = true;
        }
        else sleepTimer = 0f;

        if (sleeping) targetAngle = Mathf.MoveTowards(targetAngle, 0f, sleepReturnRate * dt);

        targetAngle = Mathf.Clamp(targetAngle, -maxTiltDeg, maxTiltDeg);
        visualAngleDeg = Mathf.MoveTowards(visualAngleDeg, targetAngle, angleSlewDegPerSec * dt);
        transform.rotation = Quaternion.Euler(0f, 0f, visualAngleDeg);

        // ---------- SALTO ----------
        if (jumpRequest && onGround)
        {
            // impulso vertical
            Vector2 up = transform.up.normalized;
            velocidad += up * jumpImpulse;

            // Lean horizontal REAL
            float leanRaw = LeanRaw();                // [-1..+1]
            jumpLeanSign = Mathf.Sign(leanRaw == 0f ? (Random.value - 0.5f) : leanRaw);

            float dir = Mathf.Sign(leanRaw);
            float mag = Mathf.Pow(Mathf.Abs(leanRaw), jumpLateralPow);
            velocidad.x += dir * mag * jumpLateralStrength;

            if (Mathf.Abs(velocidad.x) > maxAirSpeed)
                velocidad.x = Mathf.Sign(velocidad.x) * maxAirSpeed;

            followTimer = airFollowTime;
            tiltBoostTimer = jumpTiltBoostTime;

            onGround = false;
            jumpRequest = false;
            activeTimer = wobbleActiveHold;
            sleeping = false;

            // ---- Disparar pierna ----
            if (enableKickLeg)
            {
                legTimer = legSwingTime + legRetractTime;
                legSwingPhase = true;   // primero sube (golpea)
                legActive = true;
            }
        }
        else jumpRequest = false;

        // ---------- FOLLOW-THRUST aire ----------
        if (!onGround && followTimer > 0f)
        {
            float leanRaw = LeanRaw();
            float dir = Mathf.Sign(leanRaw == 0f ? jumpLeanSign : leanRaw);
            float mag = Mathf.Pow(Mathf.Abs(leanRaw), jumpLateralPow);
            velocidad.x += dir * mag * airFollowStrength * dt;

            if (Mathf.Abs(velocidad.x) > maxAirSpeed)
                velocidad.x = Mathf.Sign(velocidad.x) * maxAirSpeed;
        }

        // ---------- GRAVEDAD / AIRE ----------
        velocidad.y += -g * dt;
        if (!onGround && airLinearDamping > 0f)
            velocidad.x *= Mathf.Clamp01(1f - airLinearDamping * dt);

        // ---------- INTEGRACIÓN ----------
        posicion += velocidad * dt;

        // ---------- SUELO con cápsula ----------
        GetCapsuleSegmentWorldAt(posicion, out var bot, out var top);
        float lowestCircleY = Mathf.Min(bot.y, top.y);
        float lowestY = lowestCircleY - colRadius;
        float distToFloor = lowestY - suelo;

        if (distToFloor <= 0f)
        {
            float impactoVel = velocidad.y;
            float dy = -distToFloor;
            posicion.y += dy;

            if (impactoVel < 0f)
            {
                if (Mathf.Abs(impactoVel) < minBounceSpeed) velocidad.y = 0f;
                else velocidad.y = -impactoVel * e;

                if (Mathf.Abs(impactoVel) >= landPushMinSpeed && landPushTimer <= 0f)
                {
                    float leanRaw = LeanRaw();
                    float dir = Mathf.Sign(leanRaw == 0f ? jumpLeanSign : leanRaw);
                    float mag = Mathf.Pow(Mathf.Abs(leanRaw), jumpLateralPow);
                    velocidad.x += Mathf.Abs(impactoVel) * landSideGain * dir * mag;
                    landPushTimer = landPushCooldown;
                    activeTimer = wobbleActiveHold;
                    sleeping = false;
                }
            }

            if (Mathf.Abs(velocidad.y) < liftOffThreshold) velocidad.y = 0f;

            velocidad.x = Mathf.MoveTowards(velocidad.x, 0f, friccionSuelo * 0.4f * dt);
            onGround = true;
        }
        else
        {
            if (onGround)
            {
                if (distToFloor <= groundExitMargin)
                {
                    if (distToFloor <= groundSnap) posicion.y += -distToFloor;
                    if (velocidad.y > 0f && velocidad.y < liftOffThreshold) velocidad.y = 0f;
                    onGround = true;
                }
                else onGround = false;
            }
            else
            {
                if (distToFloor <= groundEnterMargin)
                {
                    if (distToFloor <= groundSnap) posicion.y += -distToFloor;
                    if (velocidad.y > 0f) velocidad.y = 0f;
                    onGround = true;
                }
                else onGround = false;
            }
        }

        // Fricción en piso (dinámica + estática) + límite
        if (onGround)
        {
            velocidad.x = Mathf.MoveTowards(velocidad.x, 0f, friccionSuelo * dt);
            if (Mathf.Abs(velocidad.x) < staticFrictionVel)
                velocidad.x = Mathf.MoveTowards(velocidad.x, 0f, staticFrictionAccel * dt);
            velocidad.x = Mathf.Clamp(velocidad.x, -maxGroundSpeed, maxGroundSpeed);
        }

        // ---------- TRANSFORM ----------
        transform.position = new Vector3(posicion.x, posicion.y, transform.position.z);

        // ---------- PIERNA: cinemática y posición/vel ----------
        UpdateLegKinematics(dt);
    }

    // === Lean horizontal REAL: transform.up.x (independiente de Euler y robusto a wraps) ===
    float LeanRaw()
    {
        float x = transform.up.x; // >0: cabeza hacia la DERECHA en pantalla
        return invertLeanSign ? -x : x;
    }

    // Segmento de cápsula en mundo
    public void GetCapsuleSegmentWorldAt(Vector2 center, out Vector2 bot, out Vector2 top)
    {
        Vector2 up = transform.up.normalized;
        Vector2 right = transform.right.normalized;
        Vector2 c = center + right * colCenterOffset.x + up * colCenterOffset.y;
        top = c + up * colHalfHeight;
        bot = c - up * colHalfHeight;
    }

    // ============ PIERNA: mundo ============
    void UpdateLegKinematics(float dt)
    {
        // calcular ángulo de la pierna (local al player: 0° = +right)
        float localDeg = legRestAngleDeg;

        if (enableKickLeg && legActive)
        {
            legTimer -= dt;
            if (legSwingPhase)
            {
                float t = Mathf.Clamp01((legSwingTime - Mathf.Max(0f, legTimer - legRetractTime)) / Mathf.Max(0.0001f, legSwingTime));
                // ease-out (golpe rápido al inicio)
                float s = 1f - (1f - t) * (1f - t);
                localDeg = legRestAngleDeg + legSwingAngleDeg * s;

                if (t >= 0.999f) legSwingPhase = false; // pasa a retract
            }
            else
            {
                float t = Mathf.Clamp01((legRetractTime - Mathf.Max(0f, legTimer)) / Mathf.Max(0.0001f, legRetractTime));
                // ease-in (regreso suave)
                float s = t * t;
                localDeg = legRestAngleDeg + legSwingAngleDeg * (1f - s);
            }

            if (legTimer <= 0f)
            {
                legActive = false;
                legSwingPhase = false;
                localDeg = legRestAngleDeg;
            }
        }

        // convertir a mundo
        Vector2 up = transform.up.normalized;
        Vector2 right = transform.right.normalized;

        // centro de colisión (como cápsula)
        Vector2 c = posicion + right * colCenterOffset.x + up * colCenterOffset.y;
        // hip en mundo
        legHipWorld = c + right * legHipLocal.x + up * legHipLocal.y;

        // dirección de la pierna en mundo: rotar "right" por localDeg alrededor de Z
        float rad = localDeg * Mathf.Deg2Rad;
        Vector2 dirLocal = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)); // en coords del player (right, up)
        Vector2 dirWorld = (right * dirLocal.x + up * dirLocal.y).normalized;

        legTipWorld = legHipWorld + dirWorld * Mathf.Max(0.01f, legLength);
        legAngleWorldDeg = localDeg + Vector2.SignedAngle(Vector2.right, right); // info útil para debug

        // velocidad de la punta (diferencia simple frame a frame + velocidad del cuerpo ya incluida por posicion)
        Vector2 prevTip = new Vector2(legPrevTipX, legPrevTipY);
        legTipVelWorld = (legTipWorld - prevTip) / Mathf.Max(0.0001f, dt);
        legPrevTipX = legTipWorld.x;
        legPrevTipY = legTipWorld.y;
    }

    // Entrega el segmento de la pierna (para colisiones)
    public void GetLegSegmentWorld(out Vector2 hip, out Vector2 tip, out float radius)
    {
        hip = legHipWorld;
        tip = legTipWorld;
        radius = Mathf.Max(0.0001f, legRadius);
    }

    // ¿Está en fase de golpe?
    public bool IsLegStriking() => enableKickLeg && legActive && legSwingPhase;

    // Gizmo para ajustar cápsula y pierna
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector2 up = transform.up.normalized;
        Vector2 right = transform.right.normalized;
        Vector2 center = (Application.isPlaying ? posicion : (Vector2)transform.position)
                       + right * colCenterOffset.x
                       + up * colCenterOffset.y;

        Vector2 a = center + up * colHalfHeight;
        Vector2 b = center - up * colHalfHeight;

        DrawCircle(a, colRadius);
        DrawCircle(b, colRadius);
        Gizmos.DrawLine(a + right * colRadius, b + right * colRadius);
        Gizmos.DrawLine(a - right * colRadius, b - right * colRadius);

        // Pierna
        Gizmos.color = Color.cyan;
        Vector2 hip, tip;
        float r;
        if (Application.isPlaying)
        {
            GetLegSegmentWorld(out hip, out tip, out r);
        }
        else
        {
            Vector2 hipLocal = new Vector2(0.10f, 0.55f);
            Vector2 hipPreview = center + right * hipLocal.x + up * hipLocal.y;
            float rad = legRestAngleDeg * Mathf.Deg2Rad;
            Vector2 dirLocal = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            Vector2 dirWorld = (right * dirLocal.x + up * dirLocal.y).normalized;
            hip = hipPreview;
            tip = hip + dirWorld * legLength;
            r = legRadius;
        }

        DrawCircle(hip, r);
        DrawCircle(tip, r);
        Vector2 side = Vector2.Perpendicular((tip - hip).normalized) * r;
        Gizmos.DrawLine(hip + side, tip + side);
        Gizmos.DrawLine(hip - side, tip - side);

        static void DrawCircle(Vector2 p, float r0)
        {
            const int N = 24;
            Vector3 prev = p + new Vector2(r0, 0);
            for (int i = 1; i <= N; i++)
            {
                float ang = (i / (float)N) * Mathf.PI * 2f;
                Vector3 q = p + new Vector2(Mathf.Cos(ang) * r0, Mathf.Sin(ang) * r0);
                Gizmos.DrawLine(prev, q);
                prev = q;
            }
        }
    }
}
