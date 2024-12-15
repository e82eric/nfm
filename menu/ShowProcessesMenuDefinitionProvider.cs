﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Input;
using nfzf;
using nfzf.ListProcesses;

namespace nfm.menu;

public class ShowProcessesMenuDefinitionProvider(MainViewModel mainViewModel, Action? onClosed) : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var header = string.Format("{0,-75} {1,8} {2,20} {3,20} {4,10}",
            "Name", "PID", "WorkingSet(kb)", "PrivateBytes(kb)", "CPU(s)");
        var keyBindings = new Dictionary<(KeyModifiers, Key), Func<object, Task>>();
        keyBindings.Add((KeyModifiers.Control, Key.K), async lineObj =>
        {
            var line = (string)lineObj;
            var match = Regex.Match(line, @"\s+([0-9]+)\s+");
            if (!match.Success)
            {
                return;
            }

            var pidString = match.Groups[1].Value;

            var pid = Convert.ToInt32(pidString);
            
            await mainViewModel.ShowToast($"Killed process {pid}", 500);
            await ProcessLister.KillProcessById(line, pid);
        });
        keyBindings.Add((KeyModifiers.Control, Key.M), async lineObj =>
        {
            var line = (string)lineObj;
            var match = Regex.Match(line, @"\s+([0-9]+)\s+");
            if (!match.Success)
            {
                await mainViewModel.ShowToast("Failed to parse process ID from input.");
                return;
            }

            if (!int.TryParse(match.Groups[1].Value, out var pid))
            {
                await mainViewModel.ShowToast("Invalid process ID format.");
                return;
            }

            await Task.Run(async () =>
            {
                try
                {
                    var process = Process.GetProcessById(pid);
                    var dumpFilePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        $"{process.ProcessName}_{DateTimeOffset.Now:yyyyMMddHHmmss}.dmp");

                    MemoryDumpTaker.TakeMemoryDump(pid, dumpFilePath);
                    await mainViewModel.ShowToast($"Memory dump of {process.ProcessName} saved to: {dumpFilePath}");
                }
                catch (Exception e)
                {
                    await mainViewModel.ShowToast($"Memory dump of {pid} failed: {e.Message}");
                }
            });
        });
        keyBindings.Add((KeyModifiers.Control, Key.Z), async lineObj =>
        {
            var line = (string)lineObj;
            var match = Regex.Match(line, @"\s+([0-9]+)\s+");
            if (!match.Success)
            {
                await mainViewModel.ShowToast("Failed to parse process ID from input.");
                return;
            }

            if (!int.TryParse(match.Groups[1].Value, out var pid))
            {
                await mainViewModel.ShowToast("Invalid process ID format.");
                return;
            }

            //await Task.Run(async () =>
            //{
                try
                {
                    JitDebugLauncher.LaunchJitDebugger(pid);
                    await mainViewModel.ShowToast($"Launched jit debugger for: {pid}");
                }
                catch (Exception e)
                {
                    await mainViewModel.ShowToast($"Launching jit debugger failed for {pid} failed: {e.Message}");
                }
            //});
        });

        AddResultKeyBinding(keyBindings, header, ProcessLister.RunSortedByCpu2, (KeyModifiers.Control, Key.D1));
        AddResultKeyBinding(keyBindings, header, ProcessLister.RunSortedByPid2, (KeyModifiers.Control, Key.D2));
        AddResultKeyBinding(keyBindings, header, ProcessLister.RunSortedByPrivateBytes2, (KeyModifiers.Control, Key.D3));
        AddResultKeyBinding(keyBindings, header, ProcessLister.RunSortedByWorkingSet2, (KeyModifiers.Control, Key.D4));

        var definition = CreateDefinition(ProcessLister.RunSortedByWorkingSet2, header, keyBindings, Comparers.StringScoreLengthAndValue);
        return definition;
    }

    private void AddResultKeyBinding(
        Dictionary<(KeyModifiers, Key), Func<object, Task>> keyBindings,
        string header,
        Func<ChannelWriter<object>, CancellationToken, Task> asyncFunc,
        (KeyModifiers Control, Key D4) keys)
    {
        keyBindings.Add(keys, async _ =>
        {
            var definition = CreateDefinition(asyncFunc, header, keyBindings, Comparer);
            await mainViewModel.Clear();
            await mainViewModel.RunDefinitionAsync(definition);
        });
    }

    private MenuDefinition CreateDefinition(
        Func<ChannelWriter<object>, CancellationToken, Task> resultFunc,
        string? header,
        Dictionary<(KeyModifiers, Key), Func<object, Task>> keyBindings, IComparer<Entry> comparer)
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = resultFunc,
            Header = header,
            KeyBindings = keyBindings,
            MinScore = 0,
            ResultHandler = new StdOutResultHandler(),
            ShowHeader = true,
            Comparer = comparer,
            FinalComparer = comparer,
            OnClosed = onClosed,
            ScoreFunc = (sObj, pattern, slab) =>
            {
                var s = (string)sObj;
                var result = FuzzySearcher.GetScore(s, pattern, slab);
                return (s.Length, result);
            },
        };
        return definition;
    }

    private static readonly IComparer<Entry> Comparer = Comparer<Entry>.Create((x, y) => y.Score.CompareTo(x.Score));
}