#!/bin/bash
set -e

echo "-------- Prepair images --------"

docker pull gcr.io/kaniko-project/executor:latest
docker pull docker
docker pull registry.centos.org/dotnet/dotnet-22-centos7:latest
docker pull registry.centos.org/dotnet/dotnet-22-runtime-centos7:latest
docker tag registry.centos.org/dotnet/dotnet-22-runtime-centos7:latest localhost:5000/dotnet/dotnet-22-runtime-centos7:latest
docker push localhost:5000/dotnet/dotnet-22-runtime-centos7:latest

echo "-------- Build artifacts --------"

docker run --rm \
	--name dotnet-build \
	-v /$PWD:/opt/app-root/workspace \
	registry.centos.org/dotnet/dotnet-22-centos7:latest -- bash -c "../workspace/dotnet-build.sh"
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

cat << EOF | docker run -i --rm -v /$PWD:/opt/app-root/workspace registry.centos.org/dotnet/dotnet-22-centos7:latest bash
set -e

dotnet tool install -g Nibbler --version 1.0.0-beta.5 
echo "-------- Nibbler tool installed --------"
nibbler \
	--base-image host.docker.internal:5000/dotnet/dotnet-22-runtime-centos7:latest \
	--destination host.docker.internal:5000/nibbler-test:nibbler \
	--add "/opt/app-root/workspace/TestData/publish-docker:/opt/app-root/app" \
	--workdir /opt/app-root/app \
	--cmd "dotnet aspnetcore-new.dll" \
	--insecure \
	--debug
EOF

docker pull localhost:5000/nibbler-test:nibbler
docker run -d --rm -p 8082:8080 --name nibbler-test-app localhost:5000/nibbler-test:nibbler

echo "-------- Done! --------"
