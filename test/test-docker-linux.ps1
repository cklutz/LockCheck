#    -e VSTEST_HOST_DEBUG=1 `
$mydir = $PSScriptRoot
$project = './test/LockCheck.Tests/LockCheck.Tests.csproj'
$dotnet = '/usr/share/dotnet/dotnet'

$script = "$dotnet test -c Release -f net8.0 && $dotnet test -c Debug -f net8.0"
$script = $script.Replace("`r", "")

docker run --rm --name LockCheck.Tests -v ${mydir}/..:/mnt/lc -w /mnt/lc mcr.microsoft.com/dotnet/sdk:8.0 bash -c $script
