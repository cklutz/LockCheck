docker run `
    --rm --name LockCheck.Tests `
    -e VSTEST_HOST_DEBUG=1 `
    -v $PWD:/mnt/lc `
    -w /mnt/lc `
    mcr.microsoft.com/dotnet/sdk:9.0 `
    "dotnet build &&  dotnet test LockCheck.Tests\LockCheck.Tests.csproj"