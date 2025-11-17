using UnityEngine;

public class LegSpriteBinder : MonoBehaviour
{
    [Header("Refs")]
    public PlayerController player;      // arrastra aquí tu Player con el script
    public bool hideWhenInactive = false;

    [Header("Sprite / Visual")]
    [Tooltip("SpriteRenderer a manipular. Si se deja vacío, toma el del mismo GameObject.")]
    public SpriteRenderer sprite;

    [Tooltip("Nodo hijo que contiene SOLO lo visual. Escalaremos este para no tocar el collider.")]
    public Transform visualRoot; // Recomendado: un hijo con el SpriteRenderer

    [Header("Tamaño del sprite")]
    [Tooltip("Si SpriteRenderer.drawMode = Tiled/Sliced uso size; si es Simple uso escala del 'visualRoot'.")]
    public bool useSpriteSizeApi = true;

    [Tooltip("Grosor mínimo si el radio de la pierna es muy pequeño.")]
    public float minThickness = 0.02f;

    [Range(0.1f, 1f), Tooltip("Multiplica el LARGO visual del sprite (0.7 = 70% del largo real).")]
    public float visualLengthFactor = 0.75f;

    [Range(0.5f, 2f), Tooltip("Multiplica el GROSOR visual del sprite.")]
    public float visualThicknessFactor = 1.0f;

    [Header("Orientación (Opción A)")]
    [Tooltip("Mantiene el sprite visualmente 'de pie' aunque el transform rote con la pierna.")]
    public bool keepSpriteUpright = true;

    public enum LongAxis { X, Y }  // Eje del sprite que representa el LARGO (cadera->punta)
    [Tooltip("Si tu sprite es vertical, elige Y; si es horizontal, X.")]
    public LongAxis spriteLongAxis = LongAxis.X;

    [Tooltip("Corrección fina (+90, -90, 180) si tu arte apunta a otra dirección por defecto.")]
    public float axisRotationOffsetDeg = 0f;

    SpriteRenderer sr;

    void Awake()
    {
        sr = sprite ? sprite : GetComponent<SpriteRenderer>();
        if (!visualRoot && sr) visualRoot = sr.transform; // fallback: usaremos el mismo transform (ver nota abajo)
    }

    void LateUpdate()
    {
        if (!player || !sr) return;

        // 1) Segmento cadera->punta
        player.GetLegSegmentWorld(out Vector2 hip, out Vector2 tip, out float radius);

        Vector2 dir = tip - hip;
        float len = Mathf.Max(0.0001f, dir.magnitude);
        Vector2 mid = hip + 0.5f * dir;

        // 2) Posición del objeto (collider sigue la pierna)
        transform.position = new Vector3(mid.x, mid.y, transform.position.z);

        // 3) Rotación del objeto
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; // 0° = +X mundial
        float finalAngle = (spriteLongAxis == LongAxis.X) ? angle : angle - 90f;
        finalAngle += axisRotationOffsetDeg;
        transform.rotation = Quaternion.AngleAxis(finalAngle, Vector3.forward);

        // 4) Mantener “de pie” (flip visual, no rotación física)
        if (keepSpriteUpright)
        {
            float uprightBaseline = (spriteLongAxis == LongAxis.X) ? 0f : 90f;
            bool upsideDown = Mathf.Abs(Mathf.DeltaAngle(angle, uprightBaseline)) > 90f;

            if (spriteLongAxis == LongAxis.X)
            {
                sr.flipY = upsideDown;
                sr.flipX = false;
            }
            else
            {
                sr.flipX = upsideDown;
                sr.flipY = false;
            }
        }

        // 5) Tamaño visual (acortado) — NO tocar el collider
        float thickness = Mathf.Max(minThickness, radius * 2f);
        float visLen = len * Mathf.Clamp01(visualLengthFactor);
        float visThk = thickness * Mathf.Max(0.01f, visualThicknessFactor);

        if (useSpriteSizeApi && sr.drawMode != SpriteDrawMode.Simple)
        {
            // Modo Tiled/Sliced: usar sr.size (no afecta collider)
            if (spriteLongAxis == LongAxis.X)
                sr.size = new Vector2(visLen, visThk);
            else
                sr.size = new Vector2(visThk, visLen);
        }
        else
        {
            // Modo Simple: escalar SOLO el nodo visual (no el GameObject con el collider)
            // Importante: asegúrate de que 'visualRoot' sea un HIJO sin colliders.
            Vector2 spriteSize = sr.sprite ? (Vector2)sr.sprite.bounds.size : new Vector2(1, 1);
            float sx, sy;
            if (spriteLongAxis == LongAxis.X)
            {
                sx = visLen / Mathf.Max(0.0001f, spriteSize.x);
                sy = visThk / Mathf.Max(0.0001f, spriteSize.y);
            }
            else
            {
                sx = visThk / Mathf.Max(0.0001f, spriteSize.x);
                sy = visLen / Mathf.Max(0.0001f, spriteSize.y);
            }
            if (visualRoot) visualRoot.localScale = new Vector3(sx, sy, 1f);
        }

        // 6) Mostrar/ocultar
        if (hideWhenInactive)
            sr.enabled = player.enableKickLeg && (player.IsLegStriking() || !player.onGround);
    }
}
