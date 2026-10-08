using System.IO;
using Fusion;
using Fusion.Editor;
using MultiplayerDemo.Multiplayer;
using MultiplayerDemo.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MultiplayerDemo.Editor
{
    public static class NetworkFoundationSetup
    {
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

        [InitializeOnLoadMethod]
        private static void BuildOnceAfterScriptsCompile()
        {
            if (!File.Exists(PlayerPrefabPath))
            {
                EditorApplication.delayCall += Build;
            }
            else
            {
                EditorApplication.delayCall += UpgradePlayerMovementIfNeeded;
            }
        }

        [MenuItem("Tools/Multiplayer Demo/Build Fusion Foundation")]
        public static void Build()
        {
            EnsureFolders();
            GameObject playerPrefab = CreatePlayerPrefab();
            ConfigureBootstrapScene(playerPrefab);
            ConfigureBuildSettings();
            NetworkProjectConfigUtilities.RebuildPrefabTable();
            ValidateFoundation(playerPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Fusion Foundation Setup] Foundation created and validated.");
        }

        public static void BuildBatch()
        {
            Build();
            EditorApplication.Exit(0);
        }

        [MenuItem("Tools/Multiplayer Demo/Add Player Movement")]
        public static void AddPlayerMovement()
        {
            UpgradePlayerPrefabForMovement();
            AddNetworkEventsToBootstrapScene();
            NetworkProjectConfigUtilities.RebuildPrefabTable();

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            ValidateFoundation(playerPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Fusion Movement Setup] CharacterController movement and mouse look were added and validated.");
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory("Assets/Prefabs");
            Directory.CreateDirectory("Assets/Scripts/Multiplayer/Editor");
        }

        private static GameObject CreatePlayerPrefab()
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";

            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            CharacterController characterController = player.AddComponent<CharacterController>();
            characterController.height = 2f;
            characterController.radius = 0.5f;
            characterController.center = Vector3.zero;
            characterController.stepOffset = 0.3f;

            player.AddComponent<NetworkObject>();
            NetworkCharacterController networkController = player.AddComponent<NetworkCharacterController>();
            networkController.maxSpeed = 5f;
            networkController.gravity = -20f;
            networkController.rotationSpeed = 0f;
            player.AddComponent<NetworkPlayerMovement>();
            player.AddComponent<NetworkPlayerIdentity>();

            GameObject cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            cameraObject.transform.localRotation = Quaternion.identity;

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            AudioListener listener = cameraObject.AddComponent<AudioListener>();
            listener.enabled = false;

            PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            Object.DestroyImmediate(player);
            AssetDatabase.ImportAsset(PlayerPrefabPath, ImportAssetOptions.ForceUpdate);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            if (prefab == null || !prefab.TryGetComponent<NetworkObject>(out _))
            {
                throw new InvalidDataException("Could not create the network player prefab.");
            }

            return prefab;
        }

        private static void ConfigureBootstrapScene(GameObject playerPrefab)
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);

            foreach (NetworkFoundationBootstrap existing in Object.FindObjectsByType<NetworkFoundationBootstrap>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            Camera sceneCamera = Object.FindFirstObjectByType<Camera>();
            if (sceneCamera != null)
            {
                sceneCamera.gameObject.SetActive(false);
            }

            GameObject foundation = new GameObject("Fusion Network Foundation");
            foundation.AddComponent<NetworkRunner>();
            foundation.AddComponent<NetworkSceneManagerDefault>();
            foundation.AddComponent<NetworkEvents>();
            NetworkFoundationBootstrap bootstrap = foundation.AddComponent<NetworkFoundationBootstrap>();

            SerializedObject serializedBootstrap = new SerializedObject(bootstrap);
            serializedBootstrap.FindProperty("sessionName").stringValue = "PracticalExamSession";
            serializedBootstrap.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

            GameObject floor = GameObject.Find("Foundation Floor");
            if (floor == null)
            {
                floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Foundation Floor";
                floor.transform.localScale = new Vector3(2f, 1f, 2f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureBuildSettings()
        {
            string[] paths =
            {
                "Assets/Scenes/Bootstrap.unity",
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/Lobby.unity",
                "Assets/Scenes/Game scenes.unity"
            };

            EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                scenes[i] = new EditorBuildSettingsScene(paths[i], true);
            }

            EditorBuildSettings.scenes = scenes;
        }

        private static void ValidateFoundation(GameObject playerPrefab)
        {
            if (!playerPrefab.TryGetComponent<NetworkObject>(out _) ||
                !playerPrefab.TryGetComponent<CharacterController>(out _) ||
                !playerPrefab.TryGetComponent<NetworkCharacterController>(out _) ||
                !playerPrefab.TryGetComponent<NetworkPlayerMovement>(out _) ||
                !playerPrefab.TryGetComponent<NetworkPlayerIdentity>(out _))
            {
                throw new InvalidDataException("Player prefab is missing a required movement, Fusion, or identity component.");
            }

            if (playerPrefab.TryGetComponent<NetworkTransform>(out _))
            {
                throw new InvalidDataException("Player prefab must use NetworkCharacterController instead of NetworkTransform.");
            }

            Transform cameraTransform = playerPrefab.transform.Find("Camera");
            if (cameraTransform == null || cameraTransform.GetComponent<Camera>() == null)
            {
                throw new InvalidDataException("Player prefab must contain a direct Camera child.");
            }

            Scene bootstrapScene = SceneManager.GetSceneByPath(BootstrapScenePath);
            bool sceneWasAlreadyLoaded = bootstrapScene.IsValid() && bootstrapScene.isLoaded;

            if (!sceneWasAlreadyLoaded)
            {
                bootstrapScene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Additive);
            }

            NetworkFoundationBootstrap bootstrap = FindBootstrap(bootstrapScene);
            if (bootstrap == null ||
                bootstrap.GetComponent<NetworkRunner>() == null ||
                bootstrap.GetComponent<NetworkSceneManagerDefault>() == null ||
                bootstrap.GetComponent<NetworkEvents>() == null)
            {
                throw new InvalidDataException("Bootstrap scene is missing a required Fusion foundation component.");
            }

            SerializedObject serializedBootstrap = new SerializedObject(bootstrap);
            if (serializedBootstrap.FindProperty("playerPrefab").objectReferenceValue != playerPrefab)
            {
                throw new InvalidDataException("Bootstrap scene does not reference the Player prefab.");
            }

            if (!sceneWasAlreadyLoaded)
            {
                EditorSceneManager.CloseScene(bootstrapScene, true);
            }
        }

        private static void UpgradePlayerMovementIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                return;
            }

            bool needsUpgrade =
                playerPrefab.GetComponent<CharacterController>() == null ||
                playerPrefab.GetComponent<NetworkCharacterController>() == null ||
                playerPrefab.GetComponent<NetworkPlayerMovement>() == null ||
                playerPrefab.GetComponent<NetworkTransform>() != null;

            if (needsUpgrade)
            {
                AddPlayerMovement();
            }
            else
            {
                AddNetworkEventsToBootstrapScene();
            }
        }

        private static void UpgradePlayerPrefabForMovement()
        {
            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            try
            {
                NetworkTransform oldNetworkTransform = player.GetComponent<NetworkTransform>();
                if (oldNetworkTransform != null)
                {
                    Object.DestroyImmediate(oldNetworkTransform);
                }

                CapsuleCollider capsuleCollider = player.GetComponent<CapsuleCollider>();
                if (capsuleCollider != null)
                {
                    Object.DestroyImmediate(capsuleCollider);
                }

                CharacterController characterController = player.GetComponent<CharacterController>();
                if (characterController == null)
                {
                    characterController = player.AddComponent<CharacterController>();
                }

                characterController.height = 2f;
                characterController.radius = 0.5f;
                characterController.center = Vector3.zero;
                characterController.stepOffset = 0.3f;

                NetworkCharacterController networkController = player.GetComponent<NetworkCharacterController>();
                if (networkController == null)
                {
                    networkController = player.AddComponent<NetworkCharacterController>();
                }

                networkController.maxSpeed = 5f;
                networkController.gravity = -20f;
                networkController.rotationSpeed = 0f;

                if (player.GetComponent<NetworkPlayerMovement>() == null)
                {
                    player.AddComponent<NetworkPlayerMovement>();
                }

                Transform cameraTransform = player.transform.Find("Camera");
                if (cameraTransform != null)
                {
                    cameraTransform.localPosition = new Vector3(0f, 0.6f, 0f);
                    cameraTransform.localRotation = Quaternion.identity;
                }

                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }

            AssetDatabase.ImportAsset(PlayerPrefabPath, ImportAssetOptions.ForceUpdate);
        }

        private static void AddNetworkEventsToBootstrapScene()
        {
            Scene scene = SceneManager.GetSceneByPath(BootstrapScenePath);
            bool sceneWasAlreadyLoaded = scene.IsValid() && scene.isLoaded;

            if (!sceneWasAlreadyLoaded)
            {
                scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Additive);
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (NetworkFoundationBootstrap bootstrap in root.GetComponentsInChildren<NetworkFoundationBootstrap>(true))
                {
                    if (bootstrap.GetComponent<NetworkEvents>() == null)
                    {
                        bootstrap.gameObject.AddComponent<NetworkEvents>();
                        EditorSceneManager.MarkSceneDirty(scene);
                    }
                }
            }

            if (scene.isDirty)
            {
                EditorSceneManager.SaveScene(scene);
            }

            if (!sceneWasAlreadyLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static NetworkFoundationBootstrap FindBootstrap(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                NetworkFoundationBootstrap bootstrap = root.GetComponentInChildren<NetworkFoundationBootstrap>(true);
                if (bootstrap != null)
                {
                    return bootstrap;
                }
            }

            return null;
        }
    }
}
