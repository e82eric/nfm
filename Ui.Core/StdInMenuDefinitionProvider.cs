using System.Text;
using System.Threading.Channels;
using Core;
using nfzf;

namespace nfm.menu;

public class StdInMenuDefinitionProvider(
    IMainViewModel viewModel,
    bool hasPreview,
    string? editCommandStr,
    string? previewCommand,
    char? previewDelimiter,
    string? previewStartLineCommand,
    string? previewStartLineOffsetCommand,
    string? header,
    string? lineContinuationChar,
    bool showGap) : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = lineContinuationChar != null ? async (wt, ct) =>
                {
                    await ReadFromStdInByLine2(wt, lineContinuationChar, ct);
                }
                : ReadFromStdInByLine,
            Header = header,
            HasPreview = hasPreview,
            PreviewHandler = previewCommand != null ? new CommandPreviewHandler(previewCommand, previewDelimiter, previewStartLineCommand, previewStartLineOffsetCommand) : new PreviewHandler(
                "bat --style=numbers --color=always --theme=gruvbox-dark --paging=never {0}",
                "pwsh -C dir {0}",
                previewDelimiter,
                previewStartLineCommand,
                previewStartLineOffsetCommand),
            ResultHandler = new StdOutResultHandler(viewModel),
            MinScore = 0,
            QuitOnEscape = true,
            ScoreFunc = (sObj, pattern, slab) =>
            {
                string s;
                if (sObj is TerminalEscapedLine escapedLine)
                {
                    s = escapedLine.ToString();
                }
                else
                {
                    s = (string)sObj;
                }
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
            ShowGap = showGap
        };
        return definition;
    }
    
    private static async Task ReadFromStdInByLine2(ChannelWriter<object> writer, string delimiter, CancellationToken cancellationToken)
    {
        //Reading from stdin is blocking it needs to happen on its own thread.  The async await below is probably overhead
        await Task.Run(async () => await ReadFromStdInByLineImpl(writer, delimiter, cancellationToken)); 
    }

    private static async Task ReadFromStdInByLine(ChannelWriter<object> writer, CancellationToken cancellationToken)
    {
        await Task.Run(async () => { await ReadFromStdInByLineImpl(writer, cancellationToken); });
    }

    private static async Task ReadFromStdInByLineImpl(ChannelWriter<object> writer, string delimiter, CancellationToken cancellationToken)
    {
        var stringBuilder = new StringBuilder();
        var currentLine = new TerminalEscapedLine();

        try
        {
            using (var reader = Console.In)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var lineTask = reader.ReadLineAsync();
                    var completedTask = await Task.WhenAny(lineTask, Task.Delay(Timeout.Infinite, cancellationToken));

                    if (completedTask == lineTask)
                    {
                        string? line = await lineTask;
                        if (line == null)
                        {
                            if (stringBuilder.Length > 0)
                            {
                                await writer.WriteAsync(stringBuilder.ToString(), cancellationToken);
                            }
                            break;
                        }

                        var parsedLine = TerminalEscapeCodeConverter.Parse(line);
                        if (parsedLine.LineText().EndsWith(delimiter))
                        {
                            currentLine.Lines.Add(parsedLine);
                        }
                        else
                        {
                            currentLine.Lines.Add(parsedLine);
                            await writer.WriteAsync(currentLine, cancellationToken);
                            currentLine = new TerminalEscapedLine();
                        }
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
    }

    private static async Task ReadFromStdInByLineImpl(ChannelWriter<object> writer, CancellationToken cancellationToken)
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
                        string? line = await lineTask;
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
    }
}
