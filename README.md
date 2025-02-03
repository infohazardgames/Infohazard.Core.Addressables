# Infohazard.Core.Addressables Documentation

## Table of Contents

- [Infohazard.Core.Addressables Documentation](#infohazardcoreaddressables-documentation)
  - [Table of Contents](#table-of-contents)
  - [Introduction](#introduction)
  - [Documentation and Support](#documentation-and-support)
  - [License](#license)
  - [Installation](#installation)
    - [Method 1 - Package Manager](#method-1---package-manager)
    - [Method 2 - Git Submodule](#method-2---git-submodule)
    - [Method 3 - Add To Assets](#method-3---add-to-assets)
    - [Method 4 - Asset Store](#method-4---asset-store)
  - [Setup](#setup)
    - [General Setup](#general-setup)
  - [Demo Scene](#demo-scene)
  - [Features Guide](#features-guide)
    - [AddressableSpawnRef](#addressablespawnref)
    - [AdddressableUtil](#adddressableutil)
    - [TimeToLiveAddressable](#timetoliveaddressable)
  - [Addendum: Avoiding Spawned Prefab Unloading Issue](#addendum-avoiding-spawned-prefab-unloading-issue)

## Introduction

Infohazard.Core.Addressables is an extension for [Infohazard.Core](https://github.com/infohazardgames/Infohazard.Core) which adds support in the pooling system for [Addressables](https://docs.unity3d.com/Manual/com.unity.addressables.html). It is kept separate so that you can use Infohazard.Core without having the Addressables package imported.

In addition to adding pooling support, this package makes it much easier to use Addressable prefabs. When using the pakage, you no longer have to keep track of when to unload an Addressable asset. Instead, you just release individual references when you are done with them, and the asset will be unloaded once all references are released and all instances are despawned.

## Documentation and Support

[API Docs](https://www.infohazardgames.com/docs/Infohazard.Core.Addressables/html/)

[Discord](https://discord.gg/V2jTnpS8zZ)

## License

If Infohazard.Core is acquired from the Unity Asset Store, you must follow the Unity Asset Store license.
The open-source repository uses the [MIT license](https://opensource.org/licenses/MIT).
You are welcome to have your own packages or assets depend on this package.

## Installation

### Method 1 - Package Manager

Using the Package Manager is the easiest way to install the package to your project. Simply install the project as a git URL. Note that if you go this route, you will not be able to make any edits to the package.

1. Install [UniTask](https://github.com/Cysharp/UniTask), through Package Manager or .unitypackage file.
2. Ensure [Infohazard.Core](https://github.com/infohazardgames/Infohazard.Core) is installed.
3. In Unity, open the Package Manager (Window > Package Manager).
4. Click the '+' button in the top right of the window.
5. Click "Add package from git URL...".
6. Paste in `https://github.com/infohazardgames/Infohazard.Core.Addressables.git`.
7. Click Add.

### Method 2 - Git Submodule

Using a git submodule is an option if you are using git for your project source control. This method will enable you to make changes to the package, but those changes will need to be tracked in a separate git repository.

1. Install [UniTask](https://github.com/Cysharp/UniTask), through Package Manager or .unitypackage file.
2. Ensure [Infohazard.Core](https://github.com/infohazardgames/Infohazard.Core) is installed.
3. Close the Unity Editor.
4. Using your preferred git client or the command line, add `https://github.com/infohazardgames/Infohazard.Core.Addressables.git` as a submodule in your project's Packages folder.
5. Re-open the Unity Editor.

If you wish to make changes when you use this method, you'll need to fork the package repo. Once you've made your changes, you can submit a pull request to get those changes merged back to this repository if you wish.

1. Fork this repository. Open your newly created fork, and copy the git URL.
2. In your project's Packages folder, open the package repository.
3. Change the `origin` remote to the copied URL.
4. Make your changes, commit, and push.
5. (Optional) Open your fork again, and create a pull request.

### Method 3 - Add To Assets

If you wish to make changes to the library without dealing with a git submodule (or you aren't using git), you can simply copy the files into your project's Assets folder.

1. Install [UniTask](https://github.com/Cysharp/UniTask), through Package Manager or .unitypackage file.
2. Ensure [Infohazard.Core](https://github.com/infohazardgames/Infohazard.Core) is installed.
3. In the main page for this repo, click on Code > Download Zip.
4. Extract the zip on your computer.
5. Make a Infohazard.Core.Addressables folder under your project's Assets folder.
6. Copy the `Editor` and `Runtime` folders from the extracted zip to the newly created folder.

### Method 4 - Asset Store

If you downloaded [Infohazard.Core](https://assetstore.unity.com/packages/add-ons/infohazard-core-utility-library-235104) from the asset store, this package is included as an inner unitypackage file. Simply open the package (`Assets/Plugins/Infohazard/Infohazard.Core/Integrations/Infohazard.Core.Addressables.unitypackage`) to add it. You will also need to install [UniTask](https://github.com/Cysharp/UniTask), through Package Manager or .unitypackage file.

## Setup

### General Setup

The only setup required beyond installation is to add references to the Infohazard.Core.Addressables assembly if you are using an assembly definition. If you are using the default assemblies (such as Assembly-CSharp), nothing is needed here.

## Demo Scene

The demo scene is provided to demonstrate the usage of Infohazard.Core.Addressables. It is located at `Assets/Plugins/Infohazard/Demos/Infohazard.Core.Addressables/Scenes/Demo_PoolingAndTiming_Addressables.unity`. This demo shows how you can implement the same functionality as the Infohazard.Core pooling demo using Addressables.

For the demo to work, you need to mark the following two prefabs as addressable:
 - `Assets/Plugins/Infohazard/Demos/Infohazard.Core.Addressables/Prefabs/PooledCubeAddressable.prefab`
 - `Assets/Plugins/Infohazard/Demos/Infohazard.Core.Addressables/Prefabs/PooledExplosionAddressable.prefab`

This is because Unity does not save the addressable state of assets in a package.

## Features Guide

### AddressableSpawnRef

The `AddressableSpawnRef` serializable class provides a convenient way to use the Addressable pooling system. You can add a serialized field of this type to your script to make the prefab reference assignable in the inspector, then use it in your code very similarly to an Infohazard.Core SpawnRef.

In your code, you must first call `Retain()` (or one of the async/blocking variants) to load the Addressable, then call `Spawn()` in order to spawn it. Finally, call `Release()` when you no longer need to use that specific `AddressableSpawnRef`. The system will internally keep the Addressable asset loaded until all instances are despawned AND all `AddressableSpawnRef`s have been released. This way, you don't have to worry about when to unload an Addressable - only when you don't need this specific reference to it. Additionally, as long as at least one `AddressableSpawnRef` is retained, the prefab will not be unloaded even if there are no spawned instances.

### AdddressableUtil

The `AddressableUtil` class provides static methods for spawning Addressable prefabs using the pooling system. It provides both synchronous and asynchronous methods for spawning Addressable prefabs based on a key (which can be a GUID, path, or AssetReference).

Unlike with `AddressableSpawnRef`, when using `AddressableUtil`, the prefab will not be loaded until it is spawned the first time, and will be unloaded as soon as the last instance is despawned (unless there are also `AddressableSpawnRef`s referencing it). `AddressableUtil` can be used in conjunction with `AddressableSpawnRef`, and will share the same pools without issue.

### TimeToLiveAddressable

The `TimeToLiveAddressable` script is an extension of the Core script `TimeToLive`, which simply adds an `AddressableSpawnRef` to spawn on death, in addition to the regular `SpawnRef`.

Note that you can still use `TimeToLive` just fine with Addressable prefabs, but spawning a non-addressable prefab on death can cause the issue described in the next section.

## Addendum: Avoiding Spawned Prefab Unloading Issue

You should not have an Addressable prefab spawn another prefab via a direct reference (use Addressables instead).

Example: a bullet prefab is loaded from an Addressable asset, and spawns an impact VFX prefab when it hits (through direct reference, not addressable). The last remaining bullet object in the scene impacts, spawns its VFX, and is destroyed. Because it was the last intance, the Addressable asset for that bullet becomes unloaded. Unfortunately, this means that the texture and material used for the VFX is also unloaded, since it was only loaded due to being referenced by the bullet Addressable. This leads to the particles being replacd by PINK SQUARES OF DEATH.

Solution: the bullet must also spawn its impact object through an addressable reference.
