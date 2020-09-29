#!/bin/bash
set -e

###################################
# Script that does end to end testing of
# creating a node project and building a image with Nibbler and running it
# 
###################################

NODE_IMAGE=node:12
TARGET_IMAGE=nibbler-node-test
BUILDER_IMAGE=dotnet-node-builder

echo "-------- Prepair images --------"
docker pull $NODE_IMAGE
docker tag $NODE_IMAGE localhost:5000/$NODE_IMAGE
docker push localhost:5000/$NODE_IMAGE

echo "-------- Create node builder image --------"
docker build -t $BUILDER_IMAGE -f Dockerfile.nodebuilder .

echo "-------- Create Nibbler nuget --------"
dotnet pack ../Nibbler -o ./nuget
NIBBLER_VERSION=$(minver -t v -v w)

echo "-------- Run build in docker image --------"

cat << EOF | docker run -i --rm -v /$PWD/nuget:/nuget -v /$PWD/node-app:/src $BUILDER_IMAGE bash
set -e
export PATH="\$PATH:/root/.dotnet/tools"

echo "-------- Build node app --------"
mkdir app
cd app
cp ../src/package.json .
cp ../src/*.js .
npm install
# create some symlinks
ln -s package.json link-to-file
ln -s node_modules link-to-folder

echo "-------- Install Nibbler --------"
dotnet tool install -g Nibbler --version ${NIBBLER_VERSION} --add-source /nuget

echo "-------- Build image with Nibbler --------"
nibbler \
	--from-image host.docker.internal:5000/$NODE_IMAGE \
	--to-image host.docker.internal:5000/$TARGET_IMAGE \
	--add "/app:/app" \
	--addFolder "/app:1001:1001:777" \
	--workdir /app \
	--cmd "node server.js" \
	--insecure \
	-v

echo "-------- Success building image with Nibbler! --------"
EOF

echo "-------- Pull and run built image locally at http://localhost:8085/ --------"
echo "-- remember to stop the running image: docker ps, docker stop nibbler-node-test-app"

docker pull localhost:5000/$TARGET_IMAGE
docker run -d --rm -p 8085:8080 --name nibbler-node-test-app localhost:5000/$TARGET_IMAGE
docker ps -a -f name=nibbler-node-test-app
