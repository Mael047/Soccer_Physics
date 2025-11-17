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

    public float spin = 1.2f;
    float spinAngle;

    void Start()
    {
        position = transform.position;
        spinAngle = transform.eulerAngles.z;
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

        float angleSpeed = -velocity.x / Mathf.Max(0.0001f, radius)* Mathf.Rad2Deg * spin;
        spinAngle += angleSpeed * dt;
        transform.rotation = Quaternion.Euler(0f, 0f, spinAngle);
    }

}
