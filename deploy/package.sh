#!/bin/bash
set -e

PACKAGE_DIR=package
VERSION=${GO_PIPELINE_LABEL:-beta}
APP_NAME=${APP_NAME}

rm -rf $PACKAGE_DIR
mkdir $PACKAGE_DIR

(cd ./deploy/terraform; zip -r ../../$PACKAGE_DIR/terraform.zip .)
aws s3 cp ./$PACKAGE_DIR/terraform.zip s3://zuto-build-artifacts/$APP_NAME/$VERSION/terraform.zip