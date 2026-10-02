#!/usr/bin/env python3
"""Compile and test the exact Class::Init production region without game startup."""
import argparse
from pathlib import Path
import subprocess
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--dotnet", required=True, help=".NET 9 SDK executable")
parser.add_argument("--x64-dotnet", help="Optional macOS x64 .NET 9 runtime for Rosetta checks")
args = parser.parse_args()
test_dir = Path(__file__).resolve().parent
source = test_dir.parents[1] / "Il2CppInterop.Runtime/Injection/InjectorHelpers.cs"
text = source.read_text(encoding="utf-8-sig")
marker = "#region Class::Init"
if text.count(marker) != 1:
    raise SystemExit("Expected exactly one production Class::Init region")
region = text.split(marker, 1)[1].split("#endregion", 1)[0]
with tempfile.TemporaryDirectory(prefix="il2cpp-class-init-") as directory:
    scratch = Path(directory)
    (scratch / "Fixture.cs").write_text((test_dir / "class-init-fixture.cs").read_text())
    (scratch / "Production.cs").write_text(
        "using System; using System.Linq; using System.Runtime.InteropServices;\n"
        "internal static unsafe partial class InjectorHelpers {\n" + region + "\n}\n")
    (scratch / "ClassInitRegression.csproj").write_text(
        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
        '<TargetFramework>net9.0</TargetFramework><OutputType>Exe</OutputType>'
        '<AllowUnsafeBlocks>true</AllowUnsafeBlocks><UseAppHost>false</UseAppHost>'
        '</PropertyGroup></Project>')
    (scratch / "NuGet.config").write_text(
        '<configuration><packageSources><clear /></packageSources></configuration>')
    subprocess.run([args.dotnet, "build", "--nologo", "-o", str(scratch / "out")],
                   cwd=scratch, check=True)
    assembly = str(scratch / "out/ClassInitRegression.dll")
    subprocess.run([args.dotnet, assembly], cwd=scratch, check=True)
    if args.x64_dotnet:
        subprocess.run(["arch", "-x86_64", args.x64_dotnet, assembly], cwd=scratch, check=True)
