using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Input;
using nfzf;

namespace nfm.menu;

public class StdInMenuDefinitionProvider : IMenuDefinitionProvider<string>
{
    public MenuDefinition<string> Get()
    {
        var definition = new MenuDefinition<string>
        {
            AsyncFunction = Run,
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Func<string, Task>>(),
            ResultHandler = new StdOutResultHandler(),
            MinScore = 0,
            ShowHeader = false,
            QuitOnEscape = true,
            ScoreFunc = (s, pattern, slab) =>
            {
                var score = FuzzySearcher.GetScore(s, pattern, slab);
                return (s.Length, score);
            },
            Comparer = Comparers.StringScoreLengthAndValue,
            StrConverter = new StringConverter()
        };
        return definition;
    }

    private static async Task Run(ChannelWriter<string> writer, CancellationToken cancellationToken)
    {
        await Task.Run(async () =>
        {
            try
            {
                using (var reader = Console.In)
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var lineTask = reader.ReadLineAsync();

                        var completedTask =
                            await Task.WhenAny(lineTask, Task.Delay(Timeout.Infinite, cancellationToken));

                        if (completedTask == lineTask)
                        {
                            string line = await lineTask;
                            if (line == null)
                            {
                                break;
                            }

                            await writer.WriteAsync(line, cancellationToken);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                writer.Complete();
            }
        });
    }
}