using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace nfm.menu;

public class TestResultHandler(
    bool quitAfter,
    bool quitOnEscape,
    bool writeToStdOut,
    bool searchDirectories,
    bool hasPreview,
    bool directoriesOnly,
    bool filesOnly)
    : IResultHandler
{

    private static bool IsDirectory(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        return attributes.HasFlag(FileAttributes.Directory);
    }

    public async Task HandleAsync(string output, MainViewModel viewModel)
    {
        if (!IsDirectory(output) || !searchDirectories)
        {
            await viewModel.Close();
            if (writeToStdOut)
            {
                Console.WriteLine(output);
            }
            else
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = output,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
            }

            if (quitAfter)
            {
                Environment.Exit(0);
            }
        }
        else
        {
            var definition =
                new FileSystemMenuDefinitionProvider(
                    this,
                    Int32.MaxValue,
                    [output],
                    quitOnEscape,
                    hasPreview,
                    directoriesOnly,
                    filesOnly,
                    viewModel,
                    null).Get();

            await viewModel.Clear();
            await viewModel.RunDefinitionAsync(definition);
        }
    }
}