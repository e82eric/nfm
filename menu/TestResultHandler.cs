using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Input;
using nfzf.FileSystem;

namespace nfm.menu;

public class TestResultHandler(
    MainViewModel viewModel,
    bool quitAfter,
    bool quitOnEscape,
    bool writeToStdOut,
    bool searchDirectories,
    bool hasPreview)
    : IResultHandler
{
    private readonly StreamingWin32DriveScanner2 _fileScanner = new();

    private static bool IsDirectory(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        return attributes.HasFlag(FileAttributes.Directory);
    }
    
    public async Task HandleAsync(string output, MainViewModel viewModel)
    {
        if (!IsDirectory(output) || !searchDirectories)
        {
            viewModel.Close();
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
            var definition = new MenuDefinition
            {
                AsyncFunction = (writer, ct) => _fileScanner.StartScanForDirectoriesAsync(
                    [output],
                    writer,
                    Int32.MaxValue,
                    ct),
                Header = null,
                KeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>(),
                MinScore = 0,
                ResultHandler = this,
                ShowHeader = false,
                QuitOnEscape = quitOnEscape,
                HasPreview = hasPreview
            };

            //window.Show();
            viewModel.Clear();
            await viewModel.RunDefinitionAsync(definition);
        }
    }
}