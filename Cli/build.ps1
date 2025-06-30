#del obj -Recurse -Force
#del bin -Recurse -Force
dotnet publish -r win-x64 -c RELEASE -o bin
