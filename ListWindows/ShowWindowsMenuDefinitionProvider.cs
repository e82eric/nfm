using nfm.Ui.Core;
using nfzf;

namespace nfm.ListWindows;

public class ShowWindowsMenuDefinitionProvider2(IResultHandler resultHandler, Action? onClosed) : IMenuDefinitionProvider
{
    public MenuDefinition Get()
    {
        var definition = new MenuDefinition
        {
            AsyncFunction = nfm.ListWindows.ListWindows.Run,
            Header = null,
            ResultHandler = resultHandler,
            MinScore = 0,
            HasPreview = true,
            PreviewHandler = new WindowPreviewHandler(),
            OnClosed = onClosed,
            ScoreFunc = (sObj, pattern, slab) =>
            {
                var s = (string)sObj;
                var score = FuzzySearcher.GetScore(s, pattern, slab);
                return (s.Length, score);
            },
            Comparer = Comparers.ScoreLengthAndValue,
            FinalComparer = Comparers.ScoreLengthAndValue,
        };
        return definition;
    }
}
