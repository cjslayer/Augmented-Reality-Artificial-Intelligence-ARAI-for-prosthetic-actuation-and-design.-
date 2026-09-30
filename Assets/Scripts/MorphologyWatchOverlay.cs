#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Editor-only watch overlay (uncommitted, 2026-09-20). Spawns itself when a scene loads in Play mode and draws one small
/// panel per active ArmGraspAgent: episode number, the five finger length scales, active-group count and the masked groups,
/// mean finger omega, and live palm / fingertip contact flags. Reads public state only; contact flags for the fingertips are
/// recomputed here with the agent's own acceptance test (ComputePenetration or surface gap <= contactDistance) so the agent
/// is not touched. Compiled out of every player build by the UNITY_EDITOR guard. Delete this file to remove the overlay.
/// </summary>
public class MorphologyWatchOverlay : MonoBehaviour
{
    // run 014: one tip per digit slot (design-doc slot order: primary thumb, index, middle, ring, pinky, extra finger, second thumb); the tip is the distal link of the
    // digit, read from the agent's ArticulatedHand (the rebuilt skeleton is not parented under the agent, so the old tag + IsChildOf lookup never found it)
    static readonly string[] k_TipLabels = { "thb", "idx", "mid", "ring", "pnk", "ext", "th2" };

    class Watched
    {
        public ArmGraspAgent agent; public MorphologyManager morph; public Collider[] tips = new Collider[HandLayout.MaxDigits];
        public Transform cylinder; public Collider cylinderCollider; public string name;
    }
    readonly List<Watched> m_Watched = new List<Watched>();
    GUIStyle m_Style;
    readonly StringBuilder m_Sb = new StringBuilder(512);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        if (GameObject.Find("MorphologyWatchOverlay (runtime only)") != null) return;
        // plain play-mode object: no DontSave flag (a DontSave object survives Play-mode exit and would shadow the next spawn)
        var go = new GameObject("MorphologyWatchOverlay (runtime only)");
        go.AddComponent<MorphologyWatchOverlay>();
    }

    void Start()
    {
        foreach (var agent in FindObjectsByType<ArmGraspAgent>(FindObjectsSortMode.None))
        {
            if (!agent.isActiveAndEnabled) continue;
            var w = new Watched { agent = agent, morph = agent.GetComponent<MorphologyManager>(), name = agent.gameObject.name };
            // the agent's cylinder: FindGameObjectWithTag("Cylinder") in ArmGraspAgent.Initialize; mirror it, nearest to this agent if several
            float best = float.MaxValue;
            foreach (var c in GameObject.FindGameObjectsWithTag("Cylinder"))
            {
                float d = Vector3.Distance(c.transform.position, agent.transform.position);
                if (d < best) { best = d; w.cylinder = c.transform; w.cylinderCollider = c.GetComponent<Collider>(); }
            }
            m_Watched.Add(w);
        }
    }

    void RefreshTips(Watched w)
    {   // the articulated skeleton is rebuilt every episode: take each digit's distal link from the agent's hand (null when the digit is absent)
        var h = w.agent.Hand;
        for (int k = 0; k < HandLayout.MaxDigits; k++)
        {
            int d = HandLayout.SlotDigit[k], g = HandLayout.FirstGroupOfDigit(d) + HandLayout.GroupsOfDigit(d) - 1;
            w.tips[k] = h != null && h.Present(g) ? h.Groups[g].GetComponent<Collider>() : null;
        }
    }
    bool TipTouching(Watched w, Collider col)
    {
        if (col == null || w.cylinderCollider == null) return false;
        if (Physics.ComputePenetration(col, col.transform.position, col.transform.rotation,
                w.cylinderCollider, w.cylinder.position, w.cylinder.rotation, out _, out _)) return true;
        Vector3 p0 = w.cylinderCollider.ClosestPoint(col.transform.position);
        Vector3 onSegment = col.ClosestPoint(p0);
        return Vector3.Distance(p0, onSegment) <= w.agent.contactDistance;
    }

    void OnGUI()
    {
        if (m_Style == null)
        {
            m_Style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, richText = true, wordWrap = false };
            m_Style.normal.textColor = Color.white;
        }
        float y = 8f;
        foreach (var w in m_Watched)
        {
            var m = w.morph; var a = w.agent;
            m_Sb.Length = 0;
            m_Sb.Append("<b>").Append(w.name).Append("</b>   episode ").Append(a.CompletedEpisodes + 1)
                .Append("   step ").Append(a.StepCount).Append("   successes ").Append(a.SuccessCount).Append('\n');
            if (m == null) { m_Sb.Append("no MorphologyManager"); }
            else
            {
                m_Sb.Append("randomize ").Append(m.Randomizing ? "<color=#7CFC00>ON</color>" : "<color=#FF6060>OFF</color>").Append("   ");
                m_Sb.Append(HandLayout.Slots ? "digits " + m.DigitCount + " (" + HandLayout.SlotString(m.exists) + ")  len" : "len");
                for (int k = 0; k < HandLayout.MaxDigits; k++) { int d = HandLayout.SlotDigit[k]; if (m.exists[d]) m_Sb.Append("  ").Append(k_TipLabels[k]).Append(' ').Append(m.lengthScale[d].ToString("F2")); }
                m_Sb.Append('\n');
                int active = 0, present = 0; float wSum = 0f, kSum = 0f;
                for (int g = 0; g < HandLayout.MaxFingerGroups; g++) { if (!m.GroupPresent(g)) continue; present++; int mg = HandLayout.MorphIndex(g); if (m.mask[g]) active++; wSum += m.NaturalFrequency(mg); kSum += m.stiffness[mg]; }
                m_Sb.Append("active groups ").Append(active).Append('/').Append(present).Append("   masked: ");
                bool any = false;
                for (int g = 0; g < HandLayout.MaxFingerGroups; g++)
                    if (m.GroupPresent(g) && !m.mask[g]) { if (any) m_Sb.Append(", "); m_Sb.Append(HandLayout.GroupNames[g]); any = true; }
                if (!any) m_Sb.Append("none");
                m_Sb.Append('\n');
                m_Sb.Append("mean finger k ").Append((kSum / Mathf.Max(1, present)).ToString("F2")).Append(" Nm/rad (derived omega ").Append((wSum / Mathf.Max(1, present)).ToString("F0")).Append(" rad/s)")
                    .Append("   wrist omega ").Append(m.NaturalFrequency(MorphologyManager.FingerGroupCount).ToString("F1"))
                    .Append(" / ").Append(m.NaturalFrequency(MorphologyManager.FingerGroupCount + 1).ToString("F1")).Append('\n');
            }
            RefreshTips(w);
            m_Sb.Append("palm ").Append(a.LastPalmTouching ? "<color=#7CFC00>TOUCH</color>" : "<color=#808080>----</color>").Append("   tips ");
            for (int i = 0; i < HandLayout.MaxDigits; i++)
            {
                if (w.tips[i] == null) continue;
                bool t = TipTouching(w, w.tips[i]);
                m_Sb.Append(t ? "<color=#7CFC00>" : "<color=#808080>").Append(k_TipLabels[i]).Append("</color> ");
            }
            m_Sb.Append("  contacts ").Append(a.CurrentContacts).Append('/').Append(w.morph != null ? w.morph.PresentGroupCount : 14);
            var content = new GUIContent(m_Sb.ToString());
            Vector2 size = m_Style.CalcSize(content);
            GUI.Box(new Rect(8f, y, size.x + 12f, size.y + 8f), content, m_Style);
            y += size.y + 14f;
        }
    }
}
#endif
