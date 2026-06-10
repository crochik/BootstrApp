#!/bin/bash   
APP="gtm"
IMAGE="docker.fci.cloud/${APP}"
TAG="012519"

rm -rd out

echo "Publishing: ${IMAGE} - ${TAG}"
dotnet publish -c Release -o out -r linux-x64 --self-contained
docker build --rm -f "Dockerfile" -t ${IMAGE}:${TAG} .
docker push ${IMAGE}:${TAG}

echo "----------------------------------------------"
echo "kubectl set image deployment/${APP} ${APP}=${IMAGE}:${TAG}"
kubectl set image deployment/${APP} ${APP}=${IMAGE}:${TAG}
echo "----------------------------------------------"
