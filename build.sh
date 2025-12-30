#!/usr/bin/env bash
set -euo pipefail

# Defaults
TARGET="Debug"
FRAMEWORK="net40"
OUTPUT_DIRECTORY="$(pwd)/build"

# Supported options
SUPPORTED_TARGETS=("Debug" "Release")
SUPPORTED_FRAMEWORKS=("net40" "netstandard2.0")

# Solution mapping
declare -A SOLUTION_MAPPING=(
    ["net40"]="librespot-csharp.sln"
    ["netstandard2.0"]="librespot-csharp.NetStandard.sln"
)

# Usage function
usage() {
    echo "Usage: $0 [-t <Debug|Release>] [-f <net40|netstandard2.0>] [-o <output-directory>]"
    exit 1
}

# Parse arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        -h|--help)
            usage
            ;;
        -t|--target)
            TARGET="$2"
            shift 2
            ;;
        -f|--framework)
            FRAMEWORK="$2"
            shift 2
            ;;
        -o|--output-directory)
            OUTPUT_DIRECTORY="$2"
            shift 2
            ;;
        -*)
            echo "Unknown option: $1"
            usage
            ;;
        *)
            usage
            ;;
    esac
done

# Validate target
if [[ ! " ${SUPPORTED_TARGETS[*]} " =~ " ${TARGET} " ]]; then
    echo "Error: Unsupported target '${TARGET}'"
    usage
fi

# Validate framework
if [[ ! " ${SUPPORTED_FRAMEWORKS[*]} " =~ " ${FRAMEWORK} " ]]; then
    echo "Error: Unsupported framework '${FRAMEWORK}'"
    usage
fi

# Ensure output directory exists
mkdir -p "$OUTPUT_DIRECTORY"

# Run dotnet build
dotnet build "${SOLUTION_MAPPING[$FRAMEWORK]}" \
    --framework "$FRAMEWORK" \
    --configuration "$TARGET" \
    --output "$OUTPUT_DIRECTORY"
