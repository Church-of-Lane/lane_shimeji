using System.Diagnostics;

namespace ShimejiLane;

public static class ScreenCapture
{
    
    static string folder = AppContext.BaseDirectory;
    static string outputPath = Path.Combine(folder, "screens");
    public static async Task<byte[]> DoScreenShot()
    {
        string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        var psi = new ProcessStartInfo("ffmpeg")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in new[] { "-loglevel", "error", "-f", "gdigrab", "-i", "desktop",
                                  "-frames:v", "1", "-vf", "scale=1280:-2", "-q:v", "4", "-f", "mjpeg", "pipe:1" })
            psi.ArgumentList.Add(a);

        
        using var p = Process.Start(psi)!;
        using var ms = new MemoryStream();
        await p.StandardOutput.BaseStream.CopyToAsync(ms);
        await p.WaitForExitAsync();
        byte[] jpeg = ms.ToArray();
        if (jpeg.Length > 0)
        {
            Directory.CreateDirectory(outputPath);
            string name = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".jpg";
            await File.WriteAllBytesAsync(Path.Combine(outputPath, name), jpeg);
        }
        return jpeg;

    }
    
}