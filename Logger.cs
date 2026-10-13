namespace ShimejiLane;

public static class Logger
{
    // Next to the .exe, so it lives with the build (bin/... or the published folder)
    static string logPath = Path.Combine(AppContext.BaseDirectory, "log.txt");

    public static void Log(string text)
    {
        // Keep every entry on one line
        text = text.Replace("\r\n", " ").Replace("\n", " ");
        string line = DateTime.Now.ToString("yyyy-MM-dd-HH:mm:ss") + "-" + text + Environment.NewLine;
        // AppendAllText creates the file if missing and only ever adds to the end
        File.AppendAllText(logPath, line);
    }
}
