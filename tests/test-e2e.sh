#!/bin/bash
set -e

###################################
# Script that does end to end testing of
# building a dotnet project in a container, creating a image with Nibbler and running it
# uses git to clone https://github.com/nordseth/aspnetcore-new as source
###################################

dotnetVersion=3.1
dotnetRuntimeTag=$dotnetVersion
dotnetSdkTag=$dotnetVersion
targetImage=nibbler-test

echo "-------- Prepair images --------"
docker pull mcr.microsoft.com/dotnet/core/sdk:$dotnetSdkTag
docker pull mcr.microsoft.com/dotnet/core/aspnet:$dotnetRuntimeTag
docker tag mcr.microsoft.com/dotnet/core/aspnet:$dotnetRuntimeTag localhost:5000/dotnet/core/aspnet:$dotnetRuntimeTag
docker push localhost:5000/dotnet/core/aspnet:$dotnetRuntimeTag

echo "-------- Create Nibbler nuget --------"
dotnet pack ../Nibbler -o ./nuget
NIBBLER_VERSION=$(minver -t v -v w)

echo "-------- Run build in docker image --------"

cat << EOF | docker run -i --rm -v /$PWD/nuget:/nuget mcr.microsoft.com/dotnet/core/sdk:$dotnetSdkTag bash
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
dotnet tool install -g Nibbler --version ${NIBBLER_VERSION} --add-source /nuget

echo "-------- Build image with Nibbler --------"
nibbler \
	--from-image host.docker.internal:5000/dotnet/core/aspnet:$dotnetRuntimeTag \
	--to-image host.docker.internal:5000/$targetImage:$dotnetVersion \
	--add "publish:/app" \
	--addFolder "/app:1001:1001:777" \
	--git-labels \
	--workdir /app \
	--cmd "dotnet aspnetcore-new.dll" \
	--insecure \
	-v

echo "-------- Success building image with Nibbler! --------"
EOF

echo "-------- Pull and run built image locally at http://localhost:8080/ --------"
echo "-- remember to stop the running image: docker ps, docker stop nibbler-test-app"

docker pull localhost:5000/$targetImage:$dotnetVersion
docker run -d --rm -p 8080:80 --name nibbler-test-app localhost:5000/$targetImage:$dotnetVersion
docker ps -a -f name=nibbler-test-app
