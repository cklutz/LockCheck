#!/bin/bash
#
# Run test cycle on linux. This script can be run directly from within Linux (e.g. WSL),
# and also serves as the driver to be run inside a docker container (see test-docker-linux.ps1).
#

frameworks=('net8.0' 'net9.0')
configurations=('Release' 'Debug')
platforms=('x64')
project="$(dirname $0)/LockCheck.Tests/LockCheck.Tests.csproj"
resultsDir="$(dirname $0)/../artifacts/TestResults"

export DOTNET_CLI_TELEMETRY_OPTOUT=1

# TODO: This issue https://github.com/dotnet/sdk/issues/29742 prevents us from running
# the build separately, like on Windows, and thus less often then with every test
# combination.
for framework in "${frameworks[@]}"; do
    for configuration in "${configurations[@]}"; do
        for platform in "${platforms[@]}"; do
            echo -e "\n\033[34m[$framework - $configuration - $platform]\033[0m"
            runPivot=$(echo "${configuration}_${framework}_linux-${platform}" | tr '[:upper:]' '[:lower:]')
            /usr/share/dotnet/dotnet test --logger console --results-directory "$resultsDir/$runPivot" -c $configuration -f $framework -a $platform $project || exit 1
        done
    done
done
