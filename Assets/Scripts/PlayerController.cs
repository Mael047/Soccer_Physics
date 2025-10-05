using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{

    public float radio = 0.5f;
    public float m = 1f;
    public float e = 0.2f; // rebote en colisiones
    public float jumpImpulse = 6f;
    public float suelo = 0f;
    public float g = 9.8f;

    public float angulo = 0f;
    public float w = 0f; // velocidad angular
    public float amortiguamiento = 6f;
    public float angleSpring = 12f; // fuerza que endereza
    public float tambaleoFactor = 6f; // cuánto afecta el tambaleo al salto
    public float tambaleoExtra = 10f;

    public float friccionSuelo = 30f;

    public Vector2 posicion;
    public Vector2 velocidad;
    public bool onGround;
    public bool jumpRequest;

    public int playerId = 1; // 1 o 2, para usar distintas teclas

    SpriteRenderer spriteRenderer;
    Animator animator;
    float halfHeight;

    void Start()
    {
        posicion = transform.position;
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        if(spriteRenderer != null)
        {
            halfHeight = spriteRenderer.bounds.extents.y;
        }
        else
        {
            halfHeight = radio;
        }
    }

    void Update()
    {
        if(playerId == 1)
        {
            if(Input.GetKeyDown(KeyCode.W))
            {
                jumpRequest = true;
                Debug.Log("W presionada");
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                jumpRequest = true;
                Debug.Log("up presionada");
            }
        }
    }


    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        
        // Salto
        if (jumpRequest && onGround)
        {
            Vector2 dir = transform.up.normalized; // dirección del salto
            Debug.Log("Jump!");
            velocidad += dir * jumpImpulse;
            onGround = false;
            jumpRequest = false;

            float refuerzo = 0.5f * tampSafeSign(angulo, w);
            w += refuerzo * 6f;
        }
        else
        {
            jumpRequest = false; // en caso de que no se haga 
        }

        //Gravedad
        velocidad.y += -g * dt;

        if(!onGround)
        {
            velocidad.x += -velocidad.x * (1 * dt);
        }

        //Actualizar posición 
        posicion += velocidad * dt;

        float torque = -angulo * angleSpring;
        float accAngular = torque - w * amortiguamiento;
        w += accAngular * dt;
        angulo += w * dt;
        transform.rotation = Quaternion.Euler(0f, 0f, angulo);

        //Colision con el suelo 
        float minY = suelo + halfHeight;
        if (posicion.y < minY)
        {
            float impactoVel = velocidad.y;
            posicion.y = minY;
            if(impactoVel < 0f)
            {
                velocidad.y = -impactoVel * e;
            }

            if(!onGround)
            {
                onGround = true;
                float impacto = Mathf.Abs(impactoVel);
                float sign;
                if(Mathf.Abs(angulo) > 0.1f) sign = Mathf.Sign(angulo);
                else if (Mathf.Abs(w) > 0.1f) sign = Mathf.Sign(w);
                else sign = (Random.value < 0.5f) ? -1f : 1f;

                w += impacto * tambaleoFactor * sign;

                w *= Mathf.Max(0f, 1f - tambaleoExtra * dt);

                velocidad.x = Mathf.MoveTowards(velocidad.x,0f, friccionSuelo * 0.6f * dt);

            }        
        }

        if (onGround)
        {
            float restauracion = angleSpring * 1f;
            float accAngularSuelo = -angulo * restauracion - w * (amortiguamiento + 0.5f);
            w += accAngularSuelo * dt;
            angulo += w * dt;
            transform.rotation = Quaternion.Euler(0f, 0f, angulo);

            velocidad.x = Mathf.MoveTowards(velocidad.x, 0f, friccionSuelo * dt);

            if(Mathf.Abs(velocidad.x) < 0.1) velocidad.x = 0f;
        }


        //Actualizar transform
        transform.position = new Vector3(posicion.x, posicion.y, transform.position.z);
    }

    private float tampSafeSign(float anguloVal , float angularVel)
    {
        if(Mathf.Abs(anguloVal) > 0.1f) return Mathf.Sign(anguloVal);
        if (Mathf.Abs(angularVel) > 0.1f) return Mathf.Sign(angularVel);
        return (Random.value < 0.5f) ? -1f : 1f;
    }

    public void Impulso(Vector2 impulse)
    {
        velocidad += impulse / m;
    }



}
