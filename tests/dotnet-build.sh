#!/bin/bash
set -e

###################################
# Script used by image-compare.sh
# to build artifacts inside docker
###################################

echo "-------- dotnet-build.sh: download repo --------"

curl -L https://github.com/nordseth/aspnetcore-new/archive/5.0.tar.gz -o ../src.tar.gz
tar -xzf ../src.tar.gz
cd aspnetcore-new-5.0

echo "-------- dotnet-build.sh: dotnet build artifacts --------"

dotnet restore
dotnet build --no-restore -c Release
rm -rf /opt/app-root/workspace/TestData/publish-docker
dotnet publish -c Release --no-build -o /opt/app-root/workspace/TestData/publish-docker \
  -p:MicrosoftNETPlatformLibrary=Microsoft.NETCore.App
  
echo "-------- dotnet-build.sh: end --------"