using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SmartGear
{
    /// <summary>
    /// Automatically detected role for a pawn based on their skills and traits.
    /// Determines what kind of gear they should prefer.
    /// </summary>
    public enum Role
    {
        Default,
        Shooter,    // High shooting, prefers ranged
        Brawler,    // High melee or Brawler trait, prefers melee
        Doctor,     // High medicine, carries meds, wears medical gear
        Hunter,     // Assigned to hunting, needs hunting weapon
        Worker,     // General worker, prefers work-stat clothing
        Pacifist    // Incapable of violence
    }

    public static class RoleDetector
    {
        // Track last detected role per pawn for change-only logging
        private static readonly Dictionary<int, Role> lastLoggedRole = new Dictionary<int, Role>();

        /// <summary>
        /// Detect the best role for a pawn based on skills, traits, and work assignments.
        /// </summary>
        public static Role DetectRole(Pawn pawn)
        {
            if (pawn?.skills == null || pawn?.story == null) return Role.Default;
            if (!pawn.IsColonistPlayerControlled) return Role.Default;

            Role result;
            string reason;

            // Pacifist check
            if (pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                result = Role.Pacifist;
                reason = "incapable of violence";
            }
            // Brawler trait always = Brawler role
            else if (pawn.story.traits?.HasTrait(TraitDefOf.Brawler) == true)
            {
                result = Role.Brawler;
                reason = "Brawler trait";
            }
            // Hunter: assigned to hunting as priority 1
            else if (pawn.workSettings != null && pawn.workSettings.EverWork
                && pawn.workSettings.GetPriority(WorkTypeDefOf.Hunting) == 1)
            {
                result = Role.Hunter;
                reason = "hunting priority 1";
            }
            else
            {
                int shooting = pawn.skills.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
                int melee = pawn.skills.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                int medicine = pawn.skills.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;

                // Doctor: medicine is their best combat-relevant skill AND >= 8
                if (medicine >= 8 && medicine >= shooting && medicine >= melee)
                {
                    result = Role.Doctor;
                    reason = $"medicine={medicine} >= shooting={shooting}, melee={melee}";
                }
                // Shooter vs Brawler: who's better?
                else if (shooting >= 8 && shooting > melee)
                {
                    result = Role.Shooter;
                    reason = $"shooting={shooting} > melee={melee}";
                }
                else if (melee >= 8 && melee > shooting)
                {
                    result = Role.Brawler;
                    reason = $"melee={melee} > shooting={shooting}";
                }
                // If both combat skills are low, check if they're primarily a worker
                else if (shooting < 5 && melee < 5)
                {
                    result = Role.Worker;
                    reason = $"low combat skills (shooting={shooting}, melee={melee})";
                }
                // Moderate combat skills: default to shooting (ranged is generally safer)
                else if (shooting >= melee)
                {
                    result = Role.Shooter;
                    reason = $"shooting={shooting} >= melee={melee} (moderate)";
                }
                else
                {
                    result = Role.Brawler;
                    reason = $"melee={melee} > shooting={shooting} (moderate)";
                }
            }

            // Log only when role changes
            int pawnId = pawn.thingIDNumber;
            Role prev;
            if (lastLoggedRole.TryGetValue(pawnId, out prev))
            {
                if (prev != result)
                {
                    Log.Message($"[SmartGear] {pawn.LabelShort} role changed: {prev} -> {result} ({reason})");
                    lastLoggedRole[pawnId] = result;
                }
            }
            else
            {
                Log.Message($"[SmartGear] {pawn.LabelShort} initial role: {result} ({reason})");
                lastLoggedRole[pawnId] = result;
            }

            return result;
        }

        /// <summary>
        /// Get the primary combat stat priority for a role.
        /// </summary>
        public static StatDef GetPrimaryWeaponStat(Role role)
        {
            switch (role)
            {
                case Role.Shooter:
                case Role.Hunter:
                    return StatDefOf.RangedWeapon_DamageMultiplier;
                case Role.Brawler:
                    return StatDefOf.MeleeWeapon_AverageDPS;
                default:
                    return StatDefOf.RangedWeapon_DamageMultiplier;
            }
        }

        /// <summary>
        /// Should this role prefer melee weapons?
        /// </summary>
        public static bool PrefersMelee(Role role)
        {
            return role == Role.Brawler;
        }
    }
}
