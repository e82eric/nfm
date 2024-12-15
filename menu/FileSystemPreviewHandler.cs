using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using TextMateSharp.Grammars;

namespace nfm.menu;

public class FileSystemPreviewHandler : IPreviewHandler
{
    public async Task Handle(IPreviewRenderer renderer, object node, CancellationToken ct)
    {
        var path = node.ToString();
            
        if (path.EndsWith(".mp4", StringComparison.InvariantCultureIgnoreCase) || path.EndsWith(".wmv", StringComparison.InvariantCultureIgnoreCase))
        {
            Random random = new Random();
            renderer.RenderText($"Loading...", ".txt");
            int randomNumber = random.Next(3, 9);
            var seconds = await CalculateThumbnailTime(renderer, path, randomNumber, ct);
            if (seconds == 0)
            {
                renderer.RenderError("Video duration is 0");
                return;
            }
            string arguments =
                $"-ss {TimeSpan.FromSeconds(seconds)} -i \"{path}\" -frames:v 1 -f image2pipe -vcodec png pipe:1";

            renderer.RenderText($"Loading...", ".txt");
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = @"C:\msys64\mingw64\bin\ffmpeg.exe",
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = null,
                    }
                };

                if (ct.IsCancellationRequested)
                {
                    renderer.RenderError("Cancelled");
                    return;
                }

                process.Start();

                var memoryStream = new MemoryStream();
                try
                {
                    var copyTask = process.StandardOutput.BaseStream.CopyToAsync(memoryStream, ct);
                    var exitTask = process.WaitForExitAsync(ct);

                    // Wait for either the process to exit or the copy task to complete
                    var completedTask = await Task.WhenAny(copyTask, exitTask);

                    if (completedTask == exitTask)
                    {
                        // Process has exited, cancel the copy operation
                        if (!copyTask.IsCompleted)
                        {
                            ct.ThrowIfCancellationRequested(); // Ensure the cancellation token propagates
                        }
                    }

                    // Ensure both tasks have completed
                    await Task.WhenAll(copyTask, exitTask);
                }
                catch (Exception e)
                {
                    process.Kill();
                }

                string error = await process.StandardError.ReadToEndAsync(ct);
                if (process.ExitCode != 0)
                {
                    renderer.RenderError(error);
                    return;
                }

                if (ct.IsCancellationRequested)
                {
                    renderer.RenderError("Cancelled");
                    return;
                }
                memoryStream.Seek(0, SeekOrigin.Begin);
                try
                {
                    renderer.RenderImage(new Bitmap(memoryStream));
                }
                catch (Exception e)
                {
                    renderer.RenderError(e.Message);
                }
            }
            catch (Exception ex)
            {
                renderer.RenderError(ex.Message);
            }
        }
        else
        {
            var displayText = string.Empty;
            var extension = ".txt";
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                var (isText, lines) = await TryReadTextFile(info, Int32.MaxValue, ct);
                if (ct.IsCancellationRequested)
                {
                    return;
                }
                if (isText)
                {
                    if (lines.Any())
                    {
                        displayText += $"{string.Join("\n", lines)}";
                    }
                    else
                    {
                        displayText += "\n\nThe file is empty.";
                    }

                    extension = info.Extension;
                }
                else
                {
                    var fileInfo = new FileInfo(path);
                    displayText = $"File: {fileInfo.Name}\n" +
                                  $"Path: {fileInfo.FullName}\n" +
                                  $"Size: {fileInfo.Length} bytes\n" +
                                  $"Created: {fileInfo.CreationTime}\n" +
                                  $"Last Accessed: {fileInfo.LastAccessTime}\n" +
                                  $"Last Modified: {fileInfo.LastWriteTime}\n" +
                                  $"Extension: {fileInfo.Extension}\n" +
                                  $"Is Read-Only: {fileInfo.IsReadOnly}\n" +
                                  $"Attributes: {fileInfo.Attributes}";

                    displayText += "\n\nThe file appears to be binary and was not read.";
                }
            }
            else if (Directory.Exists(path))
            {
                var dirInfo = new DirectoryInfo(path);
                displayText = $"Directory: {dirInfo.Name}\n" +
                              $"Path: {dirInfo.FullName}\n" +
                              $"Created: {dirInfo.CreationTime}\n" +
                              $"Last Modified: {dirInfo.LastWriteTime}\n" +
                              $"Attributes: {dirInfo.Attributes}\n\n" +
                              $"Items\n";

                var i = 0;
                foreach (var fileSystemInfo in dirInfo.EnumerateFileSystemInfos())
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }
                    if (i > 15)
                    {
                        break;
                    }

                    displayText += $"  {fileSystemInfo.Name}\n";
                }
            }
            else
            {
                displayText = "The provided path does not exist.";
            }
            
            renderer.RenderText(displayText, extension);
        }
    }
    
    private async Task<double> CalculateThumbnailTime(IPreviewRenderer renderer, string filePath, double percentage, CancellationToken ct)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "C:\\msys64\\mingw64\\bin\\ffprobe.exe",
                Arguments = $"-i \"{filePath}\" -show_entries format=duration -v quiet -of csv=p=0",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync(ct);
        
        string error = await process.StandardError.ReadToEndAsync(ct);
        if (process.ExitCode != 0)
        {
            renderer.RenderError(error);
            return 0;
        }

        if (double.TryParse(output, out double duration))
        {
            return duration * percentage / 100;
        }

        renderer.RenderError("Failed to parse duration from ffprobe output.");
        return 0;
    }
    
    private async Task<(bool IsText, List<string> Lines)> TryReadTextFile(FileInfo path, int maxLines, CancellationToken ct)
    {
        var registryOptions = new RegistryOptions(ThemeName.Dark);
        var language = registryOptions.GetLanguageByExtension(path.Extension);
        
        var lines = new List<string>();

        try
        {
            if (language == null)
            {
                if (ct.IsCancellationRequested)
                {
                    return (false, null);
                }
                
                using (var stream = new FileStream(path.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new StreamReader(stream))
                {
                    if (ct.IsCancellationRequested)
                    {
                        return (false, null);
                    }
                    
                    var buffer = new char[1024];
                    int charsRead = await reader.ReadAsync(buffer, 0, buffer.Length);

                    // Check for binary content in the first 1024 characters
                    for (int i = 0; i < charsRead; i++)
                    {
                        if (buffer[i] == '\0' ||
                            (buffer[i] < 32 && buffer[i] != '\t' && buffer[i] != '\n' && buffer[i] != '\r'))
                        {
                            return (false, null); // File appears to be binary
                        }
                    }
                }
            }

            using (var stream = new FileStream(path.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(stream))
            {
                // If the file is determined to be text, read the first `maxLines`
                stream.Position = 0; // Reset to the beginning for reading lines
                while (!reader.EndOfStream && lines.Count < maxLines)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return (false, null);
                    }
                    var line = await reader.ReadLineAsync(ct);
                    lines.Add(line);
                }
            }

            return (true, lines); // File is text and lines are read
        }
        catch
        {
            return (false, null); // If an error occurs, assume it's not a valid text file
        }
    }
}
