#!/usr/bin/env just --justfile

@default:
    just --list

plugin := "plugin/SquareMap.csproj"
dev_plugins := env_var('HOME') / ".xlcore/devPlugins/SquareMap"
dalamud_home := env_var('HOME') / ".xlcore/dalamud/Hooks/dev"
dotnet := "nix shell nixpkgs#dotnet-sdk_10 -c env DALAMUD_HOME=" + dalamud_home + " DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 dotnet"

restore:
    {{dotnet}} restore {{plugin}}

build-plugin: restore
    {{dotnet}} build {{plugin}} --configuration Debug --no-restore

build-release: restore
    {{dotnet}} build {{plugin}} --configuration Release --no-restore

install: build-plugin
    rm -rf {{dev_plugins}}
    mkdir -p {{dev_plugins}}
    cp -r plugin/bin/Debug/. {{dev_plugins}}/
    @echo "Installed to {{dev_plugins}}."
    @echo "Now: /xlplugins -> Dev Tools -> reload, or restart the game."

fmt:
    {{dotnet}} format style {{plugin}}
    {{dotnet}} format analyzers {{plugin}}

fmt-check:
    {{dotnet}} format style {{plugin}} --verify-no-changes
    {{dotnet}} format analyzers {{plugin}} --verify-no-changes

check: fmt-check build-plugin
    prek run --all-files

hooks:
    prek install --install-hooks
    prek install --hook-type commit-msg

clean:
    rm -rf dist plugin/bin plugin/obj SquareMap.zip pack/
