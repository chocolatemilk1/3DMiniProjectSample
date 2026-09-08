using System;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Effects/Helicopter Rotor Wash")]
public sealed class HelicopterRotorWash : MonoBehaviour
{
    [Header("Wind (world units)")]
    [Range(0f, 1f)] public float throttle = 1f;
    [Min(0.1f)] public float rotorRadius = 7f;
    [Min(0.1f)] public float windDepth = 20f;
    [Min(0f)] public float outwardSpread = 1.2f;
    [Min(0f)] public float acceleration = 65f;
    [Min(0f)] public float maxWindSpeed = 28f;
    [Min(0f)] public float outwardForce = 55f;
    [Min(0f)] public float groundSpreadForce = 90f;
    [Range(0f, 1f)] public float swirl = 0.45f;

    [Header("Affected particles")]
    [Tooltip("Affect only Blizzard ExrernalForces snow, Fog and GroundSnow. Also disables generated dust.")]
    public bool blizzardOnly = true;
    [Tooltip("Drag the Blizzard instance from the Hierarchy here, not the Project prefab. If empty, find Blizzard ExrernalForces by name.")]
    public Transform blizzardRoot;
    [Tooltip("Discover active Particle Systems once per second. Disable to use only the list below.")]
    public bool autoFindParticles = true;
    public LayerMask particleLayers = ~0;
    public ParticleSystem[] affectedParticles = Array.Empty<ParticleSystem>();

    [Header("Ground dust")]
    public bool emitDust = false;
    [Tooltip("Include ground layers with colliders.")]
    public LayerMask groundLayers = ~0;
    [Tooltip("Ignore colliders under this transform. Defaults to the effect's parent.")]
    public Transform helicopterRoot;
    public Shader dustShader;
    public Color dustColor = new Color(0.65f, 0.6f, 0.5f, 0.25f);
    [Min(0f)] public float dustPerSecond = 65f;
    [Min(0.1f)] public float dustLifetime = 2f;

    private ParticleSystem[] discoveredParticles = Array.Empty<ParticleSystem>();
    private ParticleSystem.Particle[] buffer = Array.Empty<ParticleSystem.Particle>();
    private RaycastHit[] groundHits = new RaycastHit[16];
    private ParticleSystem dust;
    private Material dustMaterial;
    private float nextDiscovery;
    private float dustRemainder;
    private bool legacyTuningMigrated;

    private void OnEnable()
    {
        MigrateLegacySceneInstance();
        nextDiscovery = 0f;
        if (dust != null)
            dust.Play();
    }

    private void MigrateLegacySceneInstance()
    {
        if (legacyTuningMigrated || outwardForce > 0f)
            return;

        // Existing scene instances were created before the visible Blizzard tuning existed.
        rotorRadius = Mathf.Max(rotorRadius, 7f);
        windDepth = Mathf.Max(windDepth, 20f);
        outwardSpread = Mathf.Max(outwardSpread, 1.2f);
        acceleration = Mathf.Max(acceleration, 65f);
        maxWindSpeed = Mathf.Max(maxWindSpeed, 28f);
        outwardForce = 55f;
        groundSpreadForce = 90f;
        swirl = Mathf.Max(swirl, 0.45f);
        emitDust = false;

        Transform foundBlizzard = FindBlizzardRoot();
        if (foundBlizzard != null)
        {
            blizzardOnly = true;
            blizzardRoot = foundBlizzard;
        }
        legacyTuningMigrated = true;
    }

    private Transform FindBlizzardRoot()
    {
        Transform match = null;
        foreach (Transform candidate in FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene == gameObject.scene && IsBlizzardName(candidate.name))
            {
                match = candidate;
                break;
            }
        }
        return match;
    }

    private void LateUpdate()
    {
        if (throttle <= 0f || Time.deltaTime <= 0f)
            return;

        if (autoFindParticles && Time.unscaledTime >= nextDiscovery)
        {
            discoveredParticles = blizzardOnly && blizzardRoot != null
                ? blizzardRoot.GetComponentsInChildren<ParticleSystem>(true)
                : FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            nextDiscovery = Time.unscaledTime + 1f;
        }

        Vector3 origin = transform.position;
        Vector3 down = -transform.up;
        bool grounded = TryGetGround(origin, down, out RaycastHit ground);
        ParticleSystem[] targets = autoFindParticles ? discoveredParticles : affectedParticles;
        if (targets != null)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                // Inspector lists may contain duplicates; apply the wind only once.
                if (autoFindParticles || Array.IndexOf(targets, targets[i], 0, i) < 0)
                    ApplyWind(targets[i], origin, down, grounded, ground);
            }
        }

        if (!blizzardOnly && emitDust && grounded)
            EmitGroundDust(ground, down);
        else
            dustRemainder = 0f;
    }

    public static bool IsBlizzardName(string objectName)
    {
        return objectName.StartsWith("Blizzard ExrernalForces", StringComparison.OrdinalIgnoreCase)
            || objectName.StartsWith("Blizzard ExternalForces", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsTargetParticle(ParticleSystem system)
    {
        if (!blizzardOnly)
            return true;
        if (blizzardRoot != null)
            return system.transform.IsChildOf(blizzardRoot);
        for (Transform parent = system.transform; parent != null; parent = parent.parent)
        {
            if (IsBlizzardName(parent.name))
                return true;
        }
        return false;
    }

    private bool TryGetGround(Vector3 origin, Vector3 down, out RaycastHit ground)
    {
        int count;
        // Grow when saturated so helicopter colliders cannot hide a ground hit.
        while (true)
        {
            count = Physics.RaycastNonAlloc(origin, down, groundHits, windDepth,
                groundLayers, QueryTriggerInteraction.Ignore);
            if (count < groundHits.Length)
                break;
            Array.Resize(ref groundHits, groundHits.Length * 2);
        }
        Transform ignoredRoot = helicopterRoot != null ? helicopterRoot : transform.parent;
        ground = default;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.transform.IsChildOf(transform) ||
                (ignoredRoot != null && hit.transform.IsChildOf(ignoredRoot)))
                continue;
            if (hit.distance < nearest)
            {
                nearest = hit.distance;
                ground = hit;
            }
        }
        return nearest < float.PositiveInfinity;
    }

    private void ApplyWind(ParticleSystem system, Vector3 origin, Vector3 down,
        bool grounded, RaycastHit ground)
    {
        if (system == null || system == dust || !system.isPlaying ||
            (particleLayers.value & (1 << system.gameObject.layer)) == 0 ||
            !IsTargetParticle(system))
            return;
        int capacity = system.particleCount;
        if (capacity == 0)
            return;
        if (buffer.Length < capacity)
            Array.Resize(ref buffer, Mathf.NextPowerOfTwo(capacity));

        var main = system.main;
        Transform space = main.simulationSpace == ParticleSystemSimulationSpace.Local
            ? system.transform : main.simulationSpace == ParticleSystemSimulationSpace.Custom
                ? main.customSimulationSpace : null;
        Matrix4x4 toWorld = space != null ? space.localToWorldMatrix : Matrix4x4.identity;
        Matrix4x4 fromWorld = toWorld.inverse;
        int count = system.GetParticles(buffer);
        bool changed = false;
        float dt = (main.useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) * main.simulationSpeed;
        for (int i = 0; i < count; i++)
        {
            Vector3 position = toWorld.MultiplyPoint3x4(buffer[i].position);
            Vector3 offset = position - origin;
            float depth = Vector3.Dot(offset, down);
            if (depth < 0f || depth > windDepth)
                continue;
            float height = grounded ? Vector3.Dot(position - ground.point, ground.normal) : windDepth;
            if (grounded && height < -0.1f)
                continue;
            float radius = rotorRadius + depth * outwardSpread;
            Vector3 radial = offset - down * depth;
            float distance = radial.magnitude;
            if (distance >= radius)
                continue;

            Vector3 outward = distance > 0.001f ? radial / distance : transform.right;
            float groundBlend = grounded ? 1f - Mathf.Clamp01(height / 3f) : 0f;
            Vector3 direction = down + outward * outwardSpread;
            Vector3 spreadDirection = outward;
            if (grounded)
            {
                Vector3 alongGround = Vector3.ProjectOnPlane(outward, ground.normal).normalized;
                spreadDirection = alongGround.sqrMagnitude > 0.001f ? alongGround : outward;
                direction = Vector3.Lerp(direction, alongGround * 2f + ground.normal * 0.15f, groundBlend);
            }
            direction = (direction + Vector3.Cross(down, outward) * swirl).normalized;
            float falloff = (1f - distance / radius) * (1f - depth / windDepth);
            Vector3 velocity = toWorld.MultiplyVector(buffer[i].velocity);
            float availableSpeed = Mathf.Max(0f, maxWindSpeed * throttle - Vector3.Dot(velocity, direction));
            velocity += direction * Mathf.Min(acceleration * throttle * falloff * dt, availableSpeed);
            // Add a separate lateral kick so existing downward snow still visibly spreads.
            float spreadLimit = maxWindSpeed * (grounded ? 1.5f : 1f) * throttle;
            float currentSpreadSpeed = Vector3.Dot(velocity, spreadDirection);
            float availableSpread = Mathf.Max(0f, spreadLimit - currentSpreadSpeed);
            float spreadAcceleration = Mathf.Lerp(outwardForce, groundSpreadForce, groundBlend);
            velocity += spreadDirection * Mathf.Min(spreadAcceleration * throttle * falloff * dt, availableSpread);
            buffer[i].velocity = fromWorld.MultiplyVector(velocity);
            changed = true;
        }
        if (changed)
            system.SetParticles(buffer, count);
    }

    private void EmitGroundDust(RaycastHit ground, Vector3 down)
    {
        if (dust == null && !CreateDust())
            return;
        float proximity = 1f - Mathf.Clamp01(ground.distance / windDepth);
        dustRemainder += dustPerSecond * throttle * proximity * Time.deltaTime;
        int count = Mathf.FloorToInt(dustRemainder);
        dustRemainder -= count;
        Vector3 tangent = Vector3.Cross(ground.normal, transform.forward).normalized;
        if (tangent.sqrMagnitude < 0.01f)
            tangent = Vector3.Cross(ground.normal, transform.right).normalized;
        Vector3 bitangent = Vector3.Cross(ground.normal, tangent);
        for (int i = 0; i < count; i++)
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            Vector3 outward = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
            Vector3 position = ground.point + outward * rotorRadius * UnityEngine.Random.Range(0.2f, 1f);
            // Sample each spawn position to follow slopes and avoid emitting over ledges.
            if (!TryGetGround(position - down * 2f, down, out RaycastHit surface))
                continue;
            dust.Emit(new ParticleSystem.EmitParams
            {
                position = surface.point + surface.normal * 0.15f,
                velocity = (Vector3.ProjectOnPlane(outward, surface.normal).normalized * 0.65f
                    + surface.normal * 0.12f) * maxWindSpeed * throttle * proximity,
                startColor = dustColor,
                startSize = UnityEngine.Random.Range(0.6f, 1.4f),
                startLifetime = dustLifetime,
                rotation = UnityEngine.Random.Range(0f, 360f)
            }, 1);
        }
    }

    private bool CreateDust()
    {
        if (dustShader == null)
            return false;
        var child = new GameObject("Rotor Wash Dust");
        child.transform.SetParent(transform, false);
        dust = child.AddComponent<ParticleSystem>();
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = dust.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 512;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = dust.emission;
        emission.enabled = false;
        var shape = dust.shape;
        shape.enabled = false;
        var size = dust.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 2.5f));
        var color = dust.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;
        dustMaterial = new Material(dustShader);
        var renderer = dust.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = dustMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        dust.Play();
        return true;
    }

    private void OnDisable()
    {
        dustRemainder = 0f;
        if (dust != null)
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDestroy()
    {
        if (dust != null)
            Destroy(dust.gameObject);
        if (dustMaterial != null)
            Destroy(dustMaterial);
    }

    private void OnValidate()
    {
        nextDiscovery = 0f;
        rotorRadius = Mathf.Max(0.1f, rotorRadius);
        windDepth = Mathf.Max(0.1f, windDepth);
        throttle = Mathf.Clamp01(throttle);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
        Vector3 bottom = transform.position - transform.up * windDepth;
        float bottomRadius = rotorRadius + windDepth * outwardSpread;
        for (int i = 0; i < 32; i++)
        {
            float angle = i * Mathf.PI * 2f / 32f;
            float next = (i + 1) * Mathf.PI * 2f / 32f;
            Vector3 a = transform.right * Mathf.Cos(angle) + transform.forward * Mathf.Sin(angle);
            Vector3 b = transform.right * Mathf.Cos(next) + transform.forward * Mathf.Sin(next);
            Gizmos.DrawLine(transform.position + a * rotorRadius, transform.position + b * rotorRadius);
            Gizmos.DrawLine(bottom + a * bottomRadius, bottom + b * bottomRadius);
            if (i % 8 == 0)
                Gizmos.DrawLine(transform.position + a * rotorRadius, bottom + a * bottomRadius);
        }
    }
}
