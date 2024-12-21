dotnet publish Cli\Cli.csproj -c Release -r win-x64 --self-contained
dotnet publish LibNfm\libnfm.csproj -r win-x64 -c Release --property NativeLib=Static
