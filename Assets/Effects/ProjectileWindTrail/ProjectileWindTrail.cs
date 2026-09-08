using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
[AddComponentMenu("Effects/Projectile Wind Trail")]
public sealed class ProjectileWindTrail : MonoBehaviour
{
    private static Material defaultTrailMaterial;

    public Material trailMaterial;
    public Color tint = new Color(0.85f, 0.95f, 1f, 0.4f);
    [Min(0.01f)] public float duration = 0.18f;
    [Min(0.001f)] public float width = 0.12f;
    [Min(0f)] public float spread = 0.18f;
    [Min(0f)] public float curlSpeed = 5f;
    [Range(0f, 1f)] public float irregularity = 0.35f;
    [Min(0f)] public float minimumSpeed = 1f;
    [Tooltip("Movement farther than this in a frame clears the trail instead of drawing a teleport streak.")]
    [Min(0.01f)] public float teleportDistance = 100f;

    private TrailRenderer[] trails;
    private Vector3 previousPosition;
    private float[] strandPhase;
    private float[] strandRadius;
    private float[] strandSpeed;

    public static ProjectileWindTrail AttachTo(GameObject target)
    {
        if (target == null)
            return null;

        var trail = target.GetComponent<ProjectileWindTrail>();
        return trail != null ? trail : target.AddComponent<ProjectileWindTrail>();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;
        previousPosition = transform.position;
        if (trailMaterial == null)
            trailMaterial = GetDefaultTrailMaterial();
        if (trailMaterial == null)
            return;

        trails = new TrailRenderer[3];
        strandPhase = new float[trails.Length];
        strandRadius = new float[trails.Length];
        strandSpeed = new float[trails.Length];
        for (int i = 0; i < trails.Length; i++)
        {
            var child = new GameObject("Wind Streak " + (i + 1));
            // Keep streaks as world-space roots so their fade remains after the projectile is destroyed.
            child.transform.position = transform.position;
            var currentTrail = child.AddComponent<TrailRenderer>();
            trails[i] = currentTrail;
            currentTrail.sharedMaterial = trailMaterial;
            currentTrail.emitting = false;
            currentTrail.time = duration;
            currentTrail.minVertexDistance = 0.02f;
            currentTrail.alignment = LineAlignment.View;
            currentTrail.textureMode = LineTextureMode.Stretch;
            currentTrail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.4f),
                new Keyframe(0.15f, 1f), new Keyframe(1f, 0f));
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(tint, 0f), new GradientColorKey(tint, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(tint.a, 0.12f),
                    new GradientAlphaKey(0f, 1f) });
            currentTrail.colorGradient = gradient;
            currentTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            currentTrail.receiveShadows = false;
            currentTrail.widthMultiplier = width * (i == 0 ? 1f : 0.55f);

            // Keep each strand's variation stable so the vortex feels organic instead of noisy.
            float seed = Mathf.Sin((GetInstanceID() + 17f) * (i + 1f)) * 0.5f + 0.5f;
            strandPhase[i] = seed * Mathf.PI * 2f;
            strandRadius[i] = 1f + (seed - 0.5f) * 0.8f * irregularity;
            strandSpeed[i] = 1f + (0.5f - seed) * 0.35f * irregularity;
        }
    }

    private static Material GetDefaultTrailMaterial()
    {
        if (defaultTrailMaterial != null)
            return defaultTrailMaterial;

        Shader shader = Shader.Find("MiniProject/Projectile Wind Trail");
        if (shader == null)
        {
            Debug.LogWarning("Projectile Wind Trail shader could not be found.");
            return null;
        }

        defaultTrailMaterial = new Material(shader)
        {
            name = "Projectile Wind Trail (Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
        return defaultTrailMaterial;
    }

    public void ClearTrail()
    {
        previousPosition = transform.position;
        if (trails == null)
            return;
        foreach (var currentTrail in trails)
            if (currentTrail != null)
                currentTrail.Clear();
    }

    private void LateUpdate()
    {
        if (trails == null || Time.deltaTime <= 0f)
            return;
        Vector3 currentPosition = transform.position;
        Vector3 movement = currentPosition - previousPosition;
        previousPosition = currentPosition;
        if (movement.magnitude > teleportDistance)
        {
            ClearTrail();
            return;
        }

        bool moving = movement.magnitude / Time.deltaTime >= minimumSpeed;
        Vector3 forward = moving ? movement.normalized : transform.forward;
        Vector3 axis = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) < 0.9f ? Vector3.up : Vector3.right;
        Vector3 right = Vector3.Cross(forward, axis).normalized;
        Vector3 up = Vector3.Cross(right, forward);
        for (int i = 0; i < trails.Length; i++)
        {
            float angle = Time.time * curlSpeed * strandSpeed[i] + i * Mathf.PI + strandPhase[i];
            float wobble = 1f + Mathf.Sin(Time.time * curlSpeed * 0.45f + strandPhase[i] * 1.7f)
                * 0.35f * irregularity;
            Vector3 offset = i == 0 ? Vector3.zero
                : (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * spread * strandRadius[i] * wobble;
            trails[i].transform.position = currentPosition + offset;
            trails[i].time = duration;
            trails[i].widthMultiplier = width * (i == 0 ? 1f : 0.55f);
            trails[i].emitting = moving;
        }
    }

    private void OnDisable()
    {
        if (trails == null)
            return;
        foreach (var currentTrail in trails)
        {
            if (currentTrail == null)
                continue;
            currentTrail.emitting = false;
            // The streak objects are already unparented; avoid changing parents during deactivation.
            Destroy(currentTrail.gameObject, Mathf.Max(0.01f, currentTrail.time));
        }
        trails = null;
    }
}
