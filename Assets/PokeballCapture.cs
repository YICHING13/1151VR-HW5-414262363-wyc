
using System.Collections;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PokeballCapture : MonoBehaviour
{
    [Header("Objects")]
    public GameObject bunny;
    public Transform rightArm;
    public Transform rightForeArm;

    [Header("Throw Animation")]
    public Vector3 armAxis = new Vector3(0, 0, 1);
    public Vector3 foreArmAxis = new Vector3(0, 0, 1);

    public float windUpAngle = -70f;
    public float releaseAngle = 85f;
    public float elbowBendAngle = -35f;

    public float windUpTime = 0.6f;
    public float swingTime = 0.35f;
    public float returnTime = 0.35f;

    [Header("Pokeball Flight")]
    public float throwDuration = 1.8f;
    public float arcHeight = 2.5f;
    public float spinSpeed = 360f;

    [Header("Capture")]
    public float captureDuration = 0.7f;

    [Header("Sound Effect")]
    public AudioClip successSound;

    private AudioSource audioSource;

    private bool hasThrown = false;
    private bool animatingArm = false;

    private float armAngle = 0f;
    private float elbowAngle = 0f;

    // Keep the Pokeball's world size constant
    private Vector3 fixedBallScale;

    void Awake()
    {
        // Find or create AudioSource
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Do not play audio automatically
        audioSource.playOnAwake = false;

        // 2D audio so it can be heard clearly
        audioSource.spatialBlend = 0f;
    }

    void Update()
    {
        if (hasThrown) return;

        bool spacePressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            spacePressed =
                Keyboard.current.spaceKey.wasPressedThisFrame;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        spacePressed = Input.GetKeyDown(KeyCode.Space);
#endif

        if (spacePressed)
        {
            if (bunny == null || rightArm == null)
            {
                Debug.LogError(
                    "Please assign Bunny and RightArm!"
                );
                return;
            }

            hasThrown = true;
            StartCoroutine(ThrowSequence());
        }
    }

    // Apply the throw pose after Idle animation
    void LateUpdate()
    {
        if (!animatingArm) return;

        if (rightArm != null)
        {
            Vector3 axis =
                armAxis.sqrMagnitude > 0.001f
                ? armAxis.normalized
                : Vector3.forward;

            rightArm.localRotation *=
                Quaternion.AngleAxis(
                    armAngle,
                    axis
                );
        }

        if (rightForeArm != null)
        {
            Vector3 axis =
                foreArmAxis.sqrMagnitude > 0.001f
                ? foreArmAxis.normalized
                : Vector3.forward;

            rightForeArm.localRotation *=
                Quaternion.AngleAxis(
                    elbowAngle,
                    axis
                );
        }
    }

    IEnumerator AnimateArm(
        float fromArm,
        float toArm,
        float fromElbow,
        float toElbow,
        float duration)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / duration
            );

            t = Mathf.SmoothStep(0f, 1f, t);

            armAngle = Mathf.Lerp(
                fromArm,
                toArm,
                t
            );

            elbowAngle = Mathf.Lerp(
                fromElbow,
                toElbow,
                t
            );

            yield return null;
        }

        armAngle = toArm;
        elbowAngle = toElbow;
    }

    IEnumerator ThrowSequence()
    {
        animatingArm = true;

        // 1. Pull the arm backward
        yield return AnimateArm(
            0f,
            windUpAngle,
            0f,
            elbowBendAngle,
            windUpTime
        );

        // 2. Swing the arm forward
        yield return AnimateArm(
            windUpAngle,
            releaseAngle,
            elbowBendAngle,
            0f,
            swingTime
        );

        // 3. Remember the Pokeball's world size
        fixedBallScale = transform.lossyScale;

        // Detach from RightHand
        transform.SetParent(null, true);

        // Preserve the Pokeball's world size
        transform.localScale = fixedBallScale;

        Vector3 startPosition = transform.position;

        // Return the arm to Idle
        StartCoroutine(ReturnArm());

        // 4. Aim at Bunny
        Vector3 endPosition =
            bunny.transform.position + Vector3.up;

        Renderer targetRenderer =
            bunny.GetComponentInChildren<Renderer>();

        if (targetRenderer != null)
        {
            endPosition = targetRenderer.bounds.center;
        }

        // 5. Throw the Pokeball in an arc
        float elapsed = 0f;

        float duration = Mathf.Max(
            0.1f,
            throwDuration
        );

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / duration
            );

            Vector3 position = Vector3.Lerp(
                startPosition,
                endPosition,
                t
            );

            position.y +=
                Mathf.Sin(t * Mathf.PI) * arcHeight;

            transform.position = position;

            transform.Rotate(
                Vector3.forward,
                spinSpeed * Time.deltaTime,
                Space.Self
            );

            // Keep the Pokeball the same size
            transform.localScale = fixedBallScale;

            yield return null;
        }

        transform.position = endPosition;
        transform.localScale = fixedBallScale;

        // 6. Capture particle effect
        CreateCaptureEffect(endPosition);

        // 7. Shrink Bunny only
        Vector3 bunnyOriginalScale =
            bunny.transform.localScale;

        elapsed = 0f;

        duration = Mathf.Max(
            0.1f,
            captureDuration
        );

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(
                elapsed / duration
            );

            bunny.transform.localScale =
                Vector3.Lerp(
                    bunnyOriginalScale,
                    Vector3.zero,
                    t
                );

            // Pokeball stays the same size
            transform.localScale = fixedBallScale;

            yield return null;
        }

        // 8. Bunny disappears
        bunny.SetActive(false);

        // 9. Play success sound
        if (successSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(successSound);
        }

        Debug.Log("Capture Success!");
    }

    IEnumerator ReturnArm()
    {
        yield return AnimateArm(
            releaseAngle,
            0f,
            0f,
            0f,
            returnTime
        );

        animatingArm = false;
    }

    void CreateCaptureEffect(Vector3 position)
    {
        GameObject effect =
            new GameObject("Capture Sparkles");

        effect.transform.position = position;

        ParticleSystem particles =
            effect.AddComponent<ParticleSystem>();

        particles.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        var main = particles.main;

        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = 0.8f;
        main.startSpeed = 3f;
        main.startSize = 0.12f;

        main.startColor =
            new Color(1f, 0.85f, 0.2f);

        main.simulationSpace =
            ParticleSystemSimulationSpace.World;

        var emission = particles.emission;

        emission.rateOverTime = 0f;

        emission.SetBursts(
            new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 40)
            }
        );

        var shape = particles.shape;

        shape.shapeType =
            ParticleSystemShapeType.Sphere;

        shape.radius = 0.2f;

        // URP-compatible particle material
        Shader shader = Shader.Find(
            "Universal Render Pipeline/Particles/Unlit"
        );

        if (shader != null)
        {
            var particleRenderer =
                particles.GetComponent<ParticleSystemRenderer>();

            particleRenderer.material =
                new Material(shader);
        }

        particles.Play();

        Destroy(effect, 3f);
    }
}
