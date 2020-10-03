#!/bin/bash
set -e

###################################
# Script build 3 images with kaniko, docker and nibbler
# Containers are stared.
# Used with ImageCompareTest to look at image differences
###################################

echo "-------- Prepair images --------"

docker pull gcr.io/kaniko-project/executor:latest
docker pull docker
docker pull registry.centos.org/dotnet/dotnet-31-centos7:latest
docker pull registry.centos.org/dotnet/dotnet-31-runtime-centos7:latest
docker tag registry.centos.org/dotnet/dotnet-31-runtime-centos7:latest localhost:5000/dotnet/dotnet-31-runtime-centos7:latest
docker push localhost:5000/dotnet/dotnet-31-runtime-centos7:latest
docker image rm localhost:5000/dotnet/dotnet-31-runtime-centos7:latest

echo "-------- Build artifacts --------"

echo "warning: when running on windows the file dotnet-build.sh might be mounted with wrong line endings"
docker run --rm \
	--name dotnet-build \
	-v /$PWD:/opt/app-root/workspace \
	registry.centos.org/dotnet/dotnet-31-centos7:latest -- bash -c "../workspace/dotnet-build.sh"
cp Dockerfile TestData/publish-docker

echo "-------- Kaniko build --------"

docker run --rm \
	--name kaniko-build \
	-v /$PWD/TestData/publish-docker:/workspace \
	gcr.io/kaniko-project/executor:latest \
	--context=//workspace \
	--destination=host.docker.internal:5000/nibbler-test:kaniko \
	--insecure

docker pull localhost:5000/nibbler-test:kaniko
docker run -d --rm -p 8081:8080 --name kaniko-test-app localhost:5000/nibbler-test:kaniko

echo "-------- Docker build --------"

docker run --rm \
	--name docker-build \
	-v //var/run/docker.sock:/var/run/docker.sock \
	-v /$PWD:/workspace \
	docker:latest sh -c "cd /workspace/TestData/publish-docker && docker build -t nibbler-test:docker ."

docker tag nibbler-test:docker localhost:5000/nibbler-test:docker
docker push localhost:5000/nibbler-test:docker
docker run -d --rm -p 8080:8080 --name docker-test-app nibbler-test:docker

echo "-------- Nibbler build --------"

echo "-------- Create Nibbler nuget --------"
dotnet pack ../Nibbler -o ./nuget 
NIBBLER_VERSION=$(minver -t v -v w)

cat << EOF | docker run -i --rm -v /$PWD:/opt/app-root/workspace registry.centos.org/dotnet/dotnet-31-centos7:latest bash
set -e

dotnet tool install -g Nibbler --version ${NIBBLER_VERSION} --add-source /opt/app-root/workspace/nuget
echo "-------- Nibbler tool installed --------"
nibbler \
	--from-image host.docker.internal:5000/dotnet/dotnet-31-runtime-centos7:latest \
	--to-image host.docker.internal:5000/nibbler-test:nibbler \
	--add "/opt/app-root/workspace/TestData/publish-docker:/opt/app-root/app" \
	--workdir /opt/app-root/app \
	--cmd "dotnet aspnetcore-new.dll" \
	--insecure \
	-v
EOF

docker pull localhost:5000/nibbler-test:nibbler
docker run -d --rm -p 8082:8080 --name nibbler-test-app localhost:5000/nibbler-test:nibbler

echo "-------- Done! --------"

echo "-> test apps running:"
echo "  kaniko-test-app: http://localhost:8081"
echo "  docker-test-app: http://localhost:8080"
echo "  nibbler-test-app: http://localhost:8082"
