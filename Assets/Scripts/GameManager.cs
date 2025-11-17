using UnityEngine;
using System.Collections;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("Líneas de gol")]
    public GoalLine leftGoal;
    public GoalLine rightGoal;

    [Header("Entidades")]
    public Ball ball;
    public PlayerController playerA;
    public PlayerController playerB;

    [Header("Spawns (opcionales)")]
    public Transform spawnBall;
    public Transform spawnA;
    public Transform spawnB;

    [Header("Spawns Balon")]
    public Transform ballSpawnA;
    public Transform ballSpawnB;

    [Header("UI (TMP)")]
    public TMP_Text scoreText;
    public TMP_Text bannerText;

    [Header("Reglas")]
    public int maxGoals = 7;
    public float bannerTime = 1.1f;
    public float resetDelay = 0.4f;
    public float resetTime = 3f;

    [Header("Kickoff opcional")]
    public float kickoffImpulseX = 0f; // ej: 1.5f
    public float kickoffImpulseY = 0f; // ej: 0.8f

    [Header("Sonidos")]
    public AudioSource audioSource;
    public AudioClip goalSound;
    public AudioClip General;
    public AudioClip startMatch;

    public Menu Menu;
    int scoreA, scoreB;
    bool roundLock;

    // autospawn
    Vector3 initBall, initA, initB;
    bool hasInit;

    void Awake()
    {
        // enlazar backrefs por si no lo hiciste en el inspector
        if (leftGoal) leftGoal.game = this;
        if (rightGoal) rightGoal.game = this;
    }

    void Start()
    {
        // Captura posiciones iniciales si no hay spawns
        initBall = (spawnBall ? spawnBall.position : (ball ? ball.transform.position : Vector3.zero));
        initA = (spawnA ? spawnA.position : (playerA ? playerA.transform.position : Vector3.zero));
        initB = (spawnB ? spawnB.position : (playerB ? playerB.transform.position : Vector3.zero));
        hasInit = (ball && playerA && playerB);

        UpdateScoreUI();
        HideBanner();

        if(audioSource && General)
        {
            audioSource.PlayOneShot(General);
        }
    }

    public void OnGoal(int scorerId)
    {
        if (roundLock) return;
        roundLock = true;

        if (scorerId == 1) scoreA++; else scoreB++;
        Debug.Log($"[GameManager] GOL de Player {scorerId}  -> marcador: P1 {scoreA} - {scoreB} P2");

        ShowBanner($"¡Gol de Player {scorerId}!");
        UpdateScoreUI();

        if (audioSource && goalSound)
        {
            audioSource.PlayOneShot(goalSound);
        }

        if (scoreA >= maxGoals || scoreB >= maxGoals)
            StartCoroutine(EndMatch());
        else
            StartCoroutine(ResetRound());
    }

    IEnumerator ResetRound()
    {
        // deja ver el banner
        yield return new WaitForSeconds(bannerTime);

        DoHardResetPositions();

        // re-armar sensores de gol
        if (leftGoal) leftGoal.ResetSensor();
        if (rightGoal) rightGoal.ResetSensor();

        if(audioSource && startMatch)
        {
            audioSource.PlayOneShot(startMatch);
        }

        yield return new WaitForSeconds(resetDelay);
        HideBanner();
        roundLock = false;
    }

    IEnumerator EndMatch()
    {
        string winner = (scoreA > scoreB) ? "Player 1" : "Player 2";
        ShowBanner($"Partido terminado. ¡Gana {winner}!");
        yield return new WaitForSeconds(resetTime);
        Menu.Volver();
        Debug.Log("[GameManager] Fin de partido. Usa R para reiniciar.");
    }

    Vector3 GetBallSpawnPosition()
    {
        if (ballSpawnA != null && ballSpawnB != null)
        {
            Transform chosen = (Random.value < 0.5f) ? ballSpawnA : ballSpawnB;
            Vector3 p = chosen.position;

            if (ball != null)
            {
                p.z = ball.transform.position.z;
                return p;
            }
        }
        return spawnBall ? spawnBall.position : initBall;
    }

    void DoHardResetPositions()
    {
        if (!hasInit)
        {
            Debug.LogWarning("[GameManager] No se capturaron inicios (faltan refs). Se usa origen (0,0,0). Asigna ball/playerA/playerB en el inspector.");
        }

        Vector3 pBall = GetBallSpawnPosition();
        Vector3 pA = spawnA ? spawnA.position : initA;
        Vector3 pB = spawnB ? spawnB.position : initB;

        // ---- Ball: pisa transform y variables internas ----
        if (ball)
        {
            ball.velocity = Vector2.zero;
            ball.position = new Vector2(pBall.x, pBall.y);
            ball.transform.position = pBall;

            // kickoff opcional
            if (kickoffImpulseX != 0f || kickoffImpulseY != 0f)
                ball.velocity = new Vector2(kickoffImpulseX, kickoffImpulseY);
        }

        // ---- Players: pisa transform y variables internas ----
        if (playerA) HardResetPlayer(playerA, pA);
        if (playerB) HardResetPlayer(playerB, pB);
    }

    void HardResetPlayer(PlayerController p, Vector3 pos)
    {
        p.velocidad = Vector2.zero;
        p.posicion = new Vector2(pos.x, pos.y);
        p.onGround = true;
        p.jumpRequest = false;

        p.transform.position = pos;
        p.transform.rotation = Quaternion.identity;
    }

    void UpdateScoreUI()
    {
        if (scoreText) scoreText.text = $"{scoreA} - {scoreB}";
    }

    void ShowBanner(string msg)
    {
        if (bannerText) { bannerText.gameObject.SetActive(true); bannerText.text = msg; }
    }

    void HideBanner()
    {
        if (bannerText) bannerText.gameObject.SetActive(false);
    }

    // R para resetear la ronda
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R) && !roundLock)
        {
            Debug.Log("[GameManager] Reset manual (R).");
            StartCoroutine(ResetRound());
        }
    }
}
