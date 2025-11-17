using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class GoalLine : MonoBehaviour
{
    public enum Side { Left, Right }

    [Header("Config")]
    public Side side = Side.Left;
    [Tooltip("Quién recibe el punto cuando se marca en ESTA portería. 1=Player A, 2=Player B")]
    public int awardPlayerId = 2;

    [Header("Refs")]
    public Ball ball;            // asigna en Inspector
    public GameManager game;     // asigna en Inspector

    [Header("Altura del arco")]
    [Tooltip("Si es true, usa el alto del SpriteRenderer; si es false, usa los límites manuales.")]
    public bool useSpriteHeight = true;
    public float manualGoalBottomY = -0.5f;
    public float manualGoalTopY = 2.0f;

    // Internos
    SpriteRenderer sr;
    float prevSigned;    // distancia firmada al plano de la línea (con signo)
    bool primed;         // armado para detectar el siguiente cruce

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        ArmSensor();
    }

    void FixedUpdate()
    {
        if (ball == null || game == null) return;

        // Plano X de la “cara interna” de la línea
        Bounds b = sr.bounds;
        float planeX = (side == Side.Left) ? b.max.x : b.min.x;

        // Rango vertical válido del arco
        float yMin, yMax;
        if (useSpriteHeight)
        {
            yMin = b.min.y;
            yMax = b.max.y;
        }
        else
        {
            yMin = manualGoalBottomY;
            yMax = manualGoalTopY;
        }

        // Datos de la pelota (tu Ball.cs)
        float r = Mathf.Max(0.0001f, ball.radius);
        Vector2 p = ball.position;

        // ¿Está la pelota a la altura del arco?
        bool yInside = (p.y >= yMin - r) && (p.y <= yMax + r);
        if (!yInside)
        {
            // Mantén actualizado el estado pero no marques gol
            prevSigned = SignedDistanceToPlaneX(p.x, planeX, r);
            return;
        }

        // Distancia firmada actual (con radio): positiva “dentro de cancha”, negativa “afuera”
        float currSigned = SignedDistanceToPlaneX(p.x, planeX, r);

        // Detecta cruce si cruzó de + -> -
        if (primed && prevSigned >= 0f && currSigned < 0f)
        {
            Debug.Log($"[GoalLine] GOL detectado en {side}. awardPlayerId={awardPlayerId}");
            game.OnGoal(awardPlayerId);
            ArmSensor(); // evita dobles goles
            return;
        }

        prevSigned = currSigned;
        primed = true; // se arma al menos una vez dentro del juego
    }

    float SignedDistanceToPlaneX(float x, float planeX, float radius)
    {
        // Definimos “+” como lado de la cancha y “-” como fuera.
        // Para izquierda: plano mira hacia +x; para derecha: mira hacia -x.
        if (side == Side.Left)
            return (x - radius) - planeX;  // + si centro-r está a la derecha del plano
        else
            return planeX - (x + radius);  // + si centro+r está a la izquierda del plano
    }

    void ArmSensor()
    {
        if (sr == null || ball == null) return;
        Bounds b = sr.bounds;
        float planeX = (side == Side.Left) ? b.max.x : b.min.x;
        prevSigned = SignedDistanceToPlaneX(ball.position.x, planeX, Mathf.Max(0.0001f, ball.radius));
        primed = true;
    }

    // Llamado por GameManager después de cada gol
    public void ResetSensor() => ArmSensor();

    // Gizmos para depurar
    void OnDrawGizmos()
    {
        var sr2 = GetComponent<SpriteRenderer>();
        if (sr2 == null) return;
        Bounds b = sr2.bounds;
        float planeX = (side == Side.Left) ? b.max.x : b.min.x;

        float yMin = useSpriteHeight ? b.min.y : manualGoalBottomY;
        float yMax = useSpriteHeight ? b.max.y : manualGoalTopY;

        Gizmos.color = Color.red;
        Vector3 a = new Vector3(planeX, yMin, 0);
        Vector3 c = new Vector3(planeX, yMax, 0);
        Gizmos.DrawLine(a, c);

        // Marcas de altura
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(a, 0.03f);
        Gizmos.DrawSphere(c, 0.03f);
    }
}
