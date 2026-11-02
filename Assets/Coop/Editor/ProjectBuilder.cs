using System.IO;
using Coop.App;
using Coop.Gameplay;
using Coop.Session;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using SessionState = Coop.Session.SessionState;

namespace Coop.Editor
{
    public static class ProjectBuilder
    {
        const string Generated = "Assets/Coop/Generated";
        [MenuItem("Coop/Generate project assets")]
        public static void Generate()
        {
            Directory.CreateDirectory(Generated);
            PlayerSettings.companyName = "Yurii Tor";
            PlayerSettings.productName = "Dock Co-op";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            settings.FindProperty("activeInputHandler").intValue = 1;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var player = new GameObject("Player");
            player.AddComponent<NetworkObject>();
            var nt = player.AddComponent<OwnerNetworkTransform>();
            ConfigureTransform(nt);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = .35f;
            cc.center = new Vector3(0, .9f, 0);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(player.transform);
            body.transform.localPosition = new Vector3(0, .9f, 0);
            body.transform.localScale = new Vector3(.7f, .9f, .7f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            player.AddComponent<PlayerAvatar>();
            var playerPrefab = PrefabUtility.SaveAsPrefabAsset(player, Generated + "/Player.prefab");
            Object.DestroyImmediate(player);
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = "PhysicsItem";
            item.transform.localScale = Vector3.one * .65f;
            item.AddComponent<NetworkObject>().DontDestroyWithOwner = true;
            ConfigureTransform(item.AddComponent<NetworkTransform>());
            var rb = item.AddComponent<Rigidbody>();
            rb.mass = 1;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            item.AddComponent<NetworkRigidbody>();
            item.AddComponent<PhysicsItem>();
            var itemPrefab = PrefabUtility.SaveAsPrefabAsset(item, Generated + "/PhysicsItem.prefab");
            Object.DestroyImmediate(item);
            var state = new GameObject("SessionState");
            state.AddComponent<NetworkObject>();
            state.AddComponent<SessionState>();
            var statePrefab = PrefabUtility.SaveAsPrefabAsset(state, Generated + "/SessionState.prefab");
            Object.DestroyImmediate(state);
            var menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var app = new GameObject("CompositionRoot");
            var manager = app.AddComponent<NetworkManager>();
            var transport = app.AddComponent<UnityTransport>();
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, PlayerPrefab = playerPrefab, TickRate = 30, EnableSceneManagement = true, ConnectionApproval = true, LoadSceneTimeOut = 30 };
            var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            list.Add(new NetworkPrefab { Prefab = playerPrefab });
            list.Add(new NetworkPrefab { Prefab = itemPrefab });
            list.Add(new NetworkPrefab { Prefab = statePrefab });
            var existing = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(Generated + "/NetworkPrefabs.asset");
            if (existing)
            {
                EditorUtility.CopySerialized(list, existing);
                Object.DestroyImmediate(list);
                list = existing;
                EditorUtility.SetDirty(list);
            }
            else
                AssetDatabase.CreateAsset(list, Generated + "/NetworkPrefabs.asset");
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
            var root = app.AddComponent<CompositionRoot>();
            root.Network = manager;
            root.ItemPrefab = itemPrefab;
            root.SessionPrefab = statePrefab.GetComponent<SessionState>();
            EditorSceneManager.SaveScene(menu, Generated + "/Menu.unity");
            var arena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(arena, Generated + "/Arena.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Generated + "/Menu.unity", true), new EditorBuildSettingsScene(Generated + "/Arena.unity", true) };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(Generated + "/Menu.unity");
            Debug.Log("COOP_GENERATE_OK");
        }
        static void ConfigureTransform(NetworkTransform nt)
        {
            nt.Interpolate = true;
            nt.PositionThreshold = .02f;
            nt.RotAngleThreshold = 1f;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.UseUnreliableDeltas = true;
        }
        [MenuItem("Coop/Build Windows")]
        public static void BuildWindows()
        {
            Generate();
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { Generated + "/Menu.unity", Generated + "/Arena.unity" }, locationPathName = "Builds/Windows/DockCoop.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            File.WriteAllText("Builds/Windows/steam_appid.txt", "480\n");
            foreach (var launcher in Directory.GetFiles("tools/launchers"))
                File.Copy(launcher, Path.Combine("Builds/Windows", Path.GetFileName(launcher)), true);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception("Build failed: " + report.summary.result);
            Debug.Log("COOP_BUILD_OK " + report.summary.totalSize);
        }
    }
}
