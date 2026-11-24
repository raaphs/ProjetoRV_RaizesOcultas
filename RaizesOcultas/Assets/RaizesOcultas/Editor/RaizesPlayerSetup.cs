using System;
using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace RaizesOcultas.Editor
{
    internal static class RaizesPlayerSetup
    {
        private const string UndoLabel = "Corrigir jogador XR";

        [MenuItem("Raizes Ocultas/Corrigir câmera da cena aberta")]
        private static void RepairOpenScene()
        {
            RepairOpenScene(repairCamera: true);
        }

        [MenuItem("Raizes Ocultas/Corrigir colisão do jogador")]
        private static void RepairCollisionInOpenScene()
        {
            RepairOpenScene(repairCamera: false);
        }

        private static void RepairOpenScene(bool repairCamera)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.sceneCount != 1)
            {
                EditorUtility.DisplayDialog("Abra a cena do jogo",
                    "Saia do Play e abra somente a cena do protótipo antes de executar o reparo.", "OK");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            XROrigin[] origins = FindEnabledOrigins(scene);
            if (origins.Length == 0)
            {
                EditorUtility.DisplayDialog("XR Origin habilitado não encontrado",
                    "Abra a cena do protótipo e confira se o objeto XR Origin e seu componente estão habilitados no Inspector. A busca inclui objetos dentro de grupos da Hierarchy.", "OK");
                return;
            }
            if (origins.Length > 1)
            {
                string names = string.Join("\n", origins.Select(origin => origin.name));
                EditorUtility.DisplayDialog("Foram encontrados vários XR Origins",
                    $"Encontrei {origins.Length} XR Origins habilitados nesta cena.\n\n{names}\n\nConfira as instâncias na Hierarchy e desative a duplicada antes de executar o reparo.", "OK");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoLabel);
            try
            {
                XROrigin origin = origins[0];
                if (repairCamera)
                {
                    Camera camera = Apply(scene, origin);
                    Selection.activeGameObject = camera.gameObject;
                }
                else
                {
                    ConfigureLocomotion(scene, origin);
                    RepairGroundCollision(scene, origin);
                    Selection.activeGameObject = origin.Origin;
                }
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log("Raízes Ocultas: jogador XR configurado. Salve a cena e teste a colisão na aba Game.", origin);
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogException(exception);
            }
        }

        internal static XROrigin[] FindEnabledOrigins(Scene scene)
        {
            // No Editor, OnEnable pode ainda não ter sido chamado. Use os
            // valores configurados na cena para localizar o jogador fora do Play.
            return InScene<XROrigin>(scene)
                .Where(origin => origin.enabled && origin.gameObject.activeInHierarchy).ToArray();
        }

        internal static Camera Apply(Scene scene, XROrigin origin)
        {
            Camera camera = FindPlayerCamera(scene, origin);
            Transform offset = FindCameraOffset(origin, camera);
            Undo.RecordObjects(new UnityEngine.Object[] { origin, offset, offset.gameObject, origin.transform, camera,
                camera.transform, camera.gameObject }, UndoLabel);

            origin.enabled = true;
            origin.Origin = origin.gameObject;
            origin.CameraFloorOffsetObject = offset.gameObject;
            origin.CameraYOffset = 1.6f;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            SetParent(offset, origin.transform);
            offset.localPosition = Vector3.zero;
            offset.localRotation = Quaternion.identity;
            offset.localScale = Vector3.one;
            offset.gameObject.SetActive(true);
            SetParent(camera.transform, offset);
            camera.transform.localPosition = Vector3.zero;
            camera.transform.localRotation = Quaternion.identity;
            camera.transform.localScale = Vector3.one;
            camera.gameObject.SetActive(true);
            camera.gameObject.tag = "MainCamera";
            camera.enabled = true;
            camera.targetDisplay = 0;
            camera.stereoTargetEye = StereoTargetEyeMask.Both;
            origin.Camera = camera;

            ConfigureHeadTracking(camera);
            ConfigureLocomotion(scene, origin);
            RepairGroundCollision(scene, origin);
            UsePlayerView(scene, camera);
            RecordChanges(origin, offset, offset.gameObject, camera, camera.transform, camera.gameObject);
            return camera;
        }

        private static Camera FindPlayerCamera(Scene scene, XROrigin origin)
        {
            Camera[] children = origin.GetComponentsInChildren<Camera>(true)
                .Where(candidate => candidate.targetTexture == null).ToArray();
            Camera camera = children.FirstOrDefault(candidate => candidate == origin.Camera &&
                candidate.GetComponent<TrackedPoseDriver>() != null)
                ?? children.FirstOrDefault(candidate => candidate.GetComponent<TrackedPoseDriver>() != null);
            if (camera == null && origin.Camera != null && origin.Camera.gameObject.scene == scene &&
                origin.Camera.targetTexture == null)
                camera = origin.Camera;
            if (camera == null)
                camera = children.FirstOrDefault(candidate => candidate.CompareTag("MainCamera")) ?? children.FirstOrDefault();
            if (camera != null)
                return camera;

            var head = new GameObject("Main Camera", typeof(Camera));
            Undo.RegisterCreatedObjectUndo(head, UndoLabel);
            return head.GetComponent<Camera>();
        }

        private static Transform FindCameraOffset(XROrigin origin, Camera camera)
        {
            Transform offset = origin.CameraFloorOffsetObject != null
                ? origin.CameraFloorOffsetObject.transform : null;
            if (offset == null || offset == origin.transform || offset == camera.transform ||
                !offset.IsChildOf(origin.transform) || offset.IsChildOf(camera.transform))
            {
                offset = origin.transform.Find("Camera Offset");
            }
            if (offset == null || offset == camera.transform || offset.IsChildOf(camera.transform))
            {
                var group = new GameObject("Camera Offset");
                Undo.RegisterCreatedObjectUndo(group, UndoLabel);
                offset = group.transform;
            }
            return offset;
        }

        private static void ConfigureHeadTracking(Camera camera)
        {
            TrackedPoseDriver driver = camera.GetComponent<TrackedPoseDriver>();
            if (driver == null)
                driver = Undo.AddComponent<TrackedPoseDriver>(camera.gameObject);
            Undo.RecordObject(driver, UndoLabel);
            driver.enabled = false;
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            driver.ignoreTrackingState = false;
            // Ações locais são habilitadas pelo Tracked Pose Driver e também
            // recebem a pose de um HMD criado pelo XR Device Simulator.
            driver.positionInput = new InputActionProperty(new InputAction("Head Position", InputActionType.Value,
                "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            driver.rotationInput = new InputActionProperty(new InputAction("Head Rotation", InputActionType.Value,
                "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
            driver.trackingStateInput = new InputActionProperty(new InputAction("Head Tracking State", InputActionType.Value,
                "<XRHMD>/trackingState", expectedControlType: "Integer"));
            driver.enabled = true;
            RecordChanges(driver);
        }

        private static void ConfigureLocomotion(Scene scene, XROrigin origin)
        {
            ConfigureCharacterController(origin);
            LocomotionMediator mediator = origin.GetComponentsInChildren<LocomotionMediator>(true)
                .FirstOrDefault(candidate => candidate.gameObject.activeInHierarchy)
                ?? InScene<LocomotionMediator>(scene).FirstOrDefault(candidate => candidate.gameObject.activeInHierarchy);
            if (mediator == null)
                mediator = Undo.AddComponent<LocomotionMediator>(origin.gameObject);
            XRBodyTransformer body = mediator.GetComponent<XRBodyTransformer>();
            if (body == null)
                body = Undo.AddComponent<XRBodyTransformer>(mediator.gameObject);
            Undo.RecordObjects(new UnityEngine.Object[] { body, mediator }, UndoLabel);
            body.xrOrigin = origin;
            body.useCharacterControllerIfExists = true;
            body.enabled = true;
            mediator.enabled = true;
            foreach (LocomotionProvider provider in InScene<LocomotionProvider>(scene)
                         .Where(candidate => candidate.gameObject.activeInHierarchy))
            {
                Undo.RecordObject(provider, UndoLabel);
                provider.mediator = mediator;
                if (provider is ContinuousMoveProvider move)
                {
                    move.enableFly = false;
                    move.useGravity = true;
                }
                RecordChanges(provider);
            }
            RecordChanges(body, mediator);
        }

        private static void ConfigureCharacterController(XROrigin origin)
        {
            Undo.RecordObject(origin, UndoLabel);
            if (origin.Origin == null)
                origin.Origin = origin.gameObject;
            GameObject player = origin.Origin;
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller == null)
                controller = Undo.AddComponent<CharacterController>(player);
            Undo.RecordObject(controller, UndoLabel);
            controller.enabled = true;
            controller.detectCollisions = true;
            controller.radius = 0.22f;
            controller.height = 1.60f;
            controller.center = new Vector3(0f, 0.80f, 0f);
            controller.skinWidth = 0.03f;
            controller.stepOffset = 0.20f;
            controller.slopeLimit = 45f;
            controller.minMoveDistance = 0f;

            // O XRI move a cápsula pela locomoção. Um Rigidbody dinâmico no
            // mesmo objeto aplicaria uma segunda simulação de gravidade.
            Rigidbody rigidbody = player.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                Undo.RecordObject(rigidbody, UndoLabel);
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
                RecordChanges(rigidbody);
            }
            RecordChanges(origin, controller);
        }

        private static void RepairGroundCollision(Scene scene, XROrigin origin)
        {
            BoxCollider ground = InScene<BoxCollider>(scene).FirstOrDefault(candidate =>
                candidate.name == "Solo" && candidate.transform.parent != null &&
                candidate.transform.parent.name == "Clareira");
            if (ground != null)
                ConfigureGroundCollision(ground, origin);
        }

        internal static void ConfigureGroundCollision(BoxCollider ground, XROrigin origin)
        {
            CharacterController controller = origin.Origin.GetComponent<CharacterController>();
            Undo.RecordObjects(new UnityEngine.Object[] { ground, ground.gameObject, controller }, UndoLabel);
            ground.gameObject.SetActive(true);
            ground.enabled = true;
            ground.isTrigger = false;
            int playerLayer = 1 << controller.gameObject.layer;
            int groundLayer = 1 << ground.gameObject.layer;
            ground.includeLayers = ground.includeLayers.value | playerLayer;
            ground.excludeLayers = ground.excludeLayers.value & ~playerLayer;
            controller.includeLayers = controller.includeLayers.value | groundLayer;
            controller.excludeLayers = controller.excludeLayers.value & ~groundLayer;

            Physics.SyncTransforms();
            Transform player = origin.Origin.transform;
            float minimumY = ground.bounds.max.y + 0.03f;
            if (player.position.y < minimumY)
            {
                Undo.RecordObject(player, UndoLabel);
                Vector3 position = player.position;
                position.y = minimumY;
                player.position = position;
                RecordChanges(player);
            }
            RecordChanges(ground, ground.gameObject, controller);
        }

        private static void UsePlayerView(Scene scene, Camera playerCamera)
        {
            foreach (Camera other in InScene<Camera>(scene)
                         .Where(candidate => candidate != playerCamera && candidate.targetTexture == null))
            {
                Undo.RecordObjects(new UnityEngine.Object[] { other, other.gameObject }, UndoLabel);
                other.enabled = false;
                if (other.CompareTag("MainCamera"))
                    other.gameObject.tag = "Untagged";
                RecordChanges(other, other.gameObject);
            }
            AudioListener listener = playerCamera.GetComponent<AudioListener>();
            if (listener == null)
                listener = Undo.AddComponent<AudioListener>(playerCamera.gameObject);
            foreach (AudioListener other in InScene<AudioListener>(scene))
            {
                Undo.RecordObject(other, UndoLabel);
                other.enabled = other == listener;
                RecordChanges(other);
            }
            foreach (Canvas canvas in InScene<Canvas>(scene).Where(candidate => candidate.renderMode == RenderMode.WorldSpace))
            {
                Undo.RecordObject(canvas, UndoLabel);
                canvas.worldCamera = playerCamera;
                RecordChanges(canvas);
            }
            foreach (FacePlayer panel in InScene<FacePlayer>(scene))
            {
                Undo.RecordObject(panel, UndoLabel);
                var serialized = new SerializedObject(panel);
                serialized.FindProperty("viewer").objectReferenceValue = playerCamera.transform;
                serialized.ApplyModifiedProperties();
            }
            foreach (FieldTeleportFade fade in InScene<FieldTeleportFade>(scene))
            {
                SetParent(fade.transform, playerCamera.transform);
                Undo.RecordObject(fade.transform, UndoLabel);
                fade.transform.localPosition = new Vector3(0f, 0f, Mathf.Max(0.24f, playerCamera.nearClipPlane + 0.08f));
                fade.transform.localRotation = Quaternion.identity;
                RecordChanges(fade.transform);
            }
        }

        private static void SetParent(Transform child, Transform parent)
        {
            if (child.parent == parent)
                return;
            if (PrefabUtility.IsPartOfPrefabInstance(child))
            {
                GameObject prefab = PrefabUtility.GetOutermostPrefabInstanceRoot(child.gameObject);
                PrefabUtility.UnpackPrefabInstance(prefab, PrefabUnpackMode.Completely, InteractionMode.UserAction);
            }
            Undo.SetTransformParent(child, parent, UndoLabel);
        }

        private static void RecordChanges(params UnityEngine.Object[] objects)
        {
            foreach (UnityEngine.Object target in objects)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(target))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                EditorUtility.SetDirty(target);
            }
        }

        private static IEnumerable<T> InScene<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    }
}
