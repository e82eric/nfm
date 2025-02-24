REM rd /s /q bin\cli
dotnet publish PInvokeTest\PInvokeTest.csproj -c Release -r win-x64 -o "bin"
dotnet publish PowershellHistoryReader\PowershellHistoryReader.csproj -c Release -r win-x64 -o "bin"
dotnet publish LibNfm\libnfm.csproj -c Release -r win-x64 -o "bin\libnfm"
