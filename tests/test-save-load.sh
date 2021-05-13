#!/bin/bash
set -e

buildImage=mcr.microsoft.com/dotnet/sdk:5.0
hubFromImage=nginx:latest
fromImage=nginx:latest
toImage=test-save-load
containerName=test-save-load

echo "-------- Prepair images --------"
docker pull $buildImage
docker pull $hubFromImage
docker tag $hubFromImage localhost:5000/$fromImage
docker push localhost:5000/$fromImage
docker image rm localhost:5000/$fromImage

echo "-------- Create Nibbler nuget --------"
dotnet pack ../Nibbler -o ./nuget
NIBBLER_VERSION=$(minver -t v -v e)

echo "-------- Run build in docker image --------"

cat << EOF | docker run -i --rm -v /$PWD/nuget:/nuget $buildImage bash
set -e
export PATH="\$PATH:/root/.dotnet/tools"

echo "-------- Install Nibbler --------"
dotnet tool install -g Nibbler --version ${NIBBLER_VERSION} --add-source /nuget

echo "-------- Create content --------"
mkdir stage1
STAGE_1_TIME=\$(date -Ins)
echo "test-save-load <a href=\"stage1.html\">stage1</a>, <a href=\"stage2.html\">stage2</a><br>\$STAGE_1_TIME" > stage1/index.html
echo "stage1! \$STAGE_1_TIME" > stage1/stage1.html

echo "-------- Build image with Nibbler to file --------"
nibbler \
	--from-image host.docker.internal:5000/$fromImage \
    --from-insecure \
	--to-file image \
	--add "stage1:/usr/share/nginx/html" \
	-v

echo "-------- List file contents --------"
ls -l image/

echo "-------- Add content --------"

mkdir stage2
STAGE_2_TIME=\$(date -Ins)
echo "stage2! \$STAGE_2_TIME" > stage2/stage2.html

echo "-------- Build image with Nibbler from file --------"
nibbler \
	--from-file image \
	--to-image host.docker.internal:5000/$toImage \
	--to-insecure \
	--add "stage2:/usr/share/nginx/html" \
	-v

echo "-------- Success building image with Nibbler! --------"
EOF

echo "-------- Pull and run built image locally at http://localhost:8080/ --------"
echo "-- remember to stop the running image: docker ps, docker stop $containerName"

docker pull localhost:5000/$toImage
docker run -d --rm -p 8080:80 --name $containerName localhost:5000/$toImage
docker ps -a -f name=$containerName
