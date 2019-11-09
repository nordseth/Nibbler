#!/bin/bash
set -e

dotnetVersion=3.0
dotnetRuntimeTag=$dotnetVersion
dotnetSdkTag=$dotnetVersion-bionic
nibblerVersion=1.0.0-beta.2
targetImage=nibbler-test

echo "-------- Prepair images --------"
docker pull mcr.microsoft.com/dotnet/core/sdk:$dotnetSdkTag
docker pull mcr.microsoft.com/dotnet/core/aspnet:$dotnetRuntimeTag
docker tag mcr.microsoft.com/dotnet/core/aspnet:$dotnetRuntimeTag localhost:5000/dotnet/core/aspnet:$dotnetRuntimeTag
docker push localhost:5000/dotnet/core/aspnet:$dotnetRuntimeTag

echo "-------- Run build in docker image --------"

cat << EOF | docker run -i --rm mcr.microsoft.com/dotnet/core/sdk:$dotnetSdkTag bash
set -e
export PATH="\$PATH:/root/.dotnet/tools"

echo "-------- Clone repo https://github.com/nordseth/aspnetcore-new.git@$dotnetVersion --------"
cd
git clone https://github.com/nordseth/aspnetcore-new.git -b $dotnetVersion
cd aspnetcore-new

echo "-------- Build --------"
dotnet restore
dotnet build --no-restore
dotnet publish -o ./publish --no-build

echo "-------- Install Nibbler --------"
dotnet tool install -g Nibbler --version 1.0.0-beta.2

echo "-------- Build image with Nibbler --------"
nibbler init host.docker.internal:5000/dotnet/core/aspnet:$dotnetRuntimeTag --insecure --debug
nibbler labels git --debug
nibbler cmd dotnet aspnetcore-new.dll --debug
nibbler workdir /app --debug
nibbler add publish /app --debug
nibbler push host.docker.internal:5000/$targetImage:$dotnetVersion --debug

echo "-------- Success building image with Nibbler! --------"
EOF

echo "-------- Pull and run built image locally at http://localhost:8080/ --------"
echo "-- remember to stop the running image: docker ps, docker stop nibbler-test-app"

docker pull localhost:5000/$targetImage:$dotnetVersion
docker run -i --rm -p 8080:80 --name nibbler-test-app localhost:5000/$targetImage:$dotnetVersion

