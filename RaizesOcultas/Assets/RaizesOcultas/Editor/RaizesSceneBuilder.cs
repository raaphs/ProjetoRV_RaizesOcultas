using System;
using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace RaizesOcultas.Editor
{
    internal static class RaizesSceneBuilder
    {
        private const string PackagePath = "Assets/RaizesOcultas";
        private const string OutputPath = PackagePath + "/Gerado";
        private const string MenuPath = "Raizes Ocultas/Gerar cena do protótipo";

        private sealed class Rig
        {
            public XROrigin Origin;
            public Transform Left;
            public Transform Right;
            public TeleportationArea SampleGround;
            public TeleportationProvider Teleport;
            public EventSystem Events;
            public XRInteractionManager Interactions;
        }

        private static readonly Color BoardBackground = new Color32(23, 34, 31, 251);
        private static readonly Color BoardText = new Color32(248, 242, 224, 255);
        private static readonly Color BoardAccent = new Color32(225, 187, 124, 255);

        [MenuItem(MenuPath)]
        private static void Build()
        {
            Scene source = SceneManager.GetActiveScene();
            string sourcePath = source.path;
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SceneManager.sceneCount != 1 || string.IsNullOrEmpty(sourcePath) ||
                sourcePath.StartsWith(OutputPath + "/", StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog("Abra a cena do template",
                    "Fora do Play, abra somente a SampleScene original do template VR. Depois execute o menu novamente.", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (!TryGetRig(source, out _))
            {
                EditorUtility.DisplayDialog("Configuração XR incompleta",
                    "Abra a SampleScene do template VR com XR Interaction Toolkit 3.x. A cena precisa ter um XR Origin com câmera e dois controles, XR Interaction Manager, XR UI Input Module e uma Teleportation Area ativa.", "OK");
                return;
            }

            EnsureFolder(PackagePath, "Gerado");
            EnsureFolder(OutputPath, "Materiais");
            string destination = AssetDatabase.GenerateUniqueAssetPath(OutputPath + "/RaizesOcultas.unity");
            if (!AssetDatabase.CopyAsset(sourcePath, destination))
            {
                Debug.LogError("Não foi possível copiar a cena XR para " + destination);
                return;
            }

            try
            {
                Scene output = EditorSceneManager.OpenScene(destination, OpenSceneMode.Single);
                if (!TryGetRig(output, out Rig rig))
                    throw new InvalidOperationException("Não encontrei o rig XR na cópia da cena.");
                GameObject prototype = Populate(output, rig);
                EditorSceneManager.MarkSceneDirty(output);
                if (!EditorSceneManager.SaveScene(output))
                    throw new InvalidOperationException("Não foi possível salvar a cena do protótipo.");
                AssetDatabase.SaveAssets();
                Selection.activeGameObject = prototype;
                Debug.Log("Raízes Ocultas: cena gerada em " + destination);
            }
            catch (Exception exception)
            {
                EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Single);
                AssetDatabase.DeleteAsset(destination);
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("A geração foi interrompida", exception.Message, "OK");
            }
        }

        private static GameObject Populate(Scene scene, Rig rig)
        {
            var art = new RaizesSceneArt(OutputPath + "/Materiais");
            RaizesPlayerSetup.Apply(scene, rig.Origin);
            Transform playerRoot = rig.Origin.Origin.transform;
            Undo.RecordObject(playerRoot, "Posicionar jogador");
            playerRoot.SetPositionAndRotation(new Vector3(0f, 0f, -3.0f), Quaternion.identity);
            if (PrefabUtility.IsPartOfPrefabInstance(playerRoot))
                PrefabUtility.RecordPrefabInstancePropertyModifications(playerRoot);
            ContinuousMoveProvider move = rig.Origin.GetComponentInChildren<ContinuousMoveProvider>(true)
                                          ?? FindInScene<ContinuousMoveProvider>(scene);
            if (move != null && move.gameObject.activeInHierarchy)
            {
                Undo.RecordObject(move, "Configurar movimento do jogador");
                move.enabled = true;
                move.moveSpeed = 1.15f;
                if (PrefabUtility.IsPartOfPrefabInstance(move))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(move);
            }
            else
                Debug.LogWarning("Provedor contínuo não encontrado. Consulte a parte de locomoção no LEIA-ME.");

            rig.Teleport.delayTime = FieldTeleportFade.Delay;
            var teleportMask = rig.SampleGround.interactionLayers;
            var teleportTrigger = rig.SampleGround.teleportTrigger;
            int groundLayer = rig.SampleGround.colliders.FirstOrDefault(collider => collider != null)?.gameObject.layer
                              ?? rig.SampleGround.gameObject.layer;
            XRGrabInteractable exampleProp = FindInScene<XRGrabInteractable>(scene);
            InteractionLayerMask grabMask = exampleProp != null
                ? exampleProp.interactionLayers : (InteractionLayerMask)InteractionLayerMask.GetMask("Default");
            int propLayer = exampleProp != null
                ? exampleProp.colliders.FirstOrDefault(collider => collider != null)?.gameObject.layer
                  ?? exampleProp.gameObject.layer
                : 0;

            var preserved = new Component[] { rig.Origin, rig.Teleport, rig.Teleport.mediator, move, rig.Events, rig.Interactions };
            var preservedRoots = new HashSet<GameObject>(preserved.Where(component => component != null)
                .Select(component => component.transform.root.gameObject)
                .Concat(ComponentsInScene<InputActionManager>(scene)
                    .Where(component => component.enabled && component.gameObject.activeInHierarchy)
                    .Select(component => component.transform.root.gameObject)));
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (!preservedRoots.Contains(root))
                    Object.DestroyImmediate(root);
            }

            GameObject prototype = RaizesSceneArt.Group("Raízes Ocultas", null);
            Transform stage = RaizesSceneArt.Group("Clareira", prototype.transform).transform;
            art.Light(stage);
            art.Landscape(stage);
            GameObject ground = art.Part("Solo", stage, PrimitiveType.Cube, new Vector3(0f, -0.15f, 2.50f),
                new Vector3(13f, 0.3f, 13f), art.Material("Terreno", "#7C6952"), true);
            ground.layer = groundLayer;
            RaizesPlayerSetup.ConfigureGroundCollision(ground.GetComponent<BoxCollider>(), rig.Origin);
            var area = ground.AddComponent<TeleportationArea>();
            area.colliders.Clear();
            area.colliders.Add(ground.GetComponent<BoxCollider>());
            area.interactionLayers = teleportMask;
            area.interactionManager = rig.Interactions;
            area.teleportationProvider = rig.Teleport;
            area.teleportTrigger = teleportTrigger;
            UnityEventTools.AddPersistentListener(area.teleporting, CreateFade(rig.Origin.Camera).Begin);
            art.Part("Trilha", stage, PrimitiveType.Cube, new Vector3(0f, -0.012f, 1.48f),
                new Vector3(1.40f, 0.035f, 9.10f), art.Material("Trilha", "#958166"));
            AddBoundaries(stage);

            art.Glove(rig.Left);
            art.Glove(rig.Right);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                throw new InvalidOperationException("A fonte interna LegacyRuntime.ttf não foi encontrada.");

            Transform ui = RaizesSceneArt.Group("Painéis", prototype.transform).transform;
            var intro = CreateBoard("Entrada", ui, new Vector3(0f, 1.49f, -1.12f), 750f, 345f, 0.00225f,
                "Raízes Ocultas",
                "Memórias Afrodescendentes\n\nEste sítio fictício é inspirado na memória de comunidades quilombolas. Registre três vestígios e o contexto de cada achado.",
                26, font, rig.Origin.Camera, faceView: false, interactive: true);
            Button start = AddButton(intro.Root, "Iniciar pesquisa", font, -134f);
            var journal = CreateBoard("Diário de pulso", rig.Left, new Vector3(0.035f, 0.111f, -0.096f),
                460f, 344f, 0.00086f, "Diário de campo", "", 22, font, rig.Origin.Camera, upright: false, interactive: true);
            Button wristButton = AddButton(journal.Root, "Iniciar pesquisa", font, -134f);
            journal.Root.SetActive(false);
            var catalog = CreateBoard("Catálogo final", ui, new Vector3(0f, 1.50f, 6.52f),
                850f, 440f, 0.00215f, "Sítio mapeado · Catálogo de campo", "", 27, font, rig.Origin.Camera);
            catalog.Root.SetActive(false);

            Transform bench = RaizesSceneArt.Group("Bancada", stage, new Vector3(2.02f, 0f, -1.48f)).transform;
            art.Bench(bench);
            GameObject equipment = RaizesSceneArt.Group("Ferramentas", bench);
            CreateBoard("Orientação da bancada", bench, new Vector3(0f, 1.27f, 0.33f), 505f, 140f,
                0.00185f, "Bancada de campo", "Pá e pincel · Segure com a empunhadura", 21, font, rig.Origin.Camera);

            FieldFeedback feedback = CreateFeedback(art, prototype.transform);
            FieldSession session = prototype.AddComponent<FieldSession>();
            var sites = new[]
            {
                CreatePlot(art, rig, stage, session, font, 0, "Quadra A", "Fragmento de cerâmica",
                    "Uma pista sobre recipientes e atividades do cotidiano.",
                    "A cerâmica ajuda a investigar como alimentos eram preparados e guardados. No Vale do Ribeira, saberes quilombolas ligam o cuidado das roças ao preparo do que se colhe.\n\nFonte · Iphan",
                    new Vector3(-3.16f, 0f, 1.34f), grabMask, propLayer),
                CreatePlot(art, rig, stage, session, font, 1, "Quadra B", "Conta perfurada",
                    "A posição e os vestígios do entorno ajudam a interpretar o adorno.",
                    "A posição da conta dá pistas sobre seu uso. No Campinho da Independência, moradores preservam em relatos a história de Antonica, Marcelina e Luiza, suas fundadoras.\n\nFonte · ICMBio",
                    new Vector3(0f, 0f, 3.37f), grabMask, propLayer),
                CreatePlot(art, rig, stage, session, font, 2, "Quadra C", "Pedra de moagem",
                    "Marcas de desgaste são pistas sobre práticas de trabalho.",
                    "O desgaste de uma pedra pode sugerir trabalho de moagem. Na Casa de Farinha Nascimento, no Espírito Santo, fazer farinha e beiju reúne famílias e transmite saberes do quilombo.\n\nFonte · MuseusBr",
                    new Vector3(3.12f, 0f, 1.78f), grabMask, propLayer)
            };
            CreateTool(art, rig, equipment.transform, FieldTool.Kind.Shovel, sites, feedback, grabMask, propLayer);
            CreateTool(art, rig, equipment.transform, FieldTool.Kind.Brush, sites, feedback, grabMask, propLayer);
            equipment.SetActive(false);
            Configure(session, ("equipment", equipment), ("startButton", start), ("introText", intro.Body),
                ("journal", journal.Root), ("journalText", journal.Body), ("wristButton", wristButton), ("catalog", catalog.Root),
                ("catalogText", catalog.Body), ("sites", sites), ("feedback", feedback));
            return prototype;
        }

        private static FieldSite CreatePlot(RaizesSceneArt art, Rig rig, Transform parent, FieldSession session,
            Font font, int model, string plotName, string itemName, string catalogNote, string story,
            Vector3 position, InteractionLayerMask grabMask, int layer)
        {
            var plot = RaizesSceneArt.Group(plotName, parent, position);
            art.PlotFrame(plot.transform);
            Transform earth = RaizesSceneArt.Group("Camadas de terra", plot.transform).transform;
            var layers = new[]
            {
                art.Part("01 · Superfície", earth, PrimitiveType.Sphere, new Vector3(0f, 0.469f, 0f),
                    new Vector3(0.96f, 0.25f, 0.68f), art.Material("TerraSuperficie", "#866044")),
                art.Part("02 · Intermediária", earth, PrimitiveType.Sphere, new Vector3(0f, 0.420f, 0f),
                    new Vector3(0.84f, 0.21f, 0.58f), art.Material("TerraIntermediaria", "#A37855")),
                art.Part("03 · Última", earth, PrimitiveType.Sphere, new Vector3(0f, 0.401f, 0f),
                    new Vector3(0.65f, 0.19f, 0.46f), art.Material("TerraFina", "#BF9670"))
            };
            GameObject artifact = RaizesSceneArt.Group(itemName, plot.transform, new Vector3(0f, 0.426f, 0f));
            art.ArtifactModel(artifact.transform, model);
            FieldObject prop = ConfigureGrab(artifact, rig, grabMask, layer,
                new Vector3(0.265f, 0.125f, 0.190f), Vector3.zero, startsLocked: true, returnAfter: 0f);

            Transform dirt = RaizesSceneArt.Group("Manchas de sujeira", plot.transform).transform;
            Material crust = art.Material("Mancha", "#6A4F3B");
            var patches = new[]
            {
                art.Part("01", dirt, PrimitiveType.Sphere, new Vector3(-0.074f, 0.478f, 0.022f),
                    new Vector3(0.13f, 0.046f, 0.10f), crust),
                art.Part("02", dirt, PrimitiveType.Sphere, new Vector3(0.062f, 0.480f, -0.018f),
                    new Vector3(0.13f, 0.040f, 0.10f), crust),
                art.Part("03", dirt, PrimitiveType.Sphere, new Vector3(0f, 0.480f, 0.056f),
                    new Vector3(0.11f, 0.037f, 0.09f), crust)
            };
            dirt.SetParent(artifact.transform, true);
            artifact.SetActive(false);

            var status = CreateBoard("Placa " + plotName, plot.transform, new Vector3(0f, 0.98f, 0.54f),
                502f, 174f, 0.00175f, plotName, "Pá · Faça três cavadas. Levante a pá entre elas.", 22, font, rig.Origin.Camera);
            var panel = CreateBoard("Leitura " + plotName, plot.transform, new Vector3(0.84f, 1.47f, 0.46f),
                690f, 300f, 0.00205f, itemName, story, 26, font, rig.Origin.Camera);
            panel.Root.SetActive(false);

            Transform dig = RaizesSceneArt.Group("Ponto de escavação", plot.transform, new Vector3(0f, 0.50f, 0f)).transform;
            Transform brush = RaizesSceneArt.Group("Ponto de limpeza", plot.transform, new Vector3(0f, 0.48f, 0f)).transform;
            FieldSite site = plot.AddComponent<FieldSite>();
            Configure(site, ("plotName", plotName), ("itemName", itemName), ("catalogNote", catalogNote),
                ("digPoint", dig), ("brushPoint", brush), ("soilLayers", layers), ("dirtPatches", patches),
                ("artifact", prop), ("storyCard", panel.Root), ("statusText", status.Body), ("session", session));
            return site;
        }

        private static void CreateTool(RaizesSceneArt art, Rig rig, Transform parent, FieldTool.Kind kind,
            FieldSite[] sites, FieldFeedback feedback, InteractionLayerMask mask, int layer)
        {
            bool shovel = kind == FieldTool.Kind.Shovel;
            var tool = RaizesSceneArt.Group(shovel ? "Pá" : "Pincel", parent,
                new Vector3(shovel ? -0.30f : 0.30f, 0.945f, 0.05f));
            tool.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            art.ToolModel(tool.transform, kind);
            ConfigureGrab(tool, rig, mask, layer,
                new Vector3(shovel ? 0.16f : 0.087f, shovel ? 0.56f : 0.34f, 0.048f),
                new Vector3(0f, shovel ? -0.046f : -0.010f, 0f), startsLocked: false, returnAfter: 14f);
            Transform tip = RaizesSceneArt.Group("Ponta", tool.transform,
                new Vector3(0f, shovel ? -0.324f : -0.177f, 0f)).transform;
            FieldTool script = tool.AddComponent<FieldTool>();
            Configure(script, ("kind", kind), ("tip", tip), ("sites", sites), ("feedback", feedback));
        }

        private static FieldObject ConfigureGrab(GameObject prop, Rig rig, InteractionLayerMask mask, int layer,
            Vector3 size, Vector3 center, bool startsLocked, float returnAfter)
        {
            prop.layer = layer;
            BoxCollider collider = prop.AddComponent<BoxCollider>();
            collider.size = size;
            collider.center = center;

            Rigidbody body = prop.AddComponent<Rigidbody>();
            body.mass = startsLocked ? 0.32f : 0.52f;
            body.useGravity = !startsLocked;
            body.isKinematic = startsLocked;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var grab = prop.AddComponent<XRGrabInteractable>();
            grab.colliders.Clear();
            grab.colliders.Add(collider);
            grab.interactionManager = rig.Interactions;
            grab.interactionLayers = mask;
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.useDynamicAttach = true;
            grab.throwOnDetach = false;
            grab.enabled = !startsLocked;

            FieldObject component = prop.AddComponent<FieldObject>();
            Configure(component, ("leftController", rig.Left), ("rightController", rig.Right),
                ("visualParts", prop.GetComponentsInChildren<Renderer>()), ("returnAfterSeconds", returnAfter));
            return component;
        }

        private static FieldFeedback CreateFeedback(RaizesSceneArt art, Transform parent)
        {
            GameObject sounds = RaizesSceneArt.Group("Áudio e poeira", parent);
            var contact = RaizesSceneArt.Group("Contato 3D", sounds.transform).AddComponent<AudioSource>();
            contact.playOnAwake = false;
            contact.spatialBlend = 1f;
            contact.minDistance = 0.7f;
            contact.maxDistance = 5f;
            contact.rolloffMode = AudioRolloffMode.Linear;
            contact.volume = 0.40f;
            var cue = RaizesSceneArt.Group("Toque de descoberta", sounds.transform).AddComponent<AudioSource>();
            cue.playOnAwake = false;
            cue.volume = 0.38f;
            var ambience = RaizesSceneArt.Group("Ambiente da mata", sounds.transform).AddComponent<AudioSource>();
            ambience.clip = Clip("Ambiente");
            ambience.playOnAwake = true;
            ambience.loop = true;
            ambience.volume = 0.24f;

            GameObject dustObject = RaizesSceneArt.Group("Poeira", sounds.transform);
            dustObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var dust = dustObject.AddComponent<ParticleSystem>();
            var main = dust.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.27f, 0.58f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.17f, 0.40f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.014f, 0.028f);
            main.gravityModifier = 0.12f;
            main.maxParticles = 100;
            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.02f;
            var emission = dust.emission;
            emission.enabled = false;
            var dustRenderer = dustObject.GetComponent<ParticleSystemRenderer>();
            dustRenderer.sharedMaterial = art.Material("Poeira", "#D6B283",
                Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            var feedback = sounds.AddComponent<FieldFeedback>();
            Configure(feedback, ("contactSound", contact), ("discoverySound", cue), ("shovelClip", Clip("Pa")),
                ("brushClip", Clip("Pincel")), ("discoveryClip", Clip("Descoberta")), ("dust", dust));
            return feedback;
        }

        private static AudioClip Clip(string fileName)
        {
            string path = $"{PackagePath}/Audio/{fileName}.wav";
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
                throw new InvalidOperationException("O áudio " + path + " não foi encontrado. Copie a pasta Audio do ZIP.");
            return clip;
        }

        private static void AddBoundaries(Transform parent)
        {
            Transform root = RaizesSceneArt.Group("Limites da clareira", parent).transform;
            Boundary("Esquerda", new Vector3(-6.54f, 1.20f, 2.50f), new Vector3(0.15f, 2.40f, 13.1f));
            Boundary("Direita", new Vector3(6.54f, 1.20f, 2.50f), new Vector3(0.15f, 2.40f, 13.1f));
            Boundary("Entrada", new Vector3(0f, 1.20f, -4.04f), new Vector3(13.1f, 2.40f, 0.15f));
            Boundary("Fundos", new Vector3(0f, 1.20f, 9.04f), new Vector3(13.1f, 2.40f, 0.15f));

            void Boundary(string name, Vector3 position, Vector3 size)
            {
                var wall = RaizesSceneArt.Group(name, root, position).AddComponent<BoxCollider>();
                wall.size = size;
            }
        }

        private static FieldTeleportFade CreateFade(Camera camera)
        {
            var screen = CreateCanvas("Fade de teletransporte", camera.transform,
                new Vector3(0f, 0f, Mathf.Max(0.24f, camera.nearClipPlane + 0.08f)), camera, 1200f, 1200f, 0.003f);
            screen.sortingOrder = 100;
            var image = CreateImage("Preto", screen.gameObject, Vector2.zero, new Vector2(1200f, 1200f), Color.clear);
            image.enabled = false;
            FieldTeleportFade fade = screen.gameObject.AddComponent<FieldTeleportFade>();
            Configure(fade, ("veil", image));
            return fade;
        }

        private static (GameObject Root, Text Body) CreateBoard(string name, Transform parent, Vector3 position,
            float width, float height, float scale, string title, string text, int textSize, Font font, Camera camera,
            bool faceView = true, bool upright = true, bool interactive = false)
        {
            Canvas canvas = CreateCanvas(name, parent, position, camera, width, height, scale);
            if (interactive)
                canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            if (faceView)
            {
                var facing = canvas.gameObject.AddComponent<FacePlayer>();
                Configure(facing, ("viewer", camera.transform), ("upright", upright));
            }
            var root = canvas.gameObject;
            CreateImage("Fundo", root, Vector2.zero, new Vector2(width, height), BoardBackground);
            CreateImage("Linha", root, new Vector2(0f, height * 0.5f - 2f), new Vector2(width, 4f), BoardAccent);
            AddText("Título", root, new Vector2(0f, height * 0.5f - 40f), new Vector2(width - 48f, 47f),
                title, 32, font, BoardAccent, FontStyle.Bold, TextAnchor.MiddleLeft);

            float bottom = interactive ? 77f : 17f;
            float textHeight = height - 69f - bottom;
            Text body = AddText("Texto", root, new Vector2(0f, -height * 0.5f + bottom + textHeight * 0.5f),
                new Vector2(width - 48f, textHeight), text, textSize, font, BoardText, FontStyle.Normal, TextAnchor.UpperLeft);
            return (root, body);
        }

        private static Canvas CreateCanvas(string name, Transform parent, Vector3 position, Camera camera,
            float width, float height, float scale)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localPosition = position;
            rect.localScale = Vector3.one * scale;
            rect.sizeDelta = new Vector2(width, height);
            return canvas;
        }

        private static Button AddButton(GameObject parent, string label, Font font, float y)
        {
            var pos = new Vector2(0f, y);
            var size = new Vector2(306f, 56f);
            Image image = CreateImage(label, parent, pos, size, BoardAccent);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            AddText("Rótulo", image.gameObject, Vector2.zero, size, label, 26, font,
                new Color32(28, 36, 31, 255), FontStyle.Bold, TextAnchor.MiddleCenter);
            return button;
        }

        private static Image CreateImage(string name, GameObject root, Vector2 position, Vector2 size, Color color)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Image));
            Place(child.GetComponent<RectTransform>(), root, position, size);
            var image = child.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text AddText(string name, GameObject root, Vector2 position, Vector2 size, string value,
            int fontSize, Font font, Color color, FontStyle style, TextAnchor anchor)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Text));
            Place(child.GetComponent<RectTransform>(), root, position, size);
            var text = child.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.alignment = anchor;
            text.color = color;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.lineSpacing = 1.1f;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void Place(RectTransform rect, GameObject root, Vector2 position, Vector2 size)
        {
            rect.SetParent(root.transform, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static bool TryGetRig(Scene scene, out Rig rig)
        {
            XROrigin[] origins = RaizesPlayerSetup.FindEnabledOrigins(scene);
            XROrigin origin = origins.Length == 1 ? origins[0] : null;
            rig = new Rig
            {
                Origin = origin,
                SampleGround = FindInScene<TeleportationArea>(scene),
                Teleport = FindInScene<TeleportationProvider>(scene),
                Events = FindInScene<EventSystem>(scene),
                Interactions = FindInScene<XRInteractionManager>(scene)
            };
            if (origin == null || rig.SampleGround == null ||
                rig.Teleport == null || rig.Events == null || rig.Interactions == null ||
                rig.Events.GetComponent<XRUIInputModule>() == null)
                return false;
            rig.Left = FindHand(origin, "Left");
            rig.Right = FindHand(origin, "Right");
            return rig.Left != null && rig.Right != null;
        }

        private static Transform FindHand(XROrigin origin, string side)
        {
            Transform[] children = origin.GetComponentsInChildren<Transform>(true);
            foreach (string name in new[] { side + "Controller", side + "HandController", side + "ControllerAnchor", side + "Hand" })
            {
                Transform hand = children.FirstOrDefault(child => child.gameObject.activeInHierarchy &&
                    string.Equals(child.name.Replace(" ", "").Replace("-", ""), name, StringComparison.OrdinalIgnoreCase));
                if (hand != null)
                    return hand;
            }

            var interactor = origin.GetComponentsInChildren<XRBaseInputInteractor>(true).FirstOrDefault(candidate =>
                candidate.gameObject.activeInHierarchy && candidate.handedness.ToString() == side);
            if (interactor == null)
                return null;
            var pose = interactor.GetComponentInParent<TrackedPoseDriver>();
            return pose != null ? pose.transform : interactor.transform;
        }

        private static T FindInScene<T>(Scene scene) where T : Component =>
            ComponentsInScene<T>(scene).FirstOrDefault(component => component.gameObject.activeInHierarchy);

        private static IEnumerable<T> ComponentsInScene<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static void Configure(Object component, params (string Name, object Value)[] settings)
        {
            var serialized = new SerializedObject(component);
            foreach (var (name, value) in settings)
            {
                SerializedProperty property = serialized.FindProperty(name)
                    ?? throw new InvalidOperationException($"Campo {name} ausente em {component.GetType().Name}.");
                switch (value)
                {
                    case Object[] references:
                        property.arraySize = references.Length;
                        for (int index = 0; index < references.Length; index++)
                            property.GetArrayElementAtIndex(index).objectReferenceValue = references[index];
                        break;
                    case Object reference:
                        property.objectReferenceValue = reference;
                        break;
                    case string label:
                        property.stringValue = label;
                        break;
                    case bool flag:
                        property.boolValue = flag;
                        break;
                    case float number:
                        property.floatValue = number;
                        break;
                    case FieldTool.Kind kind:
                        property.enumValueIndex = (int)kind;
                        break;
                    default:
                        throw new ArgumentException($"Tipo de valor inesperado para {name}.");
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
