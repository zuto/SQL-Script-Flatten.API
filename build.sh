#!/bin/bash
set -e

IMAGE_NAME=demo-api
BASE_REPO=525344447431.dkr.ecr.eu-west-2.amazonaws.com
VERSION=${GO_PIPELINE_LABEL:-beta}

aws ecr get-login-password --region eu-west-2 | docker login --username AWS --password-stdin $BASE_REPO
sleep 1

docker build -t $IMAGE_NAME:latest --rm .
docker tag $IMAGE_NAME:latest $BASE_REPO/$IMAGE_NAME:latest
docker tag $IMAGE_NAME:latest $BASE_REPO/$IMAGE_NAME:$VERSION
docker push $BASE_REPO/$IMAGE_NAME:$VERSION
docker push $BASE_REPO/$IMAGE_NAME:latest
