namespace nfm.Ui.Core;

public class CommandPreviewHandler(string commandTemplate, char? delimiter, string? previewStartLineCommand, string? previewStartLineOffsetCommand) : IPreviewHandler
{
    public async Task Handle(IPreviewRenderer renderer, object t, int height, CancellationToken ct)
    {
        object[] commandParams;
        var tStr = t.ToString();
        if (delimiter != null && tStr != null)
        {
            //TODO: string[] to object[] is there a better way
            commandParams = tStr.Split(delimiter.Value);
        }
        else
        {
            commandParams = [tStr ?? string.Empty];
        }
        var command = string.Format(commandTemplate, commandParams);
        var result = await ProcessRunner.RunCommandAsync(command);
        if (result.ExitCode == 0)
        {
            var textSegments = TerminalEscapeCodeConverter.Convert(result.StandardOutput);
            var startLine = 0;
            if (previewStartLineCommand != null)
            {
                var startLineCommandResult = string.Format(previewStartLineCommand, commandParams);
                if (!int.TryParse(startLineCommandResult, out startLine))
                {
                    //TODO: log something
                }
            }

            var startLineOffset = 0;
            if (previewStartLineOffsetCommand != null)
            {
                var offsetResult = string.Format(previewStartLineOffsetCommand, commandParams);
                if (!int.TryParse(previewStartLineOffsetCommand, out startLineOffset))
                {
                    //TODO: log something
                }
            }
            
            renderer.RenderText(textSegments, startLine - startLineOffset);
        }
        else
        {
            renderer.RenderError(result.StandardOutput + "\n" + result.StandardError);
        }
    }
}
