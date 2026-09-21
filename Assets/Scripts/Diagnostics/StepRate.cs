#if UNITY_EDITOR
// Throughput probe (Editor only): when Temp/steprate.json exists at scene load, drives the active agent with random
// heuristic actions, runs physics as fast as the machine allows (timeScale 100, maximumDeltaTime 10) and logs agent
// steps per wall-clock second; writes the result to the path given in the json, then stops Play mode.
using System.IO;
using UnityEngine;
using Unity.MLAgents.Policies;
public class StepRate : MonoBehaviour
{
    [System.Serializable] public class Config { public string output = "Temp/steprate.txt"; public float seconds = 20f; public float actionScale = 0.5f; }
    Config cfg; ArmGraspAgent agent; System.Diagnostics.Stopwatch sw; int steps0, decisions; float warm = 2f; bool started;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!File.Exists("Temp/steprate.json")) return;
        var c = JsonUtility.FromJson<Config>(File.ReadAllText("Temp/steprate.json"));
        foreach (var a in FindObjectsByType<ArmGraspAgent>(FindObjectsSortMode.None)) if (a.isActiveAndEnabled) { var s = a.gameObject.AddComponent<StepRate>(); s.cfg = c; s.agent = a; break; }
    }
    void Start()
    {
        agent.GetComponent<BehaviorParameters>().BehaviorType = BehaviorType.HeuristicOnly;
        Time.timeScale = 100f; Time.maximumDeltaTime = 10f; Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
        sw = new System.Diagnostics.Stopwatch();
    }
    int total;
    void FixedUpdate()
    {
        total++;
        var acts = agent.heuristicActions; for (int i = 0; i < acts.Length; i++) acts[i] = Random.Range(-cfg.actionScale, cfg.actionScale);
        if (!started) { if (Time.realtimeSinceStartup > warm) { started = true; sw.Start(); steps0 = total; } return; }
        if (sw.Elapsed.TotalSeconds >= cfg.seconds)
        {
            float rate = (total - steps0) / (float)sw.Elapsed.TotalSeconds;
            string msg = "[StepRate] " + (total - steps0) + " agent steps in " + sw.Elapsed.TotalSeconds.ToString("F1") + " s = " + rate.ToString("F0") + " steps/s (fixedDeltaTime " + Time.fixedDeltaTime + ", decisionPeriod " + agent.GetComponent<Unity.MLAgents.DecisionRequester>().DecisionPeriod + ", episodes " + agent.CompletedEpisodes + ")";
            Debug.Log(msg); File.WriteAllText(cfg.output, msg + "\n");
            Time.timeScale = 1f; Time.maximumDeltaTime = 0.3333f; enabled = false; UnityEditor.EditorApplication.isPlaying = false;
        }
    }
}
#endif
