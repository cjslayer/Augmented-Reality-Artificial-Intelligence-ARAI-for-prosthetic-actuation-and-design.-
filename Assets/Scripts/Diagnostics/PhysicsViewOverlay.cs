#if UNITY_EDITOR
// Physics view (Editor only, compiled out of players): draws the articulation's real colliders (finger / thumb capsules, palm
// box, forearm and bicep capsules) and the object's collider as semi-transparent primitives parented to the physics links,
// so the physically real hand is visible despite the skinned mesh's follow lag (up to 42 mm on the pinky). Toggle with F2 in
// Play mode, or create Temp/physview to start with it on. Rebuilt automatically when the skeleton is regenerated (each episode).
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PhysicsViewOverlay : MonoBehaviour
{
    public bool visible;
    readonly List<GameObject> drawn = new List<GameObject>();
    Material matHand, matObj; int rebuildSeen = -1; ArmGraspAgent agent;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (GameObject.Find("PhysicsViewOverlay (runtime only)") != null) return;
        var go = new GameObject("PhysicsViewOverlay (runtime only)"); var p = go.AddComponent<PhysicsViewOverlay>();
        p.visible = File.Exists("Temp/physview");
    }

    static Material MakeMat(Color c)
    {
        var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
        var m = new Material(sh) { color = c }; m.renderQueue = 3100; return m;
    }

    void Start()
    {
        foreach (var a in FindObjectsByType<ArmGraspAgent>(FindObjectsSortMode.None)) if (a.isActiveAndEnabled) { agent = a; break; }
        matHand = MakeMat(new Color(0.2f, 0.9f, 1f, 0.45f)); matObj = MakeMat(new Color(1f, 0.85f, 0.2f, 0.35f));
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2)) visible = !visible;
        if (agent == null || agent.Hand == null || !agent.Hand.Built) return;
        bool stale = agent.Hand.RebuildCount != rebuildSeen;
        if (visible && (stale || drawn.Count == 0)) Build();
        if (!visible && drawn.Count > 0) Clear();
    }

    void Clear() { foreach (var g in drawn) if (g != null) Destroy(g); drawn.Clear(); }

    void Build()
    {
        Clear(); var hand = agent.Hand; rebuildSeen = hand.RebuildCount;
        var bodies = new List<ArticulationBody> { hand.Bicep, hand.Forearm, hand.Palm }; bodies.AddRange(hand.Groups); foreach (var (eb, _) in hand.ExtraLinks) bodies.Add(eb);
        foreach (var b in bodies)
        {
            if (b == null) continue;
            var cap = b.GetComponent<CapsuleCollider>(); var box = b.GetComponent<BoxCollider>();
            if (cap != null)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Capsule); Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(b.transform, false); g.transform.localPosition = cap.center;
                g.transform.localRotation = cap.direction == 0 ? Quaternion.Euler(0f, 0f, 90f) : cap.direction == 2 ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
                g.transform.localScale = new Vector3(2f * cap.radius, 0.5f * Mathf.Max(cap.height, 2f * cap.radius), 2f * cap.radius);   // unit capsule: radius 0.5, height 2
                g.GetComponent<Renderer>().sharedMaterial = matHand; g.name = "physview_" + b.name; drawn.Add(g);
            }
            if (box != null)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(b.transform, false); g.transform.localPosition = box.center; g.transform.localRotation = Quaternion.identity; g.transform.localScale = box.size;
                g.GetComponent<Renderer>().sharedMaterial = matHand; g.name = "physview_" + b.name; drawn.Add(g);
            }
        }
        var cyl = GameObject.FindGameObjectWithTag("Cylinder");
        if (cyl != null)
        {   // the object is a unit cylinder mesh scaled by its transform: a cylinder primitive with the same local scale coincides with its convex collider
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(cyl.transform, false); g.transform.localPosition = Vector3.zero; g.transform.localRotation = Quaternion.identity; g.transform.localScale = new Vector3(1.02f, 1.02f, 1.02f);
            g.GetComponent<Renderer>().sharedMaterial = matObj; g.name = "physview_object"; drawn.Add(g);
        }
    }

    void OnGUI()
    {
        if (!visible) return;
        GUI.Label(new Rect(8f, Screen.height - 24f, 600f, 20f), "physics view ON (F2): cyan = articulation colliders (capsules / palm box), yellow = object collider");
    }
}
#endif
