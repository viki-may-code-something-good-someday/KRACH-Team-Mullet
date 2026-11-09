using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class BuildPostProcessor : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        // Path to the source file (lives in the project root, next to the Assets folder)
        string sourcePath = Path.Combine(Application.dataPath, "..", "steam_appid.txt");
        sourcePath = Path.GetFullPath(sourcePath);

        if (!File.Exists(sourcePath))
        {
            Debug.LogWarning($"[BuildPostProcessor] steam_appid.txt not found at: {sourcePath}\n" +
                             "Please put the file in the project root (next to the Assets folder).");
            return;
        }

        // Target folder = the folder the .exe is in
        string buildFolder = Path.GetDirectoryName(report.summary.outputPath);
        string destPath = Path.Combine(buildFolder, "steam_appid.txt");

        File.Copy(sourcePath, destPath, overwrite: true);
        Debug.Log($"[BuildPostProcessor] steam_appid.txt copied to: {destPath}");
    }
}