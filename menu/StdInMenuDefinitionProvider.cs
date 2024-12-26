using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using nfzf;

namespace nfm.menu;

public class StdInMenuDefinitionProvider(IMainViewModel viewModel, bool hasPreview, string? editCommandStr, string? previewCommand) : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = Run,
            Header = null,
            HasPreview = hasPreview,
            PreviewHandler = previewCommand != null ? new CommandPreviewHandler(previewCommand) : new FileSystemPreviewHandler(),
            ResultHandler = new StdOutResultHandler(viewModel),
            MinScore = 0,
            QuitOnEscape = true,
            ScoreFunc = (sObj, pattern, slab) =>
            {
                var s = (string)sObj;
                var score = FuzzySearcher.GetScore(s, pattern, slab);
                return (s.Length, score);
            },
            Comparer = Comparers.ScoreLengthAndValue,
            EditAction = editCommandStr == null ? null : async (item, newValue) =>
            {
                var oldValue = item.ToString();
                var commandStr = string.Format(editCommandStr, oldValue, newValue);
                var result = await ProcessRunner.RunCommandAsync(commandStr);
                if (result.ExitCode == 0)
                {
                    return Result.Ok();
                }

                return Result.Error(result.StandardOutput + "\n\n" + result.StandardError);
            },
        };
        return definition;
    }

    private static async Task Run(ChannelWriter<object> writer, CancellationToken cancellationToken)
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
