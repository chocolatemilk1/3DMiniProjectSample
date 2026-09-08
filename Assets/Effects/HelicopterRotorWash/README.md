# Helicopter Rotor Wash

## Apply

1. Select the helicopter root in the scene Hierarchy.
2. Choose `Tools > Helicopter > Connect Blizzard to Selected Helicopter`.
3. Move the new child to the main rotor hub. Its green Y axis should point upward.
4. Assign the scene's `Blizzard ExrernalForces` root to `Blizzard Root` if it was not found automatically.
5. Enter Play mode. The Blizzard's snow, Fog and GroundSnow below the rotor are pushed down and outward.

The menu also updates an already-added rotor wash, preserving its position and strength settings.
The default Blizzard-only mode emits no additional particles and ignores unrelated effects.
Older scene instances are upgraded at runtime when they still have the old zero-valued outward force,
so the stronger Blizzard behavior works without recreating the effect.
When `Blizzard Root` is empty, instances named `Blizzard ExrernalForces` (including clones) are found
automatically. Assign the root explicitly for renamed instances or to select one of several Blizzards.

The URP Blizzard prefab enables External Forces on its root snow system, but disables that module
on Fog and GroundSnow. This component changes the existing particles' velocities directly through
GetParticles/SetParticles, so all three respond without changing those modules or the imported prefab.
It does not create a Particle System Force Field or require a Wind Zone.

Alternatively, drag `HelicopterRotorWash.prefab` beneath your helicopter in the Hierarchy.
The effect does not fly the helicopter or animate the rotor. Attach it to the helicopter body,
not a rapidly rotating blade. Save the scene after positioning the effect.

## Tune

- `Throttle`: 0 stops new wind/dust, 1 is full power. A flight controller can set this at runtime.
- `Blizzard Only`: restricts the effect to the Blizzard hierarchy and prevents additional dust.
- `Blizzard Root`: the target instance from the Hierarchy, not the prefab asset from the Project window.
- `Rotor Radius`: radius near the rotor, in world metres (not diameter).
- `Wind Depth`: maximum reach below the rotor, in world metres.
- `Acceleration`, `Max Wind Speed`: particle push and terminal speed along the wind direction.
- `Outward Force`: extra sideways acceleration that makes the spread easy to see in existing snow.
- `Ground Spread Force`: stronger sideways acceleration near the ground.
- `Outward Spread`, `Swirl`: widening of the wind cone and rotational motion.
- `Ground Layers`: include terrain/floor layers with colliders. Triggers are ignored.
- `Helicopter Root`: colliders under this object are ignored by ground detection.
- `Dust Color`, `Emit Dust`: optional legacy dust, available only with `Blizzard Only` turned off.
- `Auto Find Particles`: discovers active Particle Systems every second. Disable and populate
  `Affected Particles` to affect only selected smoke/snow, especially in large scenes.
- `Particle Layers`: limits which particle objects receive wind.

The selected effect draws the wind cone as a cyan gizmo. A collider is not required to push existing
snow; a ground collider makes the wind turn outward near the surface. Optional legacy ground dust
fades with altitude and requires a collider within Wind Depth. Already emitted particles continue
moving when Throttle becomes zero; their existing velocity is not reset.
Disabling the component clears its own dust immediately. Local, World, and Custom particle simulation
spaces are supported; paused systems are skipped. This effect targets ordinary Unity Particle Systems,
not VFX Graph, rigidbodies, or vegetation. It does not perform per-particle wall occlusion tests.

## Check in Unity

- Put the Blizzard's snow and fog within the rotor's gizmo cone and toggle Throttle between 0 and 1.
- Confirm snow, Fog and GroundSnow are pushed, while an unrelated Particle System is unchanged.
- Confirm no Rotor Wash Dust child is created in Blizzard-only mode.
- Move the helicopter and confirm the affected area follows it.
- Move the rotor more than Wind Depth above the particles and confirm it stops adding force.
- Test World/Local/Custom simulation spaces, pause, and disable/re-enable the effect.

Particle manipulation uses Unity's GetParticles/SetParticles API:
https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ParticleSystem.GetParticles.html
