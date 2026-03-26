# Changelog

All notable changes to this mod will be documented in this file.

## [1.0.1] - 2026-03-26

### Fixes
- Fixed an error when pawns die: gear manager no longer runs on corpses.

### Improvements
- All debug logging is now conditional on LxDebug mod being active. Zero performance overhead for normal users.

## [1.0.0] - 2026-03-26

### Features
- Automatic role detection from skills and traits (Shooter, Brawler, Doctor, Worker, Hunter, Pacifist)
- Context-aware weapon selection: best weapon auto-assigned based on role
- Colony-wide weapon optimization: best fighters get the best weapons first
- Sidearm support: pawns pick up a secondary weapon (melee backup for shooters, ranged for brawlers)
- Auto-draw melee sidearm when attacked in close combat, restores primary on undraft
- Context-aware apparel: combat armor on threats, work-stat gear while working, insulation for weather
- Ideology and royal title apparel requirements respected
- Nudist and trait preferences respected
- Doctors and medics auto-carry medicine (configurable count)
- Hunting jobs auto-equip best ranged weapon
- Per-pawn lock toggle and manual role override in Smart Gear tab
- Upgrade threshold prevents constant gear swapping
- 7 languages: English, German, Chinese Simplified, Japanese, Korean, Russian, Spanish

### Fixes
- Pawns no longer auto-equip gear while drafted (manual control mode respected)
- Fixed medicine pickup loop where pawns kept swapping the same medicine
- Fixed pawns equipping non-weapon items like wood or steel
- Non-weapon items are now blocked from the equipment slot entirely
- Stricter weapon filtering in all weapon scans
