#if UNITY_EDITOR
// Checkpoint theater (Editor only, compiled out of players): watch the REAL training run inside the training scene.
// Trigger: Temp/theater.json exists at scene load, e.g. {"runId":"011","refreshMinutes":10,"behavior":"Prosthetic"}.
// On Play it switches every active ArmGraspAgent to InferenceOnly with per-episode theta resampling ON, imports the newest
// results/<runId>/<behavior>/<behavior>-<steps>.onnx checkpoint into Assets/Models/Watch/ (Editor ONNX importer), verifies
// that the model's input shapes match the scene's sensors (vector observation size and the JointTokens buffer), and hands
// it to the agents with Agent.SetModel (runtime hot-swap, no Play-mode exit). Every refreshMinutes it looks for a newer
// checkpoint and swaps again. The current perturbation lesson is read from results/<runId>/run_logs/training_status.json
// and applied through the agent's diagnostic perturbScaleOverride so the pulses match what the trainer is asking for.
// A model whose inputs do not match is refused: logged, previous model kept (the mismatched-model hang guard).
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using Unity.InferenceEngine;

public class CheckpointTheater : MonoBehaviour
{
    [System.Serializable]
    public class Config { public string runId = "011"; public float refreshMinutes = 10f; public string behavior = "Prosthetic"; public float[] lessonValues = { 0f, 0.25f, 0.5f, 0.75f, 1f }; }
    Config cfg; readonly List<ArmGraspAgent> agents = new List<ArmGraspAgent>();
    int loadedSteps = -1; string loadedPath = ""; float nextCheck; int lessonNum = -1; float lessonValue = -1f; string lastError = "";
    GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        const string path = "Temp/theater.json";
        if (!File.Exists(path)) return;
        Config c; try { c = JsonUtility.FromJson<Config>(File.ReadAllText(path)); } catch (System.Exception e) { Debug.LogError("[Theater] bad config: " + e.Message); return; }
        var go = new GameObject("CheckpointTheater (runtime only)"); var t = go.AddComponent<CheckpointTheater>(); t.cfg = c ?? new Config();
    }

    void Start()
    {
        foreach (var a in FindObjectsByType<ArmGraspAgent>(FindObjectsSortMode.None)) if (a.isActiveAndEnabled) agents.Add(a);
        foreach (var a in agents) { var mm = a.GetComponent<MorphologyManager>(); if (mm != null) mm.randomizeByDefault = true; }   // InferenceOnly is set only once a model has been handed over (an InferenceOnly agent without a model throws)
        TryRefresh(); nextCheck = Time.realtimeSinceStartup + cfg.refreshMinutes * 60f;
    }

    void Update()
    {
        if (Time.realtimeSinceStartup >= nextCheck) { TryRefresh(); nextCheck = Time.realtimeSinceStartup + cfg.refreshMinutes * 60f; }
        if (Input.GetKeyDown(KeyCode.F5)) TryRefresh();   // manual refresh
    }

    (string path, int steps) NewestCheckpoint()
    {
        string dir = Path.Combine("results", cfg.runId, cfg.behavior); if (!Directory.Exists(dir)) return (null, -1);
        string best = null; int bestSteps = -1; var rx = new Regex(Regex.Escape(cfg.behavior) + @"-(\d+)\.onnx$");
        foreach (var f in Directory.GetFiles(dir, "*.onnx")) { var m = rx.Match(Path.GetFileName(f)); if (m.Success) { int s = int.Parse(m.Groups[1].Value); if (s > bestSteps) { bestSteps = s; best = f; } } }
        return (best, bestSteps);
    }

    void ReadLesson()
    {
        try
        {
            string p = Path.Combine("results", cfg.runId, "run_logs", "training_status.json"); if (!File.Exists(p)) return;
            var m = Regex.Match(File.ReadAllText(p), "\"perturb/scale\"\\s*:\\s*\\{\\s*\"lesson_num\"\\s*:\\s*(\\d+)");
            if (m.Success) { lessonNum = int.Parse(m.Groups[1].Value); lessonValue = cfg.lessonValues != null && lessonNum < cfg.lessonValues.Length ? cfg.lessonValues[lessonNum] : 1f; foreach (var a in agents) a.perturbScaleOverride = lessonValue; }
        }
        catch (System.Exception e) { lastError = "lesson: " + e.Message; }
    }

    void TryRefresh()
    {
        ReadLesson();
        var (path, steps) = NewestCheckpoint();
        if (path == null) { lastError = "no checkpoint under results/" + cfg.runId + "/" + cfg.behavior; return; }
        if (steps <= loadedSteps) return;
        try
        {
            string assetDir = "Assets/Models/Watch"; Directory.CreateDirectory(assetDir);
            string assetPath = assetDir + "/" + cfg.runId + "_" + cfg.behavior + "_" + steps + ".onnx";
            File.Copy(path, assetPath, true);
            UnityEditor.AssetDatabase.ImportAsset(assetPath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ModelAsset>(assetPath);
            if (asset == null) { lastError = "import failed: " + assetPath; Debug.LogError("[Theater] " + lastError); return; }
            string why; if (!ModelMatchesScene(asset, out why)) { lastError = "REFUSED " + Path.GetFileName(path) + ": " + why; Debug.LogError("[Theater] " + lastError + " (keeping " + loadedSteps + ")"); return; }
            foreach (var a in agents) { a.SetModel(cfg.behavior, asset, InferenceDevice.Default); var bp = a.GetComponent<BehaviorParameters>(); if (bp != null) bp.BehaviorType = BehaviorType.InferenceOnly; }
            loadedSteps = steps; loadedPath = path; lastError = "";
            Debug.Log("[Theater] loaded checkpoint " + steps + " (" + path + ") lesson " + lessonNum + " (perturb scale " + lessonValue + ")");
        }
        catch (System.Exception e) { lastError = "swap failed: " + e.Message; Debug.LogError("[Theater] " + lastError); }
    }

    /// <summary>Guard: the model must have a vector-observation input whose last dimension equals the scene's VectorObservationSize and a buffer input matching the JointTokens sensor (max observables x observable size).</summary>
    bool ModelMatchesScene(ModelAsset asset, out string why)
    {
        why = "";
        var agent = agents.Count > 0 ? agents[0] : null; if (agent == null) { why = "no agent"; return false; }
        var bp = agent.GetComponent<BehaviorParameters>(); int vecSize = bp != null ? bp.BrainParameters.VectorObservationSize : -1;
        var buf = agent.GetComponent<BufferSensorComponent>(); int bufN = buf != null ? buf.MaxNumObservables : -1, bufD = buf != null ? buf.ObservableSize : -1;
        Model model; try { model = ModelLoader.Load(asset); } catch (System.Exception e) { why = "cannot parse model: " + e.Message; return false; }
        bool vecOk = false, bufOk = bufN < 0; var seen = new System.Text.StringBuilder();
        foreach (var inp in model.inputs)
        {
            var dims = new List<int>(); foreach (var tok in inp.shape.ToString().Trim('(', ')', '[', ']').Split(',')) { dims.Add(int.TryParse(tok.Trim(), out int v) ? v : -1); }   // dynamic dims (batch) parse as -1
            seen.Append(inp.name).Append('[').Append(string.Join(",", dims)).Append("] ");
            if (dims.Count == 2 && dims[1] == vecSize) vecOk = true;
            if (dims.Count == 3 && dims[1] == bufN && dims[2] == bufD) bufOk = true;
        }
        if (!vecOk || !bufOk) { why = "inputs " + seen + "do not match scene (vector " + vecSize + ", buffer " + bufN + "x" + bufD + ")"; return false; }
        return true;
    }

    void OnGUI()
    {
        if (style == null) { style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13, richText = true }; style.normal.textColor = Color.white; }
        string txt = "<b>Checkpoint theater</b>  run " + cfg.runId + "\ncheckpoint step: " + (loadedSteps >= 0 ? loadedSteps.ToString("N0") : "none") + "\ncurriculum lesson: " + (lessonNum >= 0 ? lessonNum + " (perturb scale " + lessonValue + ")" : "unknown") + "\nnext refresh in " + Mathf.Max(0f, (nextCheck - Time.realtimeSinceStartup) / 60f).ToString("F1") + " min (F5 = now)" + (lastError.Length > 0 ? "\n<color=#FF8080>" + lastError + "</color>" : "");
        var content = new GUIContent(txt); var size = style.CalcSize(content);
        GUI.Box(new Rect(Screen.width - size.x - 20f, 8f, size.x + 12f, size.y + 8f), content, style);
    }
}
#endif
