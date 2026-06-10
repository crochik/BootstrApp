#!/bin/bash

DEVROOT=/Users/felipe/DEVELOPMENT/github/

cd $DEVROOT/SchedOnl/PI.OpenAPI

#curl -X 'GET' \
#  'http://localhost:5025/openapi/v1/Generate/Profile/33d883d8-c153-44b2-9094-d4fd158e767d' \
#  -H 'accept: */*' \
#  -H 'Authorization: Bearer Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6ImF0K2p3dCJ9.eyJuYmYiOjE3NzA4Mjc5MzMsImV4cCI6MTc3MDg2MzkzMywiaXNzIjoiaHR0cHM6Ly9pZHAuZmNpLmNsb3VkIiwiYXVkIjoiU2NoZWQuT25sIiwiY2xpZW50X2lkIjoicGkuZmNpLmNsb3VkIiwic3ViIjoiNWNlYjk5ZDctODU5MS00NTAyLWI2ZTItNDI4NWRkNzczZTE0IiwiYXV0aF90aW1lIjoxNzcwMjQ0NjI5LCJpZHAiOiJTYWxlc2ZvcmNlIiwibmFtZSI6IkZlbGlwZSBDcm9jaGlrIiwiZ2l2ZW5fbmFtZSI6IkZlbGlwZSIsImZhbWlseV9uYW1lIjoiQ3JvY2hpayIsImVtYWlsIjoiZmVsaXBlQGNyb2NoaWsuY29tIiwicm9sZSI6IkFkbWluIiwicGlfYWNjb3VudF9pZCI6ImZjMTAwMDAwLTAwMDAtMDAwMC0wMDAwLTAwMDAwMDAwMDAwMCIsInBpX3Byb2ZpbGVfaWQiOiJkMzNkMmRkYTUxODA0NGE1OWU2Mzc3NDk0ZjU3NjQ2YSIsIngtc2FsZXNmb3JjZS11c2VyaWQiOiIwMDUxTDAwMDAwQkJteUxRQVQiLCJqdGkiOiIwMURERDE0RDk2NDJGOERFN0VBQzhCMEIyNEIyM0RGOSIsImlhdCI6MTc3MDgyNzkzMywic2NvcGUiOlsib3BlbmlkIiwicHJvZmlsZSIsImFwaSIsInJlc3QiLCJvZmZsaW5lX2FjY2VzcyJdLCJhbXIiOlsiZXh0ZXJuYWwiXX0.JXwKzx4VnSkp_ckw5_TZBp3u4F-Bs1-uzc4cy--FtP8ZFCsCyzUc_izKYoNVg5tDF7FujtqEohgfJBAE_PHeKNHN9FAasn07Ocn5Wfq_RbNfrWXb79CpjEzMrVoqBTr_w-dD0V6P4jwyhQW7zIbruJOhcjFEErvFbRSpbVy7kO8iukoSC_wk5JSFoSJq_FtzjXWDJ_Y9w9TSY7gZC0GDg9a8-vxbJRIgPzJ7i_kA0MebLZdTlk1PujwP7H3Rzw4XM0bA_jgzkNelfRa0iDqRyJPiUKx_Iw_gR5ALHxO5TS_Nl3N6P5JSIBQV0xhIwLon9tekgF0VfZNCQ6muocgU_w' \
#  -o openapi.yaml

rm -rd $DEVROOT/custom-openapi-client-generator/output/ts/pi_api_2/*
rm -rd $DEVROOT/rich-editor/packages/pi_api_2
    
cp "openapi.yaml" "$DEVROOT/custom-openapi-client-generator/samples/openapi.yaml"

cd $DEVROOT/custom-openapi-client-generator
./build-pi-api-2-ts.sh

cd $DEVROOT/rich-editor/packages
cp -R $DEVROOT/custom-openapi-client-generator/output/ts/pi_api_2 pi_api_2
cp "$DEVROOT/SchedOnl/openapi.yaml" "$DEVROOT/rich-editor/packages/pi_api_2"
