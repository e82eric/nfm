rd /s /q bin\cli

dotnet publish Cli\Cli.csproj -c Release -r win-x64 --self-contained -o "bin\cli"
dotnet publish LibNfm\libnfm.csproj -c Release -r win-x64 -o "bin\libnfm"
