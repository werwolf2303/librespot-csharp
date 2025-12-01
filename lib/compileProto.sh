#!/bin/bash

export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

mkdir -p protogens

find "proto" -type f -name "*.proto" | sed 's|^proto/||' | xargs -I {} ~/.dotnet/tools/protogen --proto_path=proto --csharp_out=protogens {}
