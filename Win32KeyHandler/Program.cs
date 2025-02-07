using System.Runtime.Versioning;
using nfm.menu;
using Win32FromForms;

namespace Win32KeyHandler;

[SupportedOSPlatform("windows")]
public class Program
{
    private const int VK_P = 0x50;
    public static void Main()
    {
        var viewModel = new ViewModel();
        var window = new Win32Window(viewModel, () =>
        {
        });
        
            var definitionProvider = new FileSystemMenuDefinitionProvider(
                new StdOutResultHandler(viewModel),
                int.MaxValue,
                ["C:\\users\\eric\\src"],
                false,
                true,
                false,
                false,
                viewModel,
                null,
                () => { viewModel.Close(); });
            var keyBindings = new Dictionary<(GlobalKeyHandler.Modifiers, int), Func<Task>>
            {
                {
                    (GlobalKeyHandler.Modifiers.LAlt | GlobalKeyHandler.Modifiers.LShift, VirtualKeyCodes.VK_P),
                    async () => { await viewModel.RunDefinitionAsync(definitionProvider.Get()); }
                },
            };
            GlobalKeyHandler.SetHook(keyBindings);
        window.Run();
    }
}