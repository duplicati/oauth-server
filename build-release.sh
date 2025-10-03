#!/bin/bash

# Build the release version of the project
RIDS="win-x64 linux-x64 osx-arm64"
VERSION=1.0.1
DATE=$(date +%Y-%m-%d)

# Remove the existing release folder
rm -rf ./bin/Release

for RID in $RIDS
do
    # Build a release version of the project
    dotnet publish -c Release -o ./bin/Release/$RID --self-contained -r $RID
    
    # Create a zip file for each platform
    pushd ./bin/Release/$RID
    zip -r ../../Release-$RID-$VERSION-$DATE.zip .
    popd
done
