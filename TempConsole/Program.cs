using System.Threading.Channels;
using nfzf.FileSystem;

namespace TempConsole;

class Program
{
    static async Task Main(string[] args)
    {
        var scanner = new StreamingWin32DriveScanner2();
        var reader = scanner.ScanAsync(@"C:\");

// Read results
        await foreach (var file in reader.ReadAllAsync())
        {
            Console.WriteLine(file);
        }

// Wait for scanning to complete
        //await scanTask;
        //var channel = 
        //
        //var scanner = new StreamingWin32DriveScanner2();
        //await foreach (var file in scanner.ScanAsync(@"C:\"))
        //{
        //    Console.WriteLine(file);
        //}
    }
}