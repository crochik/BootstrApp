#!/bin/bash
./clean.sh
mkdir -p src
./codegen-cli.sh generate -i /gen/swagger.json -o /gen/src -l $1 -c /gen/$1.json
cd src
npm install
npm run build
npm pack
cp *.tgz ..