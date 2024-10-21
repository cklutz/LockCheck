$frameworks = ('net481', 'net8.0')
$configurations = ('Debug', 'Release')
$platforms = ('x86', 'x64')
$project = "$PSScriptRoot\LockCheck.Tests\LockCheck.Tests.csproj"

foreach ($framework in $frameworks) {
    foreach ($platform in $platforms) {
        foreach ($configuration in $configurations) {
            Write-Host -Foreground DarkBlue "`n[$framework - $configuration - $platform]"

            & dotnet test -c $configuration -f $framework -a $platform $project
            if ($LASTEXITCODE -ne 0) {
                exit 1
            }
        }
    }
}
