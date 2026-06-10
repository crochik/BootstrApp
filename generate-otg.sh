#!/bin/bash

cd PI.OpenAPI

curl -X 'GET' \
  'http://localhost:5025/openapi/v1/Generate/Profile/33d883d8-c153-44b2-9094-d4fd158e767d' \
  -H 'accept: */*' \
  -H 'Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6ImF0K2p3dCJ9.eyJuYmYiOjE3ODA5NTMzNDMsImV4cCI6MTc4MDk4OTM0MywiaXNzIjoiaHR0cHM6Ly9pZHAuZmNpLmNsb3VkIiwiYXVkIjoiU2NoZWQuT25sIiwiY2xpZW50X2lkIjoicGkuZmNpLmNsb3VkIiwic3ViIjoiNWNlYjk5ZDctODU5MS00NTAyLWI2ZTItNDI4NWRkNzczZTE0IiwiYXV0aF90aW1lIjoxNzc4NTI0MDQwLCJpZHAiOiJTYWxlc2ZvcmNlIiwibmFtZSI6IkZlbGlwZSBDcm9jaGlrIiwiZ2l2ZW5fbmFtZSI6IkZlbGlwZSIsImZhbWlseV9uYW1lIjoiQ3JvY2hpayIsImVtYWlsIjoiZmVsaXBlQGNyb2NoaWsuY29tIiwicm9sZSI6IkFkbWluIiwicGlfYWNjb3VudF9pZCI6ImZjMTAwMDAwLTAwMDAtMDAwMC0wMDAwLTAwMDAwMDAwMDAwMCIsInBpX3Byb2ZpbGVfaWQiOiJkMzNkMmRkYTUxODA0NGE1OWU2Mzc3NDk0ZjU3NjQ2YSIsIngtc2FsZXNmb3JjZS11c2VyaWQiOiIwMDUxTDAwMDAwQkJteUxRQVQiLCJqdGkiOiJDRkY1QjBERDE2NjlGQTMwMTYzOTg3QzVBREE1RDIxMCIsImlhdCI6MTc4MDk1MzM0Mywic2NvcGUiOlsib3BlbmlkIiwicHJvZmlsZSIsImFwaSIsInJlc3QiLCJvZmZsaW5lX2FjY2VzcyJdLCJhbXIiOlsiZXh0ZXJuYWwiXX0.VPibx4AJ4q952ziNg57gc_WMjrIsIDoLYXOsd4JyyuQwIiqEwT9gxTYYF9kfjlaYi6_jfz7xpHW145pB2E8fckdGGGMNEJCgy1eJRdQKrB3YRX__07mSuwcSVJDB3xORKP9VwfwWSH0vYNd7Drn80jY-1WqgxQ_3fqwEAXRPOqlHPciKenbQ6uUbZ5llNpr5n7_OExfmiuZtYcjosIVCO3zVpKWAkz1ZN9_h0YheKUTthpR-rlswyBGSRe5Jl2J9mNSAmLLTd0x2gdd0r1FExhCf82q8_9IoyDyXLRI6bDTB4xDT44D_21OaiQ1aalmiBGkTsxKLHjMgDvU8LZrzvA' \
  -o openapi.yaml

mkdir -p  /Users/felipe/DEVELOPMENT/github/custom-openapi-client-generator/output/pi_api_2
rm -rd /Users/felipe/DEVELOPMENT/github/custom-openapi-client-generator/output/pi_api_2/*
rm -rd /Users/felipe/DEVELOPMENT/github/pi-flutter/pi_api_2/*
    
cp "openapi.yaml" "/Users/felipe/DEVELOPMENT/github/custom-openapi-client-generator/samples/openapi.yaml"

cd /Users/felipe/DEVELOPMENT/github/custom-openapi-client-generator
./test-pi.sh

cd /Users/felipe/DEVELOPMENT/github/pi-flutter
cp -R /Users/felipe/DEVELOPMENT/github/custom-openapi-client-generator/output/pi_api_2 .
cp "../SchedOnl/PI.OpenAPI/openapi.yaml" "/Users/felipe/DEVELOPMENT/github/custom-openapi-client-generator/output/pi_api_2/openapi.yaml"
