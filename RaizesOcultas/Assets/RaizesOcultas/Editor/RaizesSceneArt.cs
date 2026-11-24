using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RaizesOcultas.Editor
{
    internal sealed class RaizesSceneArt
    {
        private readonly string materialFolder;
        private readonly Shader surfaceShader;
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        public RaizesSceneArt(string materialFolder)
        {
            this.materialFolder = materialFolder;
            surfaceShader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (surfaceShader == null)
                throw new InvalidOperationException("O shader Simple Lit da URP não está disponível. Abra um projeto do template VR (URP).");
        }

        public static GameObject Group(string name, Transform parent, Vector3 position = default)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            group.transform.localPosition = position;
            return group;
        }

        public GameObject Part(string name, Transform parent, PrimitiveType type, Vector3 position,
            Vector3 scale, Material material, bool solid = false, Vector3 euler = default)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(euler);
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!solid)
                UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            return part;
        }

        public Material Material(string name, string hex, Shader shader = null)
        {
            if (materials.TryGetValue(name, out Material cached))
                return cached;

            string path = $"{materialFolder}/{name}.mat";
            Material asset = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (asset == null)
            {
                asset = new Material(shader == null ? surfaceShader : shader) { name = name };
                if (ColorUtility.TryParseHtmlString(hex, out Color color))
                    asset.SetColor("_BaseColor", color);
                if (asset.HasProperty("_Smoothness"))
                    asset.SetFloat("_Smoothness", 0.13f);
                AssetDatabase.CreateAsset(asset, path);
            }
            materials.Add(name, asset);
            return asset;
        }

        public void Light(Transform parent)
        {
            GameObject sun = Group("Sol", parent);
            sun.transform.localRotation = Quaternion.Euler(48f, -28f, 0f);
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.91f, 0.76f);
            light.intensity = 1.0f;
            light.shadows = LightShadows.Soft;
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.56f, 0.62f, 0.55f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.43f, 0.49f, 0.41f);
            RenderSettings.fogDensity = 0.025f;
        }

        public void Landscape(Transform parent)
        {
            Material wood = Material("Madeira", "#624732");
            Material leaves = Material("Folhas", "#36563B");
            Material lightLeaves = Material("FolhasClaras", "#647550");
            Material stone = Material("Pedra", "#6E6D59");
            var trees = Group("Mata", parent).transform;
            var ruins = Group("Ruínas", parent).transform;
            var random = new System.Random(31);

            for (int index = 0; index < 13; index++)
            {
                float side = index % 2 == 0 ? -1f : 1f;
                float x = side * (5.45f + (float)random.NextDouble() * 1.15f);
                float z = -3.0f + index * 0.91f;
                float h = 2.8f + (float)random.NextDouble() * 1.5f;
                Transform tree = Group($"Árvore {index + 1:00}", trees, new Vector3(x, 0f, z)).transform;
                Part("Tronco", tree, PrimitiveType.Cylinder, new Vector3(0f, h * 0.5f, 0f),
                    new Vector3(0.25f, h * 0.5f, 0.25f), wood, true);
                Part("Copa", tree, PrimitiveType.Sphere, new Vector3(0f, h, 0f),
                    new Vector3(2.05f, 1.45f, 1.73f), leaves);
                Part("Folhagem", tree, PrimitiveType.Sphere, new Vector3(-side * 0.54f, h - 0.38f, 0.26f),
                    new Vector3(1.21f, 0.92f, 1.32f), lightLeaves);
                Part("Raiz", tree, PrimitiveType.Cube, new Vector3(-side * 0.24f, 0.06f, 0.03f),
                    new Vector3(0.64f, 0.12f, 0.13f), wood, false, new Vector3(0f, 23f, side * 8f));
            }

            for (int index = 0; index < 8; index++)
            {
                float x = -4.8f + index * 0.55f;
                float h = 0.24f + (float)random.NextDouble() * 0.22f;
                Part($"Bloco {index + 1:00}", ruins, PrimitiveType.Cube, new Vector3(x, h * 0.5f, 7.5f),
                    new Vector3(0.52f, h, 0.33f), stone, true,
                    new Vector3(0f, (float)random.NextDouble() * 13f - 6f, 0f));
            }

            Transform camp = Group("Fogueira apagada", parent, new Vector3(4.3f, 0f, 5.15f)).transform;
            for (int index = 0; index < 7; index++)
            {
                float angle = index * Mathf.PI * 2f / 7f;
                Part("Pedra", camp, PrimitiveType.Sphere,
                    new Vector3(Mathf.Cos(angle) * 0.42f, 0.08f, Mathf.Sin(angle) * 0.42f),
                    new Vector3(0.27f, 0.18f, 0.20f), stone);
            }
            Part("Lenha", camp, PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f),
                new Vector3(0.54f, 0.10f, 0.10f), wood, false, new Vector3(0f, 33f, 0f));
        }

        public void Bench(Transform parent)
        {
            Material plank = Material("Tabua", "#926748");
            Material wood = Material("Madeira", "#624732");
            Part("Tampo", parent, PrimitiveType.Cube, new Vector3(0f, 0.86f, 0f),
                new Vector3(1.6f, 0.12f, 0.78f), plank, true);
            foreach (float x in new[] { -0.63f, 0.63f })
            foreach (float z in new[] { -0.29f, 0.29f })
                Part("Pé", parent, PrimitiveType.Cube, new Vector3(x, 0.42f, z),
                    new Vector3(0.105f, 0.84f, 0.105f), wood, true);
        }

        public void ToolModel(Transform parent, FieldTool.Kind kind)
        {
            Material wood = Material("Tabua", "#926748");
            Material metal = Material("Metal", "#687B80");
            if (kind == FieldTool.Kind.Shovel)
            {
                Part("Cabo", parent, PrimitiveType.Cylinder, Vector3.zero,
                    new Vector3(0.037f, 0.19f, 0.037f), wood);
                Part("Lâmina", parent, PrimitiveType.Cube, new Vector3(0f, -0.248f, 0f),
                    new Vector3(0.155f, 0.145f, 0.028f), metal);
                Part("Faixa", parent, PrimitiveType.Cylinder, new Vector3(0f, 0.145f, 0f),
                    new Vector3(0.044f, 0.039f, 0.044f), Material("MarcacaoPa", "#D39A4E"));
            }
            else
            {
                Part("Cabo", parent, PrimitiveType.Cylinder, new Vector3(0f, 0.042f, 0f),
                    new Vector3(0.033f, 0.12f, 0.033f), wood);
                Part("Virola", parent, PrimitiveType.Cube, new Vector3(0f, -0.094f, 0f),
                    new Vector3(0.074f, 0.051f, 0.037f), metal);
                Part("Cerdas", parent, PrimitiveType.Cube, new Vector3(0f, -0.145f, 0f),
                    new Vector3(0.085f, 0.063f, 0.035f), Material("Cerdas", "#D8C08F"));
            }
        }

        public void Glove(Transform hand)
        {
            Transform glove = Group("Luva de campo", hand, new Vector3(0f, -0.022f, -0.082f)).transform;
            Part("Palma", glove, PrimitiveType.Sphere, new Vector3(0f, 0f, 0.026f),
                new Vector3(0.092f, 0.049f, 0.102f), Material("Luva", "#425653"));
            Part("Punho", glove, PrimitiveType.Cube, new Vector3(0f, 0f, -0.032f),
                new Vector3(0.072f, 0.045f, 0.046f), Material("Punho", "#867C60"));
        }

        public void PlotFrame(Transform parent)
        {
            Material wood = Material("Tabua", "#926748");
            Part("Base compactada", parent, PrimitiveType.Cube, new Vector3(0f, 0.18f, 0f),
                new Vector3(1.2f, 0.36f, 0.85f), Material("TerraCompacta", "#73513C"), true);

            foreach (float x in new[] { -0.63f, 0.63f })
                Part("Parte lateral", parent, PrimitiveType.Cube, new Vector3(x, 0.175f, 0f),
                    new Vector3(0.07f, 0.35f, 0.99f), wood);
            foreach (float z in new[] { -0.46f, 0.46f })
                Part("Travessa", parent, PrimitiveType.Cube, new Vector3(0f, 0.175f, z),
                    new Vector3(1.22f, 0.35f, 0.07f), wood);
        }

        public void ArtifactModel(Transform parent, int art)
        {
            switch (art)
            {
                case 0:
                    Material clay = Material("Ceramica", "#AD593C");
                    Part("Fragmento central", parent, PrimitiveType.Cube, Vector3.zero,
                        new Vector3(0.115f, 0.078f, 0.144f), clay, false, new Vector3(0f, -12f, 0f));
                    Part("Borda curva", parent, PrimitiveType.Cube, new Vector3(0.070f, -0.006f, 0.034f),
                        new Vector3(0.118f, 0.06f, 0.098f), clay, false, new Vector3(0f, -36f, -13f));
                    Part("Faixa gravada", parent, PrimitiveType.Cube, new Vector3(-0.024f, 0.040f, 0.038f),
                        new Vector3(0.063f, 0.008f, 0.012f), Material("ArgilaClara", "#E3A677"));
                    break;
                case 1:
                    Material bead = Material("Conta", "#5A897F");
                    for (int index = 0; index < 10; index++)
                    {
                        float angle = index * Mathf.PI * 2f / 10f;
                        Part("Segmento do anel", parent, PrimitiveType.Sphere,
                            new Vector3(Mathf.Cos(angle) * 0.067f, 0f, Mathf.Sin(angle) * 0.067f),
                            new Vector3(0.070f, 0.067f, 0.070f), bead);
                    }
                    break;
                default:
                    Part("Corpo", parent, PrimitiveType.Sphere, Vector3.zero,
                        new Vector3(0.255f, 0.116f, 0.172f), Material("PedraMoagem", "#797B70"));
                    Part("Superfície lisa", parent, PrimitiveType.Sphere, new Vector3(0f, 0.046f, 0f),
                        new Vector3(0.145f, 0.024f, 0.107f), Material("PedraPolida", "#9A9A81"));
                    break;
            }
        }
    }
}
