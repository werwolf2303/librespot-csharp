#!/bin/bash

export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

~/.dotnet/tools/protogen --proto_path=proto --csharp_out=protogens +langver=4.0 *.proto 