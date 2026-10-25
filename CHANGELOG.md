# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2026-10-24

### Changed
* Changed the minimum Unity version to 6000.3. The package may continue to work on older versions but they won't be supported.

### Fixed

- Fixed a bug with a `SpawnRef` handler not getting reset in a `ScriptableObject`.
- Restructured the addressable loading async code to better handle failure and cancellation.

## [1.1.0] - 2025-2-11

### Added
- Name comparison method for checking equality of addressable assets.

### Changed
- Minimum Unity version changed to 2022.3.

## [1.0.0] - 2023-6-21

### Added
- Initial release, all files and documentation added.
