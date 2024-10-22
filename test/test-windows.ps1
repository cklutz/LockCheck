$frameworks = ('net481', 'net8.0', 'net9.0')
$configurations = ('Debug', 'Release')
$platforms = ('x86', 'x64')
$project = "$PSScriptRoot\LockCheck.Tests\LockCheck.Tests.csproj"
$resultsDir = "$((Get-Item $PSScriptRoot).Parent.FullName)\artifacts\TestResults"

$env:DOTNET_CLI_TELEMETRY_OPTOUT=1

# Build once for every configuration (platforms and frameworks will be handled automatically)
# foreach ($configuration in $configurations) {
#     & dotnet build -c $configuration $project
#     if ($LASTEXITCODE -ne 0) {
#         exit 1
#     }
# }

# Run tests; dedicated per framework/configuration/platform so that the test runner itself can
# also uses the desired platform.
foreach ($framework in $frameworks) {
    foreach ($platform in $platforms) {
        foreach ($configuration in $configurations) {
            Write-Host -Foreground DarkBlue "`n[$framework - $configuration - $platform]"
            $runPivot = "${configuration}_${framework}_win-${platform}".ToLowerInvariant()
            & dotnet test --results-directory "$resultsDir\$runPivot" -c $configuration -f $framework -a $platform "$project"
            if ($LASTEXITCODE -ne 0) {
                exit 1
            }
        }
    }
}
