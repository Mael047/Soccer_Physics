using UnityEngine;

public class Ball : MonoBehaviour
{
    public float radius = 0.2f;
    public float masa = 1f;
    public float e = 0.7f;
    public Vector2 position;
    public Vector2 velocity;
    public float g = 9.8f;
    public float suelo = 0f;



    void Start()
    {
        position = transform.position;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        velocity.y += -g * dt;
        position += velocity * dt;

        float minY = suelo + radius;
        if (position.y < minY)
        {
            position.y = minY;
            if (velocity.y < 0f) velocity.y = -velocity.y * e;
        }

        transform.position = new Vector3(position.x, position.y, transform.position.z);
    }

}
