# Simply run all test assemblies that are found in the tree.
# If we have builds for debug and release, they are both run.
# Requires that vstest.console.exe is in the PATH.

$platforms = ('x86', 'x64')
$assemblies = Get-ChildItem LockCheck.Tests.dll -Recurse | where { $_.FullName -like "*\bin\*" }

foreach ($assembly in $assemblies) {
    foreach ($platform in $platforms) {
        echo "vstest.console.exe $assembly /Platform:$platform"
        & vstest.console.exe $assembly /Platform:$platform
        if ($LASTEXITCODE -ne 0) {
            exit 1
        }
    }
}