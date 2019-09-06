rmdir /s /q bin
dotnet publish -r win10-x64 -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true
::dotnet publish -r osx-x64 -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true