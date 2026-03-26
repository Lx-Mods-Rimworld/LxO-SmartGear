using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SmartGear
{
    /// <summary>
    /// Colony-wide weapon assignment. Best skilled pawns get first pick of the best weapons.
    /// Runs periodically and reassigns weapons optimally across all colonists.
    /// Pawns will swap weapons with each other if it improves colony combat power.
    /// </summary>
    public class MapComponent_WeaponAssigner : MapComponent
    {
        private const int AssignInterval = 2500; // ~42 seconds game time

        public MapComponent_WeaponAssigner(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (!SGSettings.enabled || !SGSettings.autoWeapons) return;
            if (Find.TickManager.TicksGame % AssignInterval != 0) return;

            AssignWeaponsOptimally();
        }

        private void AssignWeaponsOptimally()
        {
            // Collect all colonists with gear management
            var pawns = new List<PawnWeaponEntry>();
            foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn.Dead || pawn.Downed) continue;
                if (pawn.Faction != Faction.OfPlayer) continue;
                if (pawn.IsPrisoner || QuestUtility.IsQuestLodger(pawn)) continue;
                // Skip children (Biotech)
                if (ModsConfig.BiotechActive && !pawn.DevelopmentalStage.Adult()) continue;
                var comp = pawn.GetComp<CompGearManager>();
                if (comp == null || comp.locked) continue;
                if (pawn.WorkTagIsDisabled(WorkTags.Violent)) continue;

                Role role = comp.CurrentRole;
                GearContext context = ContextDetector.GetContext(pawn);

                pawns.Add(new PawnWeaponEntry
                {
                    pawn = pawn,
                    comp = comp,
                    role = role,
                    context = context,
                    combatSkill = GetCombatSkill(pawn, role),
                    isSlave = pawn.IsSlave
                });
            }

            if (pawns.Count == 0) return;

            // Sort: colonists first (by combat skill), then slaves (by combat skill)
            // Colonists get best weapons, slaves get what's left
            pawns.Sort((a, b) =>
            {
                if (a.isSlave != b.isSlave)
                    return a.isSlave ? 1 : -1; // Slaves sort after colonists
                return b.combatSkill.CompareTo(a.combatSkill);
            });

            // Collect all available weapons: on map + currently equipped
            var allWeapons = new List<Thing>();
            var weaponOwner = new Dictionary<int, Pawn>(); // weaponThingID -> current owner

            // Add weapons on map (unequipped)
            foreach (Thing weapon in map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon))
            {
                if (weapon.IsForbidden(Faction.OfPlayer)) continue;
                if (!weapon.def.IsWeapon) continue;
                if (!weapon.def.IsRangedWeapon && !weapon.def.IsMeleeWeapon) continue;
                if (weapon.def.IsStuff) continue; // Materials are not weapons
                allWeapons.Add(weapon);
            }

            // Add currently equipped weapons (available for reassignment)
            foreach (var entry in pawns)
            {
                Thing weapon = entry.pawn.equipment?.Primary;
                if (weapon != null)
                {
                    allWeapons.Add(weapon);
                    weaponOwner[weapon.thingIDNumber] = entry.pawn;
                }
            }

            if (allWeapons.Count == 0) return;

            // Greedy assignment: best pawn picks best weapon first
            var assignedWeapons = new HashSet<int>();
            var assignments = new Dictionary<Pawn, Thing>(); // pawn -> weapon they should have

            foreach (var entry in pawns)
            {
                Thing bestWeapon = null;
                float bestScore = -999f;

                foreach (Thing weapon in allWeapons)
                {
                    if (assignedWeapons.Contains(weapon.thingIDNumber)) continue;

                    // Skip weapons the pawn can't reach (unless they already have it)
                    bool alreadyHas = (entry.pawn.equipment?.Primary == weapon);
                    if (!alreadyHas && weapon.Spawned)
                    {
                        if (!entry.pawn.CanReach(weapon, PathEndMode.ClosestTouch, Danger.Some))
                            continue;
                    }

                    float score = GearScorer.ScoreWeapon(entry.pawn, weapon, entry.role, entry.context);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestWeapon = weapon;
                    }
                }

                if (bestWeapon != null)
                {
                    assignments[entry.pawn] = bestWeapon;
                    assignedWeapons.Add(bestWeapon.thingIDNumber);
                }
            }

            // Execute swaps: only swap if the new weapon is significantly better
            foreach (var kvp in assignments)
            {
                Pawn pawn = kvp.Key;
                Thing targetWeapon = kvp.Value;
                Thing currentWeapon = pawn.equipment?.Primary;

                // Already have the right weapon
                if (currentWeapon == targetWeapon) continue;

                // Never swap away biocoded or persona weapons
                if (currentWeapon != null)
                {
                    var bio = currentWeapon.TryGetComp<CompBiocodable>();
                    if (bio != null && bio.Biocoded && bio.CodedPawn == pawn) continue;
                }

                var comp = pawn.GetComp<CompGearManager>();
                Role role = comp?.CurrentRole ?? Role.Default;
                GearContext context = ContextDetector.GetContext(pawn);

                float currentScore = currentWeapon != null
                    ? GearScorer.ScoreWeapon(pawn, currentWeapon, role, context) : -500f;
                float newScore = GearScorer.ScoreWeapon(pawn, targetWeapon, role, context);

                // Only swap if significantly better (prevents constant swapping)
                if (newScore <= currentScore * (1f + SGSettings.upgradeThreshold)) continue;

                // If the target weapon is on the ground, go pick it up
                if (targetWeapon.Spawned && pawn.CanReserve(targetWeapon))
                {
                    // Drop current weapon first if we have one
                    if (currentWeapon != null)
                    {
                        ThingWithComps dropped;
                        pawn.equipment.TryDropEquipment(currentWeapon as ThingWithComps,
                            out dropped, pawn.Position, false);
                    }

                    var job = JobMaker.MakeJob(JobDefOf.Equip, targetWeapon);
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
                // If the target weapon is equipped by another pawn who got assigned something else,
                // the other pawn will drop it on their next evaluation cycle
            }
        }

        private float GetCombatSkill(Pawn pawn, Role role)
        {
            if (pawn.skills == null) return 0f;

            float shooting = pawn.skills.GetSkill(SkillDefOf.Shooting)?.Level ?? 0f;
            float melee = pawn.skills.GetSkill(SkillDefOf.Melee)?.Level ?? 0f;

            // Weight by role
            switch (role)
            {
                case Role.Shooter:
                case Role.Hunter:
                    return shooting * 2f + melee * 0.5f;
                case Role.Brawler:
                    return melee * 2f + shooting * 0.5f;
                case Role.Doctor:
                    return Math.Max(shooting, melee);
                default:
                    return shooting + melee;
            }
        }

        private struct PawnWeaponEntry
        {
            public Pawn pawn;
            public CompGearManager comp;
            public Role role;
            public GearContext context;
            public float combatSkill;
            public bool isSlave;
        }
    }
}
