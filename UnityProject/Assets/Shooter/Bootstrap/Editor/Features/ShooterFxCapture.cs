using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Shooter.Contracts;
using Shooter.Generation;
using Shooter.Generation.Environment;
using Shooter.Presentation.Fx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

/// <summary>
/// ADR-126 editor review diagnostic: the real <see cref="AnalyticFxPresenter"/> bound to a training-compound visual
/// package draws a grenade blast over time, a rifle shot, bullet impacts on concrete, dirt and metal, and a burning
/// truck, at Low and High. Stand-in floor, sky and truck; not the packaged-player look, and never visual acceptance.
/// </summary>
public static class ShooterFxCapture
{
    [Serializable] public sealed class Proof
    {
        public string result = "Verified", scope = "ADR-126 effect editor diagnostic; not packaged-player look or visual acceptance";
        public string[] captures; public int ambientFires; public string[] diagnostics;
    }

    private const float Step = 1f / 60f;

    public static void Capture() => Guard(() =>
    {
        ShooterBuild.CheckToolchain();
        string output = Path.GetFullPath(ShooterBuild.Argument("-shooterCaptureRoot"));
        if (!output.StartsWith(Path.Combine(ShooterBuild.RepositoryRoot, "Artifacts") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Output must stay under repository Artifacts.");
        Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var owned = new List<UnityEngine.Object>();
        var captures = new List<string>(); var diagnostics = new List<string>();
        var proof = new Proof();
        try
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("Required URP Lit unavailable.");
            Material Lit(Color color) { var m = new Material(lit); m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", .1f); owned.Add(m); return m; }
            var camera = new GameObject("FX capture camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.55f, .62f, .70f); camera.fieldOfView = 60f; camera.nearClipPlane = .05f;
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.5f; sun.color = new Color(1f, .96f, .9f);
            sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.52f, .56f, .62f);
            RenderSettings.ambientEquatorColor = new Color(.36f, .36f, .36f); RenderSettings.ambientGroundColor = new Color(.20f, .18f, .16f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.transform.localScale = new Vector3(8, 1, 8); floor.GetComponent<MeshRenderer>().sharedMaterial = Lit(new Color(.42f, .40f, .37f));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = new Vector3(0, 1.5f, 6f); wall.transform.localScale = new Vector3(14f, 3f, .4f); wall.GetComponent<MeshRenderer>().sharedMaterial = Lit(new Color(.5f, .48f, .45f));
            var truck = GameObject.CreatePrimitive(PrimitiveType.Cube); truck.transform.position = new Vector3(30f, 1.5f, 0f); truck.transform.localScale = new Vector3(7f, 3f, 2.5f); truck.GetComponent<MeshRenderer>().sharedMaterial = Lit(new Color(.24f, .26f, .2f));

            var hash = ContentIdentity.Hash(Encoding.ASCII.GetBytes("fx-capture"));
            var visuals = PackagedVisualPayloads.FromBuild(MapPackageBuilder.Build("training-compound", "low", hash));
            var holder = new GameObject("FX capture root");
            var pool = new AnalyticFxPool();
            var presenter = AnalyticFxPresenter.Create(holder.transform, pool);
            presenter.BindVerifiedVisuals(visuals);
            var site = new AmbientFireSite(new Float3(30f, 1.5f, 0f), new Float3(7f, 3f, 2.5f), 1f);
            presenter.BindAmbientFires("fx-capture", new[] { site });
            proof.ambientFires = presenter.AmbientFireCount;
            if (proof.ambientFires != 1) throw new InvalidOperationException("Burning truck was not built.");

            foreach (var (level, label) in new[] { (0, "low"), (2, "high") })
            {
                QualitySettings.SetQualityLevel(level, true);
                void Shot(string name, Vector3 eye, Vector3 target)
                {
                    camera.transform.position = eye; camera.transform.LookAt(target);
                    string path = Path.Combine(output, $"{label}-{name}.png");
                    int alive = 0, particles = 0; string sample = "";
                    foreach (var ps in holder.GetComponentsInChildren<ParticleSystem>(false))
                    {
                        if (ps.main.loop || ps.particleCount == 0) continue;
                        alive++; particles += ps.particleCount;
                        if (sample.Length == 0) { var r = ps.GetComponent<ParticleSystemRenderer>(); sample = $" first={ps.name} playing={ps.isPlaying} paused={ps.isPaused} stopped={ps.isStopped} bounds={r.bounds} visible={r.isVisible} mat={r.sharedMaterial?.name}"; }
                    }
                    diagnostics.Add($"{label}-{name}: systems={alive} particles={particles}{sample}");
                    ShooterFeatureBuild.Render(camera, path); captures.Add(path);
                }
                void Run(float seconds)
                {
                    for (float t = 0f; t < seconds - 1e-4f; t += Step) { pool.Tick(Step); presenter.Sync(); }
                }

                // Grenade blast seen from 11 m, across the fireball's life and the lingering column.
                pool.Clear(); presenter.Sync();
                pool.SpawnGrenadeBlast(new Float3(0, 0, 0), new Float3(0, 1, 0), false, level); presenter.Sync();
                float elapsed = 0f;
                foreach (float at in new[] { .1f, .35f, .8f, 1.5f, 3.5f })
                {
                    Run(at - elapsed); elapsed = at;
                    Shot($"grenade-{(int)(at * 1000):D4}ms", new Vector3(-6f, 2.2f, -9f), new Vector3(0, 1.4f, 0));
                }

                // Rifle shot: fire ball under the star, tracer and barrel smoke, seen from the side.
                pool.Clear(); presenter.Sync();
                var muzzle = new Float3(0f, 1.4f, 0f);
                pool.SpawnGunshotFeedback(muzzle, new Float3(1f, 0f, 0f), false, 110, AnalyticFxPool.MuzzleSocketPushMeters);
                pool.TrySpawn(FxKind.SmokeProxy, muzzle, new Float3(1f, 0f, 0f)); presenter.Sync();
                Run(.03f); Shot("gunshot-0030ms", new Vector3(.4f, 1.55f, -1.6f), new Vector3(.3f, 1.4f, 0f));
                Run(.4f); Shot("gunshot-0430ms", new Vector3(.4f, 1.55f, -1.6f), new Vector3(.3f, 1.4f, 0f));

                // Bullet impacts on the wall: concrete, dirt (ground), metal.
                pool.Clear(); presenter.Sync();
                var outward = new Float3(0f, 0f, -1f);
                ImpactSurfaceFx.Spawn(pool, ImpactSurface.Concrete, new Float3(-2.5f, 1.4f, 5.75f), outward);
                ImpactSurfaceFx.Spawn(pool, ImpactSurface.Dirt, new Float3(0f, 0f, 4f), new Float3(0f, 1f, 0f));
                ImpactSurfaceFx.Spawn(pool, ImpactSurface.Metal, new Float3(2.5f, 1.4f, 5.75f), outward);
                presenter.Sync();
                Run(.12f); Shot("impacts-0120ms", new Vector3(0f, 1.6f, 1.2f), new Vector3(0f, 1.1f, 5.5f));
                Run(.3f); Shot("impacts-0420ms", new Vector3(0f, 1.6f, 1.2f), new Vector3(0f, 1.1f, 5.5f));

                // Burning truck: looping systems are simulated directly (the editor does not tick them).
                pool.Clear(); presenter.Sync();
                foreach (var ps in holder.GetComponentsInChildren<ParticleSystem>(false))
                    if (ps.main.loop) ps.Simulate(4f, true, true, false);
                Shot("burning-truck", new Vector3(18f, 3f, -12f), new Vector3(30f, 5f, 0f));
            }
            UnityEngine.Object.DestroyImmediate(holder);
            proof.captures = captures.ToArray(); proof.diagnostics = diagnostics.ToArray();
            ShooterBuild.WriteJson(ShooterBuild.Argument("-shooterReport"), proof);
            Debug.Log("SHOOTER_FX_CAPTURE files=" + captures.Count);
        }
        finally { foreach (var value in owned) if (value != null) UnityEngine.Object.DestroyImmediate(value); }
    });

    private static void Guard(Action body) { try { body(); } catch (Exception error) { Debug.LogError("SHOOTER_FAILURE: " + error); EditorApplication.Exit(1); } }
}
