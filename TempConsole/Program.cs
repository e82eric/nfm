using nfm.menu;
using nfzf.ListProcesses;

namespace TempConsole;

class Program
{
    static void Main(string[] args)
    {
        foreach(var window in ListWindows.Run())
        {
            Console.WriteLine(window);
        }
        
        foreach(var window in ProcessLister.RunNoSort())
        {
            Console.WriteLine(window);
        }
    }
}