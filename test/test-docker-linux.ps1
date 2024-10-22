$ErrorActionPreference = 'Stop'

$SourcesRootDir=((Get-Item $PSScriptRoot).Parent.FullName)
$ContainerWorkDir="/mnt/lc"
$ImageName="$env:USERNAME-lockcheck-tests"
$ContainerName="LockCheck.Tests"
$ContextDir="$SourcesRootDir\artifacts\TestContainer"
$DockerFileContent=@'
FROM mcr.microsoft.com/dotnet/sdk:9.0 as build
# Copy .NET 8.0 runtime files
COPY --from=mcr.microsoft.com/dotnet/sdk:8.0 /usr/share/dotnet/shared /usr/share/dotnet/shared
'@

# Create a docker image that combines multiple dotnet versions
mkdir -Force $ContextDir | Out-Null
echo $DockerFileContent > "$ContextDir\Dockerfile"
docker build -q -t $ImageName $ContextDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create container"
    exit 1
}

# Run tests
docker run --rm --name $ContainerName -v ${SourcesRootDir}:$ContainerWorkDir -w $ContainerWorkDir $ImageName bash $ContainerWorkDir/test/test-linux.sh
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to run tests"
    exit 1
}

# Don't rely on "prune" to be run eventually.
# If tests were successfull we don't need it anymore.
docker image rm $ImageName 
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to remove image $ImageName"
    exit 1
}
