using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenSourceTeam6.Time.Unity
{
    /// <summary>Owns only the native rain/splash systems and their runtime materials.</summary>
    internal sealed class RainParticleEffect : IDisposable
    {
        public int VisibleDropCount => drops ? drops.particleCount : 0;
        public int VisibleSplashCount => splashes ? splashes.particleCount : 0;

        private ParticleSystem drops, splashes;
        private Material rainMaterial, splashMaterial;
        private bool particlesPaused;
        private float appliedRate = -1, appliedNight = -1;
        private static readonly int TintId = Shader.PropertyToID("_Tint");

        private RainParticleEffect() { }

        public static bool TryCreate(Transform parent, LayerMask collisionLayers,
            float splashProbability, out RainParticleEffect effect)
        {
            effect = null;
            var shader = Resources.Load<Shader>("TimeCelestials/RainStreak");
            var splashShader = Resources.Load<Shader>("TimeCelestials/RainSplash");
            if (!parent || !shader || !splashShader) return false;

            var candidate = new RainParticleEffect();
            try
            {
                candidate.Create(parent, shader, splashShader, collisionLayers, splashProbability);
                effect = candidate;
                return true;
            }
            catch
            {
                candidate.Dispose();
                throw;
            }
        }

        private void Create(Transform parent, Shader shader, Shader splashShader,
            LayerMask collisionLayers, float splashProbability)
        {
            rainMaterial = new Material(shader) { name = "Rain streaks (runtime)" };
            splashMaterial = new Material(splashShader) { name = "Rain splashes (runtime)" };
            var emitter = new GameObject("Rain drops (runtime)");
            emitter.transform.SetParent(parent, false);
            drops = emitter.AddComponent<ParticleSystem>();
            drops.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = drops.main;
            main.playOnAwake = false;
            main.loop = true;
            main.prewarm = false;
            main.duration = 5;
            main.maxParticles = 2400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startLifetime = 1.2f;
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(.018f, .035f);
            main.startColor = Color.white;
            var shape = drops.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(20, .5f, 20);
            var velocity = drops.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = 1.3f;
            velocity.y = -18;
            velocity.z = .4f;
            var emission = drops.emission;
            emission.rateOverTime = 0;
            var collision = drops.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.enableDynamicColliders = false;
            collision.maxCollisionShapes = 128;
            collision.voxelSize = 1;
            collision.collidesWith = collisionLayers;
            collision.sendCollisionMessages = false;
            collision.bounce = 0;
            collision.lifetimeLoss = 1;
            var renderer = drops.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = rainMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = .025f;
            renderer.lengthScale = 1;
            renderer.cameraVelocityScale = 0;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            drops.useAutoRandomSeed = false;
            drops.randomSeed = 617;
            CreateSplashes();
            var sub = drops.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(splashes, ParticleSystemSubEmitterType.Collision,
                ParticleSystemSubEmitterProperties.InheritNothing, Mathf.Clamp01(splashProbability));
            // Native playback schedules jobs; only collisions start splash bursts.
            drops.Play(false);
        }

        private void CreateSplashes()
        {
            var emitter = new GameObject("Rain splashes (runtime)");
            emitter.transform.SetParent(drops.transform, false);
            splashes = emitter.AddComponent<ParticleSystem>();
            splashes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = splashes.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = .35f;
            main.maxParticles = 768;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .28f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.55f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.035f, .085f);
            main.gravityModifier = .8f;
            var shape = splashes.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 70;
            shape.radius = .025f;
            shape.rotation = new Vector3(-90, 0, 0);
            var emission = splashes.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 2) });
            var fade = splashes.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(.75f, 0), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var renderer = splashes.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = splashMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            splashes.useAutoRandomSeed = false;
            splashes.randomSeed = 618;
        }

        public void Apply(float emissionRate, bool paused, float night, Vector3 cameraPosition)
        {
            if (!drops || !splashes) return;
            drops.transform.position = cameraPosition + Vector3.up * 12;
            drops.transform.rotation = Quaternion.identity;
            if (paused != particlesPaused)
            {
                var main = drops.main; main.simulationSpeed = paused ? 0 : 1;
                var splashMain = splashes.main; splashMain.simulationSpeed = paused ? 0 : 1;
                particlesPaused = paused;
            }
            if (!Mathf.Approximately(emissionRate, appliedRate))
            {
                var emission = drops.emission; emission.rateOverTime = emissionRate;
                appliedRate = emissionRate;
            }
            if (!Mathf.Approximately(night, appliedNight))
            {
                Color tint = Color.Lerp(new Color(.65f, .73f, .82f, .48f), new Color(.15f, .20f, .30f, .38f), night);
                rainMaterial.SetColor(TintId, tint);
                splashMaterial.SetColor(TintId, tint);
                appliedNight = night;
            }
        }

        public void Dispose()
        {
            if (drops)
            {
                drops.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                UnityEngine.Object.Destroy(drops.gameObject);
            }
            if (rainMaterial) UnityEngine.Object.Destroy(rainMaterial);
            if (splashMaterial) UnityEngine.Object.Destroy(splashMaterial);
            drops = splashes = null;
            rainMaterial = splashMaterial = null;
        }
    }
}
