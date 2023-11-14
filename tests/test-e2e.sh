#!/bin/bash
set -e
#set -x

###################################
# Script that does end to end testing of
# building a dotnet project in a container, creating a image with Nibbler and running it
# uses git to clone https://github.com/nordseth/aspnetcore-new as source
###################################

dotnetVersion=8.0
dotnetRuntimeTag=$dotnetVersion
dotnetSdkTag=$dotnetVersion
targetImage=nibbler-test
dockerReg=localhost:5000

echo "-------- Prepair images --------"
docker pull mcr.microsoft.com/dotnet/sdk:$dotnetSdkTag
docker pull mcr.microsoft.com/dotnet/aspnet:$dotnetRuntimeTag
docker tag mcr.microsoft.com/dotnet/aspnet:$dotnetRuntimeTag localhost:5000/dotnet/aspnet:$dotnetRuntimeTag
docker push localhost:5000/dotnet/aspnet:$dotnetRuntimeTag
docker image rm localhost:5000/dotnet/aspnet:$dotnetRuntimeTag

echo "-------- Create Nibbler nuget --------"
dotnet pack ../Nibbler -o ./nuget
NIBBLER_VERSION=$(minver -t v -v e)

echo "-------- Run build in docker image --------"

cat << EOF | docker run -i --rm --network host -v /$PWD/nuget:/nuget mcr.microsoft.com/dotnet/sdk:$dotnetSdkTag bash
set -e
export PATH="\$PATH:/root/.dotnet/tools"

echo "-------- Clone repo https://github.com/nordseth/aspnetcore-new.git@$dotnetVersion --------"
cd
git clone https://github.com/nordseth/aspnetcore-new.git -b $dotnetVersion
cd aspnetcore-new

echo "-------- Build --------"
dotnet restore
dotnet build --no-restore
dotnet publish -o ./publish 

echo "-------- Install Nibbler --------"
dotnet tool install -g Nibbler --version ${NIBBLER_VERSION} --add-source /nuget

echo "-------- Build image with Nibbler --------"
nibbler \
	--from-image $dockerReg/dotnet/aspnet:$dotnetRuntimeTag \
	--to-image $dockerReg/$targetImage:$dotnetVersion \
	--add "publish:/home/app" \
	--git-labels \
	--workdir /home/app \
	--user app \
	--cmd "dotnet aspnetcore-new.dll" \
	--insecure \
	-v

echo "-------- Success building image with Nibbler! --------"
EOF

echo "-------- Pull and run built image locally at http://localhost:8080/ --------"
echo "-- remember to stop the running image: docker ps, docker stop nibbler-test-app"

docker pull localhost:5000/$targetImage:$dotnetVersion
docker run -d --rm -p 8080:8080 --name nibbler-test-app localhost:5000/$targetImage:$dotnetVersion
#docker ps -a -f name=nibbler-test-app

sleep 1
curl -s --fail http://localhost:8080
docker stop nibbler-test-app