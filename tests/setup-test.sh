#!/bin/bash
set -e

docker pull registry:2
docker run -d --rm -p 5000:5000 --name registry registry:2

docker pull mcr.microsoft.com/dotnet/aspnet:9.0
docker tag mcr.microsoft.com/dotnet/aspnet:9.0 localhost:5000/dotnet/aspnet:9.0 
docker push localhost:5000/dotnet/aspnet:9.0 
docker image rm localhost:5000/dotnet/aspnet:9.0 
docker pull hello-world:latest
docker tag hello-world:latest localhost:5000/hello-world:latest
docker push localhost:5000/hello-world:latest
docker image rm localhost:5000/hello-world:latest

rm -rf TestData
mkdir TestData

curl -L https://mcr.microsoft.com/v2/dotnet/core/aspnet/blobs/sha256:9526604e089d8d4aa947f34e52a14ac8793232dd181022932f3c15291c5cd3af -o TestData/test.tar.gz

cd TestData
dotnet new webapp
dotnet new gitignore
dotnet publish -o ./publish