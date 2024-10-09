#    -e VSTEST_HOST_DEBUG=1 `
docker run `
    --rm --name LockCheck.Tests `
    -v ${PWD}\..:/mnt/lc `
    -w /mnt/lc `
    mcr.microsoft.com/dotnet/sdk:8.0 `
    bash -c '/usr/share/dotnet/dotnet test -c Release -f net8.0 test/LockCheck.Tests/LockCheck.Tests.csproj && /usr/share/dotnet/dotnet test -c Debug -f net8.0 test/LockCheck.Tests/LockCheck.Tests.csproj'