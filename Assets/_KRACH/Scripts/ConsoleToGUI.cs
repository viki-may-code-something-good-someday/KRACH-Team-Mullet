using UnityEngine;

/// <summary>
/// In-game log for builds: F1 shows the latest log lines. Additionally everything is written
/// to Desktop/YOUR_LOGS/log-XXXX.txt (for tester bug reports).
/// </summary>
public class ConsoleToGUI : MonoBehaviour
{
    private const int MaxOnScreenChars = 1500;

    private string myLog = "*begin log";
    private string filename = "";
    private bool doShow = false;
    private Vector2 scroll;

    void OnEnable() { Application.logMessageReceived += Log; }
    void OnDisable() { Application.logMessageReceived -= Log; }
    void Update() { if (Input.GetKeyDown(KeyCode.F1)) { doShow = !doShow; } }

    public void Log(string logString, string stackTrace, LogType type)
    {
        string prefix = type switch
        {
            LogType.Error => "[ERR] ",
            LogType.Exception => "[EXC] ",
            LogType.Assert => "[ASSERT] ",
            LogType.Warning => "[WARN] ",
            _ => ""
        };

        // Previously without line breaks – all messages were glued together on one line.
        string line = $"{prefix}{logString}";
        if (type == LogType.Exception && !string.IsNullOrEmpty(stackTrace)) line += "\n" + stackTrace;

        // for onscreen...
        myLog = myLog + "\n" + line;
        if (myLog.Length > MaxOnScreenChars) { myLog = myLog.Substring(myLog.Length - MaxOnScreenChars); }

        // for the file ...
        if (filename == "")
        {
            string d = System.Environment.GetFolderPath(
               System.Environment.SpecialFolder.Desktop) + "/YOUR_LOGS";
            System.IO.Directory.CreateDirectory(d);
            string r = Random.Range(1000, 9999).ToString();
            filename = d + "/log-" + r + ".txt";
        }
        try { System.IO.File.AppendAllText(filename, $"{System.DateTime.Now:HH:mm:ss} {line}\n"); }
        catch { }
    }

    void OnGUI()
    {
        if (!doShow) { return; }
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
           new Vector3(Screen.width / 1200.0f, Screen.height / 800.0f, 1.0f));

        GUILayout.BeginArea(new Rect(10, 10, 700, 420), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label(myLog);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
