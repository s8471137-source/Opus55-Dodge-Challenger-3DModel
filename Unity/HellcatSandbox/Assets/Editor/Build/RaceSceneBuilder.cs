using System.IO;
using Hellcat.EditorTools.Car;
using Hellcat.Game.Core;
using Hellcat.Game.Racing;
using Hellcat.Game.Views;
using Hellcat.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hellcat.EditorTools.Build
{
    public static class RaceSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/CrashCircuit.unity";
        const string Folder = "Assets/Game/Racing/Generated";

        static Material Material(string name, Color colour)
        {
            string path = Folder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = colour;
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Box(string name, Vector3 p, Vector3 scale, Quaternion rotation, Material material, bool collides)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetPositionAndRotation(p, rotation); go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (collides) go.AddComponent<StaticBoxSource>();
            return go;
        }

        static Vector3 Point(float angle) => new Vector3(155f * Mathf.Cos(angle), 0f, 100f * Mathf.Sin(angle));
        static Vector3 Tangent(float angle) => new Vector3(-155f * Mathf.Sin(angle), 0f, 100f * Mathf.Cos(angle)).normalized;

        [MenuItem("Tools/Hellcat/Build Two Player Race Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var asphalt = Material("RaceAsphalt", new Color(0.12f, 0.14f, 0.16f));
            var grass = Material("RaceGrass", new Color(0.16f, 0.3f, 0.12f));
            var red = Material("RaceRed", new Color(0.8f, 0.12f, 0.08f));
            var white = Material("RaceWhite", new Color(0.92f, 0.92f, 0.86f));
            var yellow = Material("RaceYellow", new Color(1f, 0.65f, 0.05f));
            Box("Grass infield", new Vector3(0, -0.2f, 0), new Vector3(420, 0.3f, 310), Quaternion.identity, grass, false);
            new GameObject("Ground collision").AddComponent<StaticPlaneSource>();
            const int segments = 64;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments, b = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 p = Point(a), q = Point(b), d = (q - p).normalized;
                Quaternion rotation = Quaternion.LookRotation(d);
                float length = Vector3.Distance(p, q);
                Vector3 middle = (p + q) / 2f, right = Vector3.Cross(Vector3.up, d);
                Box("Asphalt " + i, middle + Vector3.down * 0.06f, new Vector3(22, 0.1f, length + 0.4f), rotation, asphalt, false);
                foreach (int side in new[] { -1, 1 })
                {
                    Box("Barrier " + i + " " + side, middle + right * (side * 11.5f) + Vector3.up * 0.55f,
                        new Vector3(0.6f, 1.1f, length + 0.65f), rotation, i % 2 == 0 ? red : white, true);
                    Box("Edge " + i + " " + side, middle + right * (side * 10.4f) + Vector3.up * 0.015f,
                        new Vector3(0.5f, 0.03f, length + 0.4f), rotation, white, false);
                }
                if (i % 2 == 0) Box("Direction dash " + i, middle + Vector3.up * 0.02f,
                    new Vector3(0.18f, 0.03f, 3), rotation, yellow, false);
            }
            // Half-width ramps preserve a bypass lane. Convex prism collision uses the same mesh profile.
            foreach (int index in new[] { 9, 41 })
            {
                float a = index * Mathf.PI * 2f / segments;
                Vector3 forward = Tangent(a), right = Vector3.Cross(Vector3.up, forward);
                var go = new GameObject("Jump ramp " + index, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.position = Point(a) - right * 4f;
                go.transform.rotation = Quaternion.LookRotation(-right);
                var prism = go.AddComponent<StaticPrismSource>();
                prism.profile = new[] { new Vector2(-4, 0), new Vector2(4, 0), new Vector2(4, 0.8f) };
                prism.length = 8f; prism.BuildMesh();
                string path = Folder + "/Ramp" + index + ".asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(go.GetComponent<MeshFilter>().sharedMesh, path);
                go.GetComponent<MeshRenderer>().sharedMaterial = yellow;
            }
            foreach (int index in new[] { 19, 22, 25, 51, 54 })
            {
                float a = index * Mathf.PI * 2f / segments;
                Vector3 forward = Tangent(a), right = Vector3.Cross(Vector3.up, forward);
                Box("Slalom obstacle " + index, Point(a) + right * (index % 2 == 0 ? 6f : -6f) + Vector3.up * 0.6f,
                    new Vector3(6, 1.2f, 1.2f), Quaternion.LookRotation(forward), red, true);
            }
            // Visible ordered checkpoint arches; overhead beams are decorative, legs collide.
            var gates = new Vector3[16];
            for (int i = 0; i < gates.Length; i++)
            {
                float a = i * Mathf.PI * 2f / gates.Length;
                gates[i] = Point(a);
                Quaternion rotation = Quaternion.LookRotation(Tangent(a));
                Vector3 right = rotation * Vector3.right;
                foreach (int side in new[] { -1, 1 })
                    Box("Gate " + i + " post " + side, gates[i] + right * (side * 10.8f) + Vector3.up * 3,
                        new Vector3(0.25f, 6, 0.25f), rotation, i == 0 ? white : yellow, true);
                Box("Gate " + i + " header", gates[i] + Vector3.up * 6,
                    new Vector3(21.8f, 0.35f, 0.4f), rotation, i == 0 ? white : yellow, false);
            }
            Box("Start finish line", gates[0] + Vector3.up * 0.03f, new Vector3(21, 0.03f, 0.8f),
                Quaternion.LookRotation(Tangent(0)), white, false);
            var spawn = new GameObject("Race grid").transform;
            spawn.SetPositionAndRotation(gates[0] - Tangent(0) * 8f - Vector3.right * 2.5f + Vector3.up,
                Quaternion.LookRotation(Tangent(0)));
            // At angle zero the grid's local right is +X; second car is offset by five metres.
            var sim = new GameObject("Race simulation");
            var runner = sim.AddComponent<SimRunner>();
            runner.vehicleName = "hellcat"; runner.spawnPoint = spawn;
            runner.spawnClearance = 0.01f; runner.settleOnSpawn = true;
            var race = sim.AddComponent<SplitScreenRace>(); race.runner = runner; race.checkpoints = gates;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarImporter.PrefabPath);
            if (prefab == null) throw new FileNotFoundException("Import the Hellcat car first", CarImporter.PrefabPath);
            for (int i = 0; i < 2; i++)
            {
                var car = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                car.name = "Player " + (i + 1) + " Hellcat";
                var view = car.GetComponent<SoftBodyCarView>(); view.runner = runner;
                if (i == 1) race.secondView = view; else race.firstView = view;
                var cam = new GameObject("Player " + (i + 1) + " camera").AddComponent<Camera>();
                cam.fieldOfView = 65; cam.farClipPlane = 1000; cam.nearClipPlane = 0.1f;
                cam.rect = new Rect(i * 0.5f, 0, 0.5f, 1); CameraSky.Apply(cam);
                cam.transform.position = spawn.position - Tangent(0) * 8 + Vector3.up * 3;
                if (i == 0) { cam.tag = "MainCamera"; cam.gameObject.AddComponent<AudioListener>(); }
                race.cameras[i] = cam;
            }
            GameRigBuilder.Lighting(Vector3.zero, new Vector3(420, 60, 310));
            foreach (var camera in race.cameras) CameraSky.Apply(camera);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            var sceneList = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!sceneList.Exists(s => s.path == ScenePath)) sceneList.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = sceneList.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Crash Circuit created: two shared-world soft-body cars, 16 gates, 3 laps.");
        }

        public static void BuildPlayer()
        {
            int code = 1;
            try
            {
                Build(); BuildTasks.ApplyPlayerSettings();
                code = BuildTasks.Build(new[] { ScenePath }, Path.Combine(VehicleAssetSync.ProjectRoot, "Builds", "CrashCircuit", "CrashCircuit.exe"));
            }
            catch (System.Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(code);
        }
    }
}
