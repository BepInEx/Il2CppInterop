#!/bin/sh
set -eu

root=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
output=${TMPDIR:-/tmp}/il2cpp-interop-enum-regression-$$
trap 'rm -f "$output"' EXIT HUP INT TERM

cc -std=c11 -Wall -Wextra -Werror "$root/tests/EnumDefaultValueRegression/native_decoder.c" -o "$output"
"${DOTNET:-dotnet}" run --project "$root/tests/EnumDefaultValueRegression" | "$output"
