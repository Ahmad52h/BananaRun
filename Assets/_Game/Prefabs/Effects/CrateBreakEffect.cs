using UnityEngine;

public class CrateBreakEffect : MonoBehaviour
{
    [SerializeField, Min(0.1f)]
    private float lifetime = 0.8f;

    private Transform[] pieces;
    private Vector3[] velocities;
    private Vector3[] spins;
    private Vector3[] initialScales;

    private float age;

    private void Start()
    {
        int count = transform.childCount;

        pieces = new Transform[count];
        velocities = new Vector3[count];
        spins = new Vector3[count];
        initialScales = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            Transform piece = transform.GetChild(i);

            pieces[i] = piece;
            initialScales[i] = piece.localScale;

            Vector3 outward = piece.position - transform.position;
            outward.y = 0f;

            if (outward.sqrMagnitude < 0.001f)
                outward = Vector3.right;

            velocities[i] =
                outward.normalized * Random.Range(1.2f, 2.2f)
                + Vector3.up * Random.Range(2f, 3f);

            spins[i] = Random.insideUnitSphere * 400f;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float shrink = 1f - Mathf.InverseLerp(
            lifetime * 0.6f, lifetime, age);

        for (int i = 0; i < pieces.Length; i++)
        {
            velocities[i] += Vector3.down * (10f * dt);

            pieces[i].position += velocities[i] * dt;

            pieces[i].Rotate(
                spins[i] * dt,
                Space.World);

            pieces[i].localScale = initialScales[i] * shrink;
        }
    }
}