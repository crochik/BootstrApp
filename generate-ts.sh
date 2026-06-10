#!/bin/bash

DEVROOT=/Users/felipe/DEVELOPMENT/github/

cd $DEVROOT/SchedOnl/PI.OpenAPI

curl -X 'GET' \
  'https://api.fci.cloud/api/swagger/API/swagger.json' \
  -H 'accept: */*' \
  -o pi-api.json

rm -rd $DEVROOT/custom-openapi-client-generator/output/ts/pi_api/*
rm -rd $DEVROOT/rich-editor/packages/pi_api
    
cp "pi-api.json" "$DEVROOT/custom-openapi-client-generator/samples/pi-api.json"

cd $DEVROOT/custom-openapi-client-generator
./build-pi-api-ts.sh

cd $DEVROOT/rich-editor/packages
cp -R $DEVROOT/custom-openapi-client-generator/output/ts/pi_api pi_api
cp "$DEVROOT/SchedOnl/PI.OpenAPI/pi-api.json" "$DEVROOT/rich-editor/packages/pi_api"
