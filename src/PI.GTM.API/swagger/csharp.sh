#!/bin/bash
rm -rd ../{Api,Client,Model,docs}
./clean.sh
./codegen-cli.sh generate -i /gen/swagger.json -o /gen/src -l csharp -c /gen/csharp.json
mv src/csharp/PI.GTM.API/* ..
mv src/docs ..
mv src/README.md ..
rm -rd src