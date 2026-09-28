# Square Map

[![ci](https://img.shields.io/github/actions/workflow/status/linusfr/ffxiv-square-map/ci.yml?branch=main&label=ci&cacheSeconds=300)](https://github.com/linusfr/ffxiv-square-map/actions/workflows/ci.yml)
[![release](https://img.shields.io/github/v/release/linusfr/ffxiv-square-map?label=release&cacheSeconds=300)](https://github.com/linusfr/ffxiv-square-map/releases/latest)
[![licence](https://img.shields.io/github/license/linusfr/ffxiv-square-map?color=blue)](LICENSE)

> A cleaner, square native minimap.

Square Map reshapes FFXIV's minimap and collects its visual cleanup controls in
one place. The native map, markers, rotation, and interactions remain intact.

## Install

### Required dependency: Penumbra

Square Map requires Penumbra for safe game-resource redirection. In
`/xlsettings` → **Experimental** → Custom Plugin Repositories, paste this URL,
click `+`, then click the **save** icon:

```text
https://raw.githubusercontent.com/xivdev/Penumbra/master/repo.json
```

Then open `/xlplugins`, search for **Penumbra**, install it, and restart the
game once. Penumbra is required; without it, Square Map cannot apply its visual
mask.

### Square Map repository

Use the same procedure to add the Square Map repository:

```text
https://raw.githubusercontent.com/linusfr/ffxiv-square-map/main/pluginmaster.json
```

Then open `/xlplugins`, search for **Square Map**, and install it. Square Map
registers its bundled mask as a temporary Penumbra mod; it does not install or
alter persistent Penumbra mods.

`/squaremap` opens the settings and `/squaremap toggle` turns the changes off
and on. A relog may be needed after changing the mask option because UI textures
can remain cached by the game.

Do not enable another plugin's minimap adjustments at the same time; plugins
writing the same native UI nodes can fight each other.

## Features

- Square or rectangular visual mask with matching collision area
- Independent viewport width and height up to 200%, with a linked square mode
- Up to 500% additional range beyond the native maximum zoom-out level,
  without changing native zoom-in levels
- Native-style square border with fixed thickness, or a borderless mode
- Individual controls for coordinates, compass lock, cardinal directions, zoom
  buttons, sun, and weather
- Restores the original node state when disabled or unloaded

## Building

```sh
just install
```

## Licence

MIT.
