#!/bin/bash

frameworks=('net8.0')
configurations=('Release' 'Debug')
platforms=('x64')
project="$(dirname $0)/LockCheck.Tests/LockCheck.Tests.csproj"

for framework in "${frameworks[@]}"; do
    for configuration in "${configurations[@]}"; do
        for platform in "${platforms[@]}"; do
            echo -e "\n\033[34m[$framework - $configuration - $platform]\033[0m"
            /usr/share/dotnet/dotnet test --logger console -c $configuration -f $framework -a $platform $project || exit 1
        done
    done
done
