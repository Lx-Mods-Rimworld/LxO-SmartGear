# LxO - Smart Gear

Intelligent automatic equipment management for RimWorld. Pawns equip the right weapon, wear the right clothes, and carry the right supplies -- based on their role, current job, weather, ideology, and combat status.

## How It Works

Smart Gear analyzes each pawn's skills, traits, and ideology to detect their **role** (Shooter, Brawler, Doctor, Worker, Pacifist). Based on role and current **context** (working, combat, hunting, cold, hot), it automatically selects the best available weapon, apparel, and inventory items.

**Zero configuration needed.** Works out of the box. Override per-pawn if you want manual control.

## Features

### Automatic Weapon Management
- Best weapon auto-selected based on role and combat skills
- Hunting jobs auto-equip best ranged weapon
- Brawler trait = melee preference, high shooting = ranged preference
- Ideology weapon precepts respected
- Careful Shooter prefers long-range, Trigger-Happy prefers fast weapons

### Sidearms
- Carry a secondary weapon (melee backup for shooters, ranged backup for brawlers)
- Auto-draw melee sidearm when attacked in melee range
- Auto-restore primary weapon when undrafted

### Context-Aware Apparel
- **Combat**: prioritizes armor, protection, mobility
- **Work**: prioritizes work-stat bonuses (medical gear for doctors, etc.)
- **Cold weather**: prioritizes cold insulation
- **Hot weather**: prioritizes heat insulation, penalizes heavy armor
- Nudist trait and ideology nudity preferences respected
- Ideology role-required apparel gets priority
- Royal title apparel requirements respected
- Tainted apparel penalized, quality rewarded

### Inventory Management
- Doctors and pawns with medicine skill auto-carry medicine
- Configurable medicine count (default: 3)

### Per-Pawn Control
- Auto-detected role shown in Smart Gear tab
- Lock toggle to disable auto-management for specific pawns
- Manual role override (force a pawn to be treated as Brawler, Doctor, etc.)
- Upgrade threshold prevents constant micro-swapping (default: 15% improvement required)

## Roles

| Role | Detection | Weapon Preference | Apparel Priority |
|------|-----------|------------------|-----------------|
| Shooter | Shooting >= 8, higher than melee | Ranged | Balanced |
| Brawler | Melee >= 8 or Brawler trait | Melee | Armor-heavy |
| Doctor | Medicine >= 8 and highest skill | Ranged (safe) | Medical stats |
| Worker | Low combat skills | Best available | Work stats |
| Hunter | On hunting job | Long-range ranged | Normal |
| Pacifist | Incapable of violence | None | Comfort |

## Settings

- Toggle each system independently (weapons, apparel, inventory, sidearms)
- Toggle each context (combat swap, hunting weapon, temperature, job apparel)
- Medicine count slider
- Upgrade threshold slider (how much better gear must be to trigger a swap)

## Compatibility

- Works alongside most mods
- Does NOT conflict with Simple Sidearms (disable our sidearm system if using SS)
- Does NOT conflict with vanilla outfit system (Smart Gear works on top of it)
- Safe to add or remove mid-save

## Requirements

- RimWorld 1.6+
- [Harmony](https://steamcommunity.com/workshop/filedetails/?id=2009463077)

## Languages

English, German, Chinese Simplified, Japanese, Korean, Russian, Spanish

## Credits

Developed by **Lexxers** ([Lx-Mods-Rimworld](https://github.com/Lx-Mods-Rimworld))

Free forever. Donations welcome: **[Ko-fi](https://ko-fi.com/lexxes)**

## License

- **Code:** MIT License
- **Content:** CC-BY-SA 4.0
