using UnityEngine;

public class Colisiones : MonoBehaviour
{
    [Header("Referencias")]
    public Ball ball;
    public PlayerController playerA;
    public PlayerController playerB;

    [Header("Ajustes de penetración")]
    [Range(0f, 1f)] public float penetrationPercent = 0.8f;
    public float penetrationSlop = 0.001f;

    // === NUEVO: controles de suavidad de la patada ===
    [Header("Patada - suavizado / límites")]
    [Tooltip("Escala global del impulso generado por la pierna.")]
    [Range(0f, 1f)] public float legImpulseScale = 0.55f;

    [Tooltip("Velocidad máx. permitida para el balón justo después de una patada.")]
    public float legMaxPostSpeed = 10f;

    [Tooltip("Factor de blending para no cambiar bruscamente la velocidad al hacer clamp.")]
    [Range(0f, 1f)] public float legClampBlend = 0.35f;

    void FixedUpdate()
    {
        if (playerA && playerB) ResolveCapsuleVsCapsule(playerA, playerB);

        // Pierna primero (con límites)
        if (playerA && ball) ResolveLegVsCircle(playerA, ball);
        if (playerB && ball) ResolveLegVsCircle(playerB, ball);

        // Cuerpo después
        if (playerA && ball) ResolveCapsuleVsCircle(playerA, ball);
        if (playerB && ball) ResolveCapsuleVsCircle(playerB, ball);
    }

    static Vector2 ClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude);
        t = Mathf.Clamp01(t);
        return a + ab * t;
    }

    static void ClosestPointsBetweenSegments(Vector2 p1, Vector2 q1, Vector2 p2, Vector2 q2,
                                             out Vector2 c1, out Vector2 c2)
    {
        Vector2 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
        float a = Vector2.Dot(d1, d1), e = Vector2.Dot(d2, d2), f = Vector2.Dot(d2, r);
        float s, t;

        if (a <= 1e-8f && e <= 1e-8f) { s = 0; t = 0; }
        else if (a <= 1e-8f) { s = 0; t = Mathf.Clamp01(f / e); }
        else
        {
            float c = Vector2.Dot(d1, r);
            if (e <= 1e-8f) { t = 0; s = Mathf.Clamp01(-c / a); }
            else
            {
                float b = Vector2.Dot(d1, d2);
                float denom = a * e - b * b;
                s = denom != 0 ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                t = Mathf.Clamp01((b * s + f) / e);
                s = Mathf.Clamp01((b * t - c) / a);
            }
        }
        c1 = p1 + d1 * s;
        c2 = p2 + d2 * t;
    }

    static void GetCapsuleSegmentWorld(PlayerController p, Vector2 playerCenter,
                                       out Vector2 bot, out Vector2 top, out float radius)
    {
        Vector2 up = p.transform.up.normalized;
        Vector2 right = p.transform.right.normalized;
        Vector2 center = playerCenter + right * p.colCenterOffset.x + up * p.colCenterOffset.y;
        top = center + up * p.colHalfHeight;
        bot = center - up * p.colHalfHeight;
        radius = Mathf.Max(0.0001f, p.colRadius);
    }

    // ==== Jugador (pierna) vs Pelota (círculo) con clamp de impulso/velocidad ====
    bool ResolveLegVsCircle(PlayerController p, Ball b)
    {
        if (!p || !p.enableKickLeg) return false;

        p.GetLegSegmentWorld(out var hip, out var tip, out var rA);

        Vector2 posB = b.position;
        Vector2 closest = ClosestPointOnSegment(hip, tip, posB);
        Vector2 delta = posB - closest;
        float dist = delta.magnitude;
        float rB = Mathf.Max(0.0001f, b.radius);
        float rSum = rA + rB;

        if (dist >= rSum && dist > 0f) return false;

        Vector2 n = (dist > 1e-6f) ? (delta / dist) : Vector2.right;
        float penetration = Mathf.Max(0f, rSum - dist);

        if (penetration > penetrationSlop)
        {
            Vector2 correction = n * (penetrationPercent * penetration);
            posB += correction;
        }

        Vector2 velB = b.velocity;
        Vector2 velSurface = p.legTipVelWorld; // “superficie” móvil
        Vector2 relV = velB - velSurface;
        float vn = Vector2.Dot(relV, n);

        if (vn > 0f)
        {
            b.position = posB;
            b.transform.position = new Vector3(posB.x, posB.y, b.transform.position.z);
            return true;
        }

        float eEff = Mathf.Clamp01(Mathf.Max(b.e, p.legKickRestitution));

        float invB = 1f / Mathf.Max(0.0001f, b.masa);

        float j = -(1f + eEff) * vn / invB;

        // === NUEVO: limitar el impulso de la pierna ===
        j *= Mathf.Clamp01(legImpulseScale);

        Vector2 impulse = j * n;
        velB += impulse * invB;

        if (p.IsLegStriking())
        {
            // boost pero suave (ya estaba en PlayerController)
            velB += n * p.legExtraKickSpeed;
        }

        // === NUEVO: clamp de velocidad post-patada ===
        float vmag = velB.magnitude;
        if (vmag > legMaxPostSpeed)
        {
            // blending para que el recorte no se sienta brusco
            Vector2 clamped = velB.normalized * legMaxPostSpeed;
            velB = Vector2.Lerp(velB, clamped, Mathf.Clamp01(legClampBlend));
        }

        b.position = posB; b.velocity = velB;
        b.transform.position = new Vector3(posB.x, posB.y, b.transform.position.z);
        return true;
    }

    // ==== Jugador (cápsula) vs Pelota (círculo) ====
    bool ResolveCapsuleVsCircle(PlayerController p, Ball b)
    {
        Vector2 posA = p.posicion;
        Vector2 velA = p.velocidad;
        float mA = Mathf.Max(0.0001f, p.m);
        float eA = Mathf.Clamp01(p.e);

        Vector2 posB = b.position;
        Vector2 velB = b.velocity;
        float mB = Mathf.Max(0.0001f, b.masa);
        float rB = Mathf.Max(0.0001f, b.radius);
        float eB = Mathf.Clamp01(b.e);

        GetCapsuleSegmentWorld(p, posA, out var botA, out var topA, out var rA);

        Vector2 closest = ClosestPointOnSegment(botA, topA, posB);
        Vector2 delta = posB - closest;
        float dist = delta.magnitude;
        float rSum = rA + rB;

        if (dist >= rSum && dist > 0f) return false;

        Vector2 n = (dist > 1e-6f) ? (delta / dist) : Vector2.right;
        float penetration = Mathf.Max(0f, rSum - dist);

        float invA = 1f / mA, invB = 1f / mB, invSum = invA + invB;
        if (penetration > penetrationSlop && invSum > 0f)
        {
            Vector2 correction = n * (penetrationPercent * penetration / invSum);
            posA -= correction * invA;
            posB += correction * invB;
        }

        Vector2 relV = velB - velA;
        float vn = Vector2.Dot(relV, n);
        if (vn > 0f)
        {
            p.posicion = posA; b.position = posB;
            p.transform.position = new Vector3(posA.x, posA.y, p.transform.position.z);
            b.transform.position = new Vector3(posB.x, posB.y, b.transform.position.z);
            return true;
        }

        float e = Mathf.Min(eA, eB);
        float j = -(1f + e) * vn / invSum;
        Vector2 impulse = j * n;

        velA -= impulse * invA;
        velB += impulse * invB;

        p.posicion = posA; p.velocidad = velA;
        b.position = posB; b.velocity = velB;
        p.transform.position = new Vector3(posA.x, posA.y, p.transform.position.z);
        b.transform.position = new Vector3(posB.x, posB.y, b.transform.position.z);
        return true;
    }

    // ==== Jugador (cápsula) vs Jugador (cápsula) ====
    bool ResolveCapsuleVsCapsule(PlayerController A, PlayerController B)
    {
        Vector2 posA = A.posicion, velA = A.velocidad;
        float mA = Mathf.Max(0.0001f, A.m), eA = Mathf.Clamp01(A.e);

        Vector2 posB = B.posicion, velB = B.velocidad;
        float mB = Mathf.Max(0.0001f, B.m), eB = Mathf.Clamp01(B.e);

        GetCapsuleSegmentWorld(A, posA, out var botA, out var topA, out var rA);
        GetCapsuleSegmentWorld(B, posB, out var botB, out var topB, out var rB);

        ClosestPointsBetweenSegments(botA, topA, botB, topB, out var cA, out var cB);

        Vector2 delta = cB - cA;
        float dist = delta.magnitude;
        float rSum = rA + rB;

        if (dist >= rSum && dist > 0f) return false;

        Vector2 n = (dist > 1e-6f) ? (delta / dist) : Vector2.right;
        float penetration = Mathf.Max(0f, rSum - dist);

        float invA = 1f / mA, invB = 1f / mB, invSum = invA + invB;

        if (penetration > penetrationSlop && invSum > 0f)
        {
            Vector2 correction = n * (penetrationPercent * penetration / invSum);
            posA -= correction * invA;
            posB += correction * invB;
        }

        Vector2 relV = velB - velA;
        float vn = Vector2.Dot(relV, n);
        if (vn > 0f)
        {
            A.posicion = posA; B.posicion = posB;
            A.transform.position = new Vector3(posA.x, posA.y, A.transform.position.z);
            B.transform.position = new Vector3(posB.x, posB.y, B.transform.position.z);
            return true;
        }

        float e = Mathf.Min(eA, eB);
        float j = -(1f + e) * vn / invSum;
        Vector2 impulse = j * n;

        velA -= impulse * invA;
        velB += impulse * invB;

        A.posicion = posA; A.velocidad = velA;
        B.posicion = posB; B.velocidad = velB;
        A.transform.position = new Vector3(posA.x, posA.y, A.transform.position.z);
        B.transform.position = new Vector3(posB.x, posB.y, B.transform.position.z);
        return true;
    }
}
