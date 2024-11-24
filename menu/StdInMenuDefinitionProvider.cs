using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Input;

namespace nfm.menu;

public class StdInMenuDefinitionProvider : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = Run,
            Header = null,
            KeyBindings = new Dictionary<(KeyModifiers, Key), Action<string>>(),
            ResultHandler = new StdOutResultHandler(),
            MinScore = 0,
            ShowHeader = false,
            QuitOnEscape = true
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