// Ships the object bank with standalone players (run 013 prep, 2026-09-27). The bank is not embedded as assets: after every standalone build the
// bank files (bank.csv, BANK_SEED.txt, obj_NNN.obj, stable_poses.json) are copied from Assets/Objects/Bank into <Product>_Data/StreamingAssets/ObjectBank
// next to the player, which is where ObjectBank reads them outside the Editor (Application.streamingAssetsPath). Nothing is written under Assets/.
// A player without these files logs a FATAL line and quits at startup (ObjectBank.Awake).
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class ObjectBankBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        var target = report.summary.platform;
        if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneLinux64 && target != BuildTarget.StandaloneOSX) return;
        string exe = report.summary.outputPath; string dir = Path.GetDirectoryName(exe);
        string dataDir = target == BuildTarget.StandaloneOSX ? Path.Combine(exe, "Contents", "Resources", "Data") : Path.Combine(dir, Path.GetFileNameWithoutExtension(exe) + "_Data");
        string src = Path.Combine(Application.dataPath, ObjectBank.BankDir), dst = Path.Combine(dataDir, "StreamingAssets", ObjectBank.PlayerBankDir);
        if (!Directory.Exists(src)) { Debug.LogError("[ObjectBankBuild] bank source missing: " + src); return; }
        Directory.CreateDirectory(dst); int n = 0;
        foreach (var f in Directory.GetFiles(src))
        {
            string name = Path.GetFileName(f);
            bool ship = name == "bank.csv" || name == "BANK_SEED.txt" || name == "stable_poses.json" || (name.StartsWith("obj_") && name.EndsWith(".obj"));
            if (!ship) continue;
            File.Copy(f, Path.Combine(dst, name), true); n++;
        }
        Debug.Log("[ObjectBankBuild] copied " + n + " bank files to " + dst);
    }
}
