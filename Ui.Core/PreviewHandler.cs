using System.Diagnostics;

namespace nfm.menu;

public class PreviewHandler : IPreviewHandler
{
    public async Task Handle(IPreviewRenderer renderer, object node, int height, CancellationToken ct)
    {
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var path = node.ToString();
        if (path == null)
        {
            return;
        }
        
        string[] videoExtensions = { ".mp4", ".wmv", ".avi", ".mkv", ".flv", ".mov", ".webm", ".mpeg", ".mpg", ".m4v", ".3gp", ".ogv" };
        if (videoExtensions.Any(ext => path.EndsWith(ext, StringComparison.InvariantCultureIgnoreCase)))
        {           
            Random random = new Random();
            //renderer.RenderText([$"Loading..."], ".txt");
            int randomNumber = random.Next(3, 9);
            var seconds = await CalculateThumbnailTime(renderer, path, randomNumber, ct);
            if (seconds == 0)
            {
                renderer.RenderError("Video duration is 0");
                return;
            }

            string arguments =
                $"-ss {TimeSpan.FromSeconds(seconds)} -i \"{path}\" -frames:v 1 -f image2pipe -vf \"scale=-1:{height}\" -vcodec png pipe:1";

            //renderer.RenderText([$"Loading..."], ".txt");
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = @"ffmpeg",
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
                    var completedTask = await Task.WhenAny(copyTask, exitTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        renderer.RenderError("Preview timed out");
                        return;
                    }
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
                catch (Exception)
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
                    renderer.RenderImage(memoryStream);
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
                var (isText, fileLines) = await TryReadTextFile(info, Int32.MaxValue, ct);
                if (fileLines == null)
                {
                    return;
                }
                if (ct.IsCancellationRequested)
                {
                    return;
                }
                if (isText)
                {
                    renderer.RenderText(fileLines, extension);
                    return;
                }
                else
                {
                    var lines = new List<string>();
                    var fileInfo = new FileInfo(path);
                    lines.Add($"File: {fileInfo.Name}");
                    lines.Add($"Path: {fileInfo.FullName}");
                    lines.Add($"Size: {fileInfo.Length} bytes");
                    lines.Add($"Created: {fileInfo.CreationTime}");
                    lines.Add($"Last Accessed: {fileInfo.LastAccessTime}");
                    lines.Add($"Last Modified: {fileInfo.LastWriteTime}");
                    lines.Add($"Extension: {fileInfo.Extension}");
                    lines.Add($"Is Read-Only: {fileInfo.IsReadOnly}");
                    lines.Add($"Attributes: {fileInfo.Attributes}");
                    lines.Add("The file appears to be binary and was not read.");
                    renderer.RenderText(lines, ".txt");
                    return;
                }
            }
            else if (Directory.Exists(path))
            {
                var lines = new List<string>();
                var dirInfo = new DirectoryInfo(path);
                lines.Add($"Directory: {dirInfo.Name}");
                lines.Add($"Path: {dirInfo.FullName}");
                lines.Add($"Created: {dirInfo.CreationTime}");
                lines.Add($"Last Modified: {dirInfo.LastWriteTime}");
                lines.Add($"Attributes: {dirInfo.Attributes}");
                lines.Add($"Items");

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

                    lines.Add($"  {fileSystemInfo.Name}");
                }
                renderer.RenderText(lines, ".txt");
                return;
            }
            displayText = "The provided path does not exist.";
            renderer.RenderText([displayText], extension);
        }
    }
    
    private async Task<double> CalculateThumbnailTime(IPreviewRenderer renderer, string filePath, double percentage, CancellationToken ct)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-i \"{filePath}\" -show_entries format=duration -v quiet -of csv=p=0",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync(ct);
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
    
    private async Task<(bool IsText, List<string>? Lines)> TryReadTextFile(FileInfo path, int maxLines, CancellationToken ct)
    {
        var lines = new List<string>();

        try
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
                    if (line != null)
                    {
                        lines.Add(line);
                    }
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
