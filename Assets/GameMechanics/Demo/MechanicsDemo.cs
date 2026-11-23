using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MedievalCastle.Mechanics
{
    public sealed class MechanicsDemo : MonoBehaviour
    {
        private readonly List<Material> materials = new List<Material>();
        private Material stone;
        private Material darkStone;
        private Material brass;
        private Material wood;
        private Material path;

        private void Awake()
        {
            RenderSettings.ambientLight = new Color(.58f, .60f, .60f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.48f, .62f, .66f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 32;
            RenderSettings.fogEndDistance = 66;

            var sunlight = Create("Sunlight", Vector3.zero).AddComponent<Light>();
            sunlight.type = LightType.Directional;
            sunlight.color = new Color(1, .91f, .77f);
            sunlight.intensity = 1.15f;
            sunlight.shadows = LightShadows.Soft;
            sunlight.transform.rotation = Quaternion.Euler(48, -37, 0);

            stone = MaterialFor(new Color32(129, 134, 127, 255));
            darkStone = MaterialFor(new Color32(70, 81, 79, 255));
            brass = MaterialFor(new Color32(211, 172, 90, 255));
            wood = MaterialFor(new Color32(88, 60, 42, 255));
            path = MaterialFor(new Color32(150, 134, 103, 255));
            var lawn = MaterialFor(new Color32(83, 101, 67, 255));
            var teal = MaterialFor(new Color32(40, 100, 96, 255));
            var rust = MaterialFor(new Color32(148, 67, 47, 255));
            var blue = MaterialFor(new Color32(66, 85, 145, 255));

            Block("Grass", new Vector3(0, -.3f, 11), new Vector3(54, .6f, 52), lawn);
            Block("Gate path", new Vector3(0, .012f, 8), new Vector3(4.4f, .024f, 23), path, false);
            Block("Courtyard", new Vector3(0, .015f, 18.5f), new Vector3(18, .03f, 14), path, false);

            Wall(-6.5f, 10, 9);
            Wall(6.5f, 10, 9);
            Wall(0, 28, 22);
            Tower(-10.8f, 10);
            Tower(10.8f, 10);
            Tower(-10.8f, 28);
            Tower(10.8f, 28);
            Block("West wall", new Vector3(-10.8f, 2.4f, 19), new Vector3(1.1f, 4.8f, 18), stone);
            Block("East wall", new Vector3(10.8f, 2.4f, 19), new Vector3(1.1f, 4.8f, 18), stone);
            Block("Gate lintel", new Vector3(0, 4.6f, 10), new Vector3(4.4f, .85f, 1.6f), darkStone);

            GameObject gateBody = Block("Castle gate", new Vector3(0, 2.1f, 10), new Vector3(4.05f, 4.2f, .43f), wood);
            GateView gate = gateBody.AddComponent<GateView>();
            for (int index = -3; index <= 3; index++)
            {
                GameObject strip = Block("Gate iron", new Vector3(index * .55f, 2.1f, 9.76f),
                    new Vector3(.07f, 4.15f, .07f), darkStone, false);
                strip.transform.SetParent(gateBody.transform, true);
            }
            Sign("CASTLE KEEP", new Vector3(0, 4.68f, 9.13f), 3.75f, darkStone);

            Block("Merchant counter", new Vector3(-4.3f, .46f, 4.25f), new Vector3(2.4f, .92f, .65f), wood);
            Block("Merchant canopy", new Vector3(-4.3f, 2.65f, 4.8f), new Vector3(3.25f, .18f, 2.05f), teal, false);
            foreach (float x in new[] { -5.7f, -2.9f })
                Block("Stall post", new Vector3(x, 1.35f, 5.55f), new Vector3(.14f, 2.7f, .14f), wood);
            Sign("MERCHANT", new Vector3(-4.3f, 2.64f, 3.73f), 2.3f, darkStone);
            Sign("GUARD", new Vector3(2.8f, 2.39f, 7.25f), 1.45f, darkStone);
            Sign("KNIGHT", new Vector3(0, 2.42f, 15.2f), 1.55f, darkStone);

            var actors = new[]
            {
                Actor("merchant_01", "Merchant", NpcRole.Merchant, new Vector3(-4.3f, 0, 4.8f), teal),
                Actor("guard_01", "Gate Guard", NpcRole.Guard, new Vector3(2.8f, 0, 7.5f), rust),
                Actor("knight_01", "Training Knight", NpcRole.Knight, new Vector3(0, 0, 15.45f), blue)
            };

            var stations = new[]
            {
                Station(1, new Vector3(-5.5f, 0, 17.7f), teal),
                Station(2, new Vector3(0, 0, 23), teal),
                Station(3, new Vector3(5.5f, 0, 17.7f), teal)
            };
            var player = Create("Player", new Vector3(0, .02f, 0)).AddComponent<CastlePlayer>();
            var body = player.GetComponent<CharacterController>();
            body.center = new Vector3(0, .93f, 0);
            body.height = 1.85f;
            body.radius = .32f;

            GameObject cameraObject = Create("Player view", Vector3.zero);
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 1.65f, 0);
            var view = cameraObject.AddComponent<Camera>();
            view.fieldOfView = 68;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(.52f, .68f, .72f);
            view.farClipPlane = 85;
            cameraObject.AddComponent<AudioListener>();
            player.view = view;

            var interaction = gameObject.AddComponent<CastleInteraction>();
            interaction.player = player;
            interaction.actors = actors;
            interaction.gate = gate;
            interaction.trainingPoints = stations;
            interaction.decisionProvider = gameObject.AddComponent<LocalNpcDecisions>();
        }

        private NpcActor Actor(string id, string title, NpcRole role, Vector3 position, Material coat)
        {
            var actor = Create(title, position).AddComponent<NpcActor>();
            actor.npcId = id;
            actor.displayName = title;
            actor.role = role;
            var torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            torso.name = "Coat";
            torso.transform.SetParent(actor.transform, false);
            torso.transform.localPosition = new Vector3(0, .87f, 0);
            torso.transform.localScale = new Vector3(.76f, .82f, .55f);
            torso.GetComponent<Renderer>().sharedMaterial = coat;
            Block("Belt", position + Vector3.up * .76f, new Vector3(.65f, .11f, .55f), wood, false);
            Block("Hood", position + Vector3.up * 1.76f, new Vector3(.44f, .43f, .43f), darkStone, false);
            Block("Face", position + new Vector3(0, 1.72f, -.21f), new Vector3(.29f, .25f, .05f), brass, false);
            return actor;
        }

        private Transform Station(int number, Vector3 position, Material color)
        {
            var station = Create($"Training station {number}", position);
            Block("Station pad", position + Vector3.up * .04f, new Vector3(2, .08f, 2), color, false);
            Block("Station post", position + new Vector3(0, .68f, .79f), new Vector3(.17f, 1.35f, .18f), wood, false);
            Sign($"STATION {number}", position + new Vector3(0, 1.45f, .76f), 1.55f, darkStone);
            return station.transform;
        }

        private void Wall(float x, float z, float width)
        {
            Block("Curtain wall", new Vector3(x, 2.45f, z), new Vector3(width, 4.9f, 1.1f), stone);
            Block("Wall coping", new Vector3(x, 4.83f, z), new Vector3(width + .08f, .24f, 1.26f), darkStone, false);
            for (float offset = -width / 2 + .45f; offset < width / 2; offset += 1.24f)
                Block("Battlement", new Vector3(x + offset, 5.26f, z), new Vector3(.66f, .75f, 1.05f), stone, false);
        }

        private void Tower(float x, float z)
        {
            Block("Tower", new Vector3(x, 3.3f, z), new Vector3(2.65f, 6.6f, 2.65f), darkStone);
            Block("Tower crown", new Vector3(x, 6.28f, z), new Vector3(2.94f, .68f, 2.94f), stone, false);
            foreach (float offset in new[] { -1.03f, 0, 1.03f })
                Block("Tower parapet", new Vector3(x + offset, 6.92f, z - 1.19f), new Vector3(.58f, .74f, .58f), stone, false);
        }

        private void Sign(string text, Vector3 position, float width, Material color)
        {
            Block(text + " board", position, new Vector3(width, .43f, .11f), color, false);
            TextMesh label = Create(text, position + Vector3.back * .059f).AddComponent<TextMesh>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<Renderer>().sharedMaterial = label.font.material;
            label.fontSize = 68;
            label.characterSize = .045f;
            label.anchor = TextAnchor.MiddleCenter;
            label.color = new Color(1, .90f, .65f);
        }

        private GameObject Create(string label, Vector3 at)
        {
            var item = new GameObject(label);
            item.transform.SetParent(transform, false);
            item.transform.position = at;
            return item;
        }

        private GameObject Block(string label, Vector3 at, Vector3 size, Material material, bool solid = true)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = label;
            item.transform.SetParent(transform, false);
            item.transform.position = at;
            item.transform.localScale = size;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) item.GetComponent<Collider>().enabled = false;
            return item;
        }

        private Material MaterialFor(Color color)
        {
            Shader shader = GraphicsSettings.currentRenderPipeline != null ? Shader.Find("Universal Render Pipeline/Lit") :
                Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            material.SetFloat("_Smoothness", .12f);
            materials.Add(material);
            return material;
        }

        private void OnDestroy()
        {
            foreach (Material material in materials) Destroy(material);
        }
    }
}
