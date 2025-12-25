<div align="center">

# [SwiftlyS2] ThirdPerson

[![GitHub Release](https://img.shields.io/github/v/release/criskkky/sws2-thirdperson?color=FFFFFF&style=flat-square)](https://github.com/criskkky/sws2-thirdperson/releases/latest)
[![GitHub Issues](https://img.shields.io/github/issues/criskkky/sws2-thirdperson?color=FF0000&style=flat-square)](https://github.com/criskkky/sws2-thirdperson/issues)
[![GitHub Downloads](https://img.shields.io/github/downloads/criskkky/sws2-thirdperson/total?color=blue&style=flat-square)](https://github.com/criskkky/sws2-thirdperson/releases)
[![GitHub Stars](https://img.shields.io/github/stars/criskkky/sws2-thirdperson?style=social)](https://github.com/criskkky/sws2-thirdperson/stargazers)<br/>
  <sub>Made with ❤️ by <a href="https://github.com/criskkky" rel="noopener noreferrer" target="_blank">criskkky</a></sub>
  <br/>
</div>

## Overview

> [!WARNING]
> **ACK**: Due to engine limitations (I guess), certain attack actions (shooting or knifing) may be performed from the camera view instead of the player's actual position. Address this problem by switching between available damage modes in the configuration.

ThirdPerson is a plugin for SwiftlyS2 that provides third-person camera functionality for Counter-Strike 2. It allows players to switch between first-person and third-person views with customizable camera settings, damage blocking options, and smooth camera movement. The plugin includes permission-based access controls and supports both instant and smooth camera transitions.

## Download Shortcuts
<ul>
  <li>
    <code>📦</code>
    <strong>&nbspDownload Latest Plugin Version</strong> ⇢
    <a href="https://github.com/criskkky/sws2-thirdperson/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a>
  </li>
  <li>
    <code>⚙️</code>
    <strong>&nbspDownload Latest SwiftlyS2 Version</strong> ⇢
    <a href="https://github.com/swiftly-solution/swiftlys2/releases/latest" target="_blank" rel="noopener noreferrer">Click Here</a>
  </li>
</ul>

## Features
- **Third-Person Toggle**: Players can toggle third-person view on/off using configurable commands.
- **Dual Camera Modes**: Support for both instant camera positioning and smooth interpolated movement.
- **Configurable Damage Blocking**: Choose between blocking damage from behind ("back") or disabling all damage ("none") in third-person mode.
- **Permission-Based Access**: Control who can use third-person functionality with customizable permissions.
- **Customizable Camera Settings**: Adjust camera distance, height, and smoothing parameters.
- **Real-time Updates**: Camera positions update smoothly every server tick.
- **Multi-language Support**: Some translations included.

## Screenshots

![Screenshot 1](assets/Screenshot_1.png)

## Plugin Setup
> [!WARNING]
> Make sure you **have installed SwiftlyS2 Framework** before proceeding.

1. Download and extract the latest plugin version into your `swiftlys2/plugins` folder.
2. Perform an initial run in order to allow file generation.
3. Generated file will be located at: `swiftlys2/configs/plugins/ThirdPerson/config.jsonc`
4. Edit the configuration file as needed.
5. Configure permissions in your server's `permissions.jsonc` file.
6. Enjoy!

## Configuration Guide

| Option | Type | Example | Description |
|--------|------|---------|-------------|
| CustomTPCommand | string | `"tp"` | Custom command to toggle third-person view. Default: `"tp"`. |
| Permission | string | `"thirdperson.use"` | Permission required to use third-person functionality. Leave empty to allow all players. |
| DamageMode | string | `"back"` | Damage blocking mode: `"back"` (block damage dealt from behind your character) or `"none"` (block all damage). |
| ThirdPersonDistance | float | `100.0` | Distance of the third-person camera from the player. |
| ThirdPersonHeight | float | `20.0` | Height offset of the third-person camera. |
| SmoothCameraEnabled | boolean | `true` | Enable smooth camera movement interpolation. |
| SmoothCameraSpeed | float | `5.0` | Speed of smooth camera movement (higher = faster). |
| EnableKnifeWarnings | boolean | `true` | Enable warnings when players try to knife attack in third-person view. |

## Permissions

ThirdPerson uses permission-based access control:

- If `Permission` is empty, all players can use third-person functionality.
- If `Permission` is set, only players with that permission will be able to toggle third-person view.

For more details on configuring permissions, see the [SwiftlyS2 permissions documentation](https://swiftlys2.net/docs/development/permissions/#configuration) and make sure to update your server's `permissions.jsonc` file.

## Backend Logic (How It Works)
1. When a player uses the third-person command, the plugin creates a camera entity positioned behind the player.
2. Camera positioning uses raycasting to avoid clipping through walls and provides collision detection.
3. Damage blocking is implemented through entity damage hooks, preventing damage based on the configured mode.
4. Smooth camera movement uses linear interpolation for fluid transitions between positions.
5. The plugin hooks into game events (round start, player death, disconnect) for intelligent resource cleanup.
6. Permission checks ensure only authorized players can access third-person functionality.

## Support and Feedback
Feel free to [open an issue](https://github.com/criskkky/sws2-thirdperson/issues/new/choose) for any bugs or feature requests. If it's all working fine, consider starring the repository to show your support!

## Contribution Guidelines
Contributions are welcome only if they align with the plugin's purpose. For major changes, please open an issue first to discuss what you would like to change.
