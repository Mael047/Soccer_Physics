using UnityEngine;

public class FieldBounds : MonoBehaviour
{
    [System.Serializable]
    public class WallSegment
    {
        [Tooltip("Punto A del segmento (extremo 1)")]
        public Transform pointA;
        [Tooltip("Punto B del segmento (extremo 2)")]
        public Transform pointB;
        [Tooltip("Grosor efectivo del palo/pared (radio añadido)")]
        public float radius = 0.05f;
    }

    [Header("Referencias")]
    public Ball ball;
    public PlayerController playerA;
    public PlayerController playerB;
    public PlayerController playerA2;
    public PlayerController playerB2;

    [Header("Paredes / palos de la cancha")]
    public WallSegment[] walls;

    [Header("Corrección de penetración")]
    [Range(0f, 1f)] public float penetrationPercent = 0.8f;
    public float penetrationSlop = 0.001f;

    void LateUpdate()
    {
        float dt = Time.deltaTime;

        if (ball != null)
            ResolveBall(dt);

        if (playerA != null && playerA.gameObject.activeInHierarchy)
            ResolvePlayer(playerA, dt);

        if (playerB != null && playerB.gameObject.activeInHierarchy)
            ResolvePlayer(playerB, dt);

        if (playerA2 != null && playerA2.gameObject.activeInHierarchy)
            ResolvePlayer(playerA2, dt); 

        if (playerB2 != null && playerB2.gameObject.activeInHierarchy)
            ResolvePlayer(playerB2, dt);   
    }

    // ================== BALL ==================
    void ResolveBall(float dt)
    {
        Vector2 pos = ball.position;
        Vector2 vel = ball.velocity;
        float r = ball.radius;
        float e = Mathf.Clamp01(ball.e);

        for (int i = 0; i < walls.Length; i++)
        {
            WallSegment w = walls[i];
            if (w == null || w.pointA == null || w.pointB == null) continue;

            Vector2 A = w.pointA.position;
            Vector2 B = w.pointB.position;

            ResolveCircleVsSegment(ref pos, ref vel, r, e, A, B, w.radius);
        }

        ball.position = pos;
        ball.velocity = vel;
        ball.transform.position = new Vector3(pos.x, pos.y, ball.transform.position.z);
    }

    // ================== PLAYER ==================
    void ResolvePlayer(PlayerController p, float dt)
    {
        // Usamos el centro de la cápsula como un círculo
        Vector2 pos = p.posicion;
        Vector2 vel = p.velocidad;
        float r = p.colRadius;
        float e = Mathf.Clamp01(p.e);

        // offset del centro de la cápsula respecto al pivote del player
        Vector2 offset = p.colCenterOffset;
        Vector2 center = pos + offset;

        for (int i = 0; i < walls.Length; i++)
        {
            WallSegment w = walls[i];
            if (w == null || w.pointA == null || w.pointB == null) continue;

            Vector2 A = w.pointA.position;
            Vector2 B = w.pointB.position;

            // resolvemos sobre el centro de la cápsula
            bool collided = ResolveCircleVsSegment(ref center, ref vel, r, e, A, B, w.radius);
            if (collided)
            {
                // reposiciona el player manteniendo el offset del centro
                pos = center - offset;
            }
        }

        p.posicion = pos;
        p.velocidad = vel;
        p.transform.position = new Vector3(pos.x, pos.y, p.transform.position.z);
    }

    // ================== CÍRCULO vs SEGMENTO ==================
    // Modela pared/palo como un segmento grueso (radio = wallRadius)
    bool ResolveCircleVsSegment(
        ref Vector2 circlePos,
        ref Vector2 circleVel,
        float circleRadius,
        float restitution,
        Vector2 A,
        Vector2 B,
        float wallRadius
    )
    {
        Vector2 ab = B - A;
        float abLen2 = ab.sqrMagnitude;
        if (abLen2 < 1e-8f)
            return false; // segmento degenerado

        // Proyección del centro del círculo sobre el segmento [A,B]
        float t = Vector2.Dot(circlePos - A, ab) / abLen2;
        t = Mathf.Clamp01(t);
        Vector2 closest = A + t * ab;

        Vector2 diff = circlePos - closest;
        float dist = diff.magnitude;

        float effectiveRadius = circleRadius + wallRadius;
        float penetration = effectiveRadius - dist;
        if (penetration <= 0f)
            return false; // no hay solapamiento

        // Normal de colisión (desde pared hacia el círculo)
        Vector2 n;
        if (dist > 1e-4f)
        {
            n = diff / dist;
        }
        else
        {
            // Si está muy encima del segmento, usamos una normal perpendicular al segmento
            n = new Vector2(-ab.y, ab.x);
            float len = n.magnitude;
            if (len > 1e-4f) n /= len;
            else n = Vector2.up;
        }

        // --- Corrección de penetración suave (como en Colisiones.cs) ---
        float corrected = Mathf.Max(penetration - penetrationSlop, 0f) * penetrationPercent;
        circlePos += n * corrected;

        // --- Respuesta de velocidad (rebote contra pared estática) ---
        float vn = Vector2.Dot(circleVel, n);
        if (vn < 0f)
        {
            // reflejamos la componente normal con restitución
            circleVel = circleVel - (1f + restitution) * vn * n;
        }

        return true;
    }

    // ========== Gizmos para ver los palos/paredes ==========
    void OnDrawGizmos()
    {
        if (walls == null) return;

        Gizmos.color = Color.cyan;
        foreach (var w in walls)
        {
            if (w == null || w.pointA == null || w.pointB == null) continue;
            Vector3 a = w.pointA.position;
            Vector3 b = w.pointB.position;
            Gizmos.DrawLine(a, b);
            // dibujito de extremos
            Gizmos.DrawSphere(a, 0.03f);
            Gizmos.DrawSphere(b, 0.03f);
        }
    }
}
