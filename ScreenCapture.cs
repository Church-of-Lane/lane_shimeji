using System.Diagnostics;

namespace ShimejiLane;

public static class ScreenCapture
{

    static string folder = AppContext.BaseDirectory;
    static string outputPath = Path.Combine(folder, "screens");
    static readonly string[] encodeArgs = { "-frames:v", "1", "-vf", "scale=1280:-2", "-q:v", "4", "-f", "mjpeg", "pipe:1" };

    public static async Task<byte[]> DoScreenShot()
    {
        byte[] jpeg;
        if (OperatingSystem.IsMacOS())
        {
            // ffmpeg's avfoundation grabber hangs on single frames, so grab with the built-in
            // screencapture tool and only use ffmpeg to scale the result down.
            string tmp = Path.Combine(Path.GetTempPath(), $"shimejilane-{Guid.NewGuid():N}.jpg");
            try
            {
                await Run("screencapture", new[] { "-x", "-C", "-t", "jpg", tmp });
                jpeg = File.Exists(tmp) ? await Run("ffmpeg", new[] { "-loglevel", "error", "-i", tmp }.Concat(encodeArgs)) : [];
            }
            finally
            {
                File.Delete(tmp);
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            string display = Environment.GetEnvironmentVariable("DISPLAY") ?? ":0";
            jpeg = await Run("ffmpeg", new[] { "-loglevel", "error", "-f", "x11grab", "-i", display }.Concat(encodeArgs));
        }
        else
        {
            jpeg = await Run("ffmpeg", new[] { "-loglevel", "error", "-f", "gdigrab", "-i", "desktop" }.Concat(encodeArgs));
        }

        if (jpeg.Length > 0)
        {
            Directory.CreateDirectory(outputPath);
            string name = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".jpg";
            await File.WriteAllBytesAsync(Path.Combine(outputPath, name), jpeg);
        }
        return jpeg;

    }

    static async Task<byte[]> Run(string exe, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        using var ms = new MemoryStream();
        await p.StandardOutput.BaseStream.CopyToAsync(ms);
        await p.WaitForExitAsync();
        return ms.ToArray();
    }

}
