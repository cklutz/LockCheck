docker run --rm --name LockCheck.Tests -v ${PSScriptRoot}/..:/mnt/lc -w /mnt/lc mcr.microsoft.com/dotnet/sdk:8.0 bash /mnt/lc/test/test-linux.sh
