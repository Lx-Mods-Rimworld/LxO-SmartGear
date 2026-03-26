using System;
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
        /// <summary>
        /// Detect the best role for a pawn based on skills, traits, and work assignments.
        /// </summary>
        public static Role DetectRole(Pawn pawn)
        {
            if (pawn?.skills == null || pawn?.story == null) return Role.Default;

            // Pacifist check
            if (pawn.WorkTagIsDisabled(WorkTags.Violent))
                return Role.Pacifist;

            // Brawler trait always = Brawler role
            if (pawn.story.traits?.HasTrait(TraitDefOf.Brawler) == true)
                return Role.Brawler;

            int shooting = pawn.skills.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
            int melee = pawn.skills.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
            int medicine = pawn.skills.GetSkill(SkillDefOf.Medicine)?.Level ?? 0;

            // Doctor: medicine is their best combat-relevant skill AND >= 8
            if (medicine >= 8 && medicine >= shooting && medicine >= melee)
                return Role.Doctor;

            // Shooter vs Brawler: who's better?
            if (shooting >= 8 && shooting > melee)
                return Role.Shooter;

            if (melee >= 8 && melee > shooting)
                return Role.Brawler;

            // If both combat skills are low, check if they're primarily a worker
            if (shooting < 5 && melee < 5)
                return Role.Worker;

            // Moderate combat skills: default to shooting (ranged is generally safer)
            if (shooting >= melee)
                return Role.Shooter;

            return Role.Brawler;
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
