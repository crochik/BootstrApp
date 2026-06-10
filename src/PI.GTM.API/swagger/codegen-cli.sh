#!/bin/bash
docker run --rm -it \
        -w /src \
        -v "${PWD}:/gen" \
        swaggerapi/swagger-codegen-cli \
        $*
