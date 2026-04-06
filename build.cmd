rd /s /q bin\cli
dotnet publish Cli\Cli.csproj -c Release -r win-x64 -o "bin" --disable-parallel -p:CopyRetryCount=3 -p:CopyRetryDelayMilliseconds=500
dotnet publish PowershellHistoryReader\PowershellHistoryReader.csproj -c Release -r win-x64 -o "bin" --disable-parallel -p:CopyRetryCount=3 -p:CopyRetryDelayMilliseconds=500
dotnet publish LibNfm\libnfm.csproj -c Release -r win-x64 -o "bin\libnfm" --disable-parallel -p:CopyRetryCount=3 -p:CopyRetryDelayMilliseconds=500
