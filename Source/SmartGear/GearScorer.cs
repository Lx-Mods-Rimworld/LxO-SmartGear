using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SmartGear
{
    /// <summary>
    /// Scores weapons and apparel for a pawn based on their role, current context,
    /// preferences, ideology, and environmental conditions.
    /// </summary>
    public static class GearScorer
    {
        // ===================== WEAPON SCORING =====================

        /// <summary>
        /// Score a weapon for a pawn. Higher = better fit.
        /// </summary>
        public static float ScoreWeapon(Pawn pawn, Thing weapon, Role role, GearContext context)
        {
            if (weapon?.def == null) return -1000f;

            // Biocoded / persona / bladelink weapons: locked to a specific pawn
            // CompBladelinkWeapon inherits CompBiocodable, so one check covers both
            var biocomp = weapon.TryGetComp<CompBiocodable>();
            if (biocomp != null && biocomp.Biocoded)
            {
                if (biocomp.CodedPawn == pawn)
                    return 9000f; // This weapon belongs to this pawn -- always keep
                else
                    return -9000f; // Coded to someone else -- never equip
            }

            float score = 0f;
            bool wantsMelee = RoleDetector.PrefersMelee(role);
            bool isMelee = weapon.def.IsMeleeWeapon;
            bool isRanged = weapon.def.IsRangedWeapon;

            // Role fit: huge bonus for matching weapon type
            if (wantsMelee && isMelee) score += 100f;
            else if (!wantsMelee && isRanged) score += 100f;
            else if (wantsMelee && isRanged) score -= 50f;
            else if (!wantsMelee && isMelee) score -= 50f;

            // Context: hunting needs ranged
            if (context == GearContext.Hunting)
            {
                if (isRanged) score += 80f;
                else score -= 100f;
                // Prefer longer range for hunting
                if (isRanged)
                {
                    float range = weapon.def.Verbs?.FirstOrDefault()?.range ?? 0f;
                    score += range * 1.5f;
                }
            }

            // DPS / damage output
            if (isMelee)
            {
                float dps = weapon.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS);
                score += dps * 5f;
            }
            else if (isRanged)
            {
                float dmgMult = weapon.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier);
                score += dmgMult * 30f;
                float cooldown = weapon.GetStatValue(StatDefOf.RangedWeapon_Cooldown);
                if (cooldown > 0) score += (1f / cooldown) * 10f;
            }

            // HP/durability: penalize damaged weapons
            if (weapon.HitPoints < weapon.MaxHitPoints)
            {
                float hpPct = (float)weapon.HitPoints / weapon.MaxHitPoints;
                score *= hpPct;
            }

            // Quality bonus
            QualityCategory quality;
            if (weapon.TryGetQuality(out quality))
                score += ((int)quality - 2) * 10f; // Normal=0, Good=+10, Excellent=+20, etc.

            // Skill match: shooting skill boosts ranged score, melee skill boosts melee
            if (pawn.skills != null)
            {
                if (isRanged)
                    score += pawn.skills.GetSkill(SkillDefOf.Shooting).Level * 2f;
                if (isMelee)
                    score += pawn.skills.GetSkill(SkillDefOf.Melee).Level * 2f;
            }

            // Ideology: check if pawn's ideo has weapon preferences
            if (pawn.Ideo != null)
            {
                foreach (var precept in pawn.Ideo.PreceptsListForReading)
                {
                    if (precept.def.defName.Contains("Weapon") || precept.def.defName.Contains("Melee")
                        || precept.def.defName.Contains("Ranged"))
                    {
                        bool disapproved = precept.def.defName.Contains("Disapproved")
                            || precept.def.defName.Contains("Despised")
                            || precept.def.defName.Contains("Horrible");
                        float preceptScore = disapproved ? -30f : 30f;

                        if (isMelee && precept.def.defName.Contains("Melee"))
                            score += preceptScore;
                        if (isRanged && precept.def.defName.Contains("Ranged"))
                            score += preceptScore;
                    }
                }
            }

            // Trait preferences
            if (pawn.story?.traits != null)
            {
                // Trigger-happy: prefer fast weapons
                if (pawn.story.traits.HasTrait(TraitDef.Named("ShootingAccuracy"), -1) && isRanged)
                {
                    float cooldown = weapon.GetStatValue(StatDefOf.RangedWeapon_Cooldown);
                    if (cooldown < 1.5f) score += 20f;
                }
                // Careful shooter: prefer accurate weapons
                if (pawn.story.traits.HasTrait(TraitDef.Named("ShootingAccuracy"), 1) && isRanged)
                {
                    float range = weapon.def.Verbs?.FirstOrDefault()?.range ?? 0f;
                    if (range > 25f) score += 20f;
                }
            }

            // Log suspicious scores (0 or negative for a real weapon that isn't biocoded to someone else)
            if (score <= 0f && (isRanged || isMelee))
            {
                Log.Warning($"[SmartGear] ScoreWeapon: suspicious score {score:F1} for {pawn.LabelShort} + '{weapon.def.defName}' (role={role}, context={context}, isMelee={isMelee}, isRanged={isRanged}, wantsMelee={wantsMelee})");
            }

            return score;
        }

        // ===================== APPAREL SCORING =====================

        /// <summary>
        /// Score apparel for a pawn. Higher = better fit.
        /// </summary>
        public static float ScoreApparel(Pawn pawn, Apparel apparel, Role role, GearContext context)
        {
            if (apparel?.def == null) return -1000f;

            float score = 0f;

            // Base protection value -- use GetStatValueAbstract so worn and unworn
            // items produce the same score (GetStatValue changes based on equipped state)
            ThingDef stuff = apparel.Stuff;
            float armor = apparel.def.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp, stuff)
                + apparel.def.GetStatValueAbstract(StatDefOf.ArmorRating_Blunt, stuff) * 0.5f;
            float insulation = apparel.def.GetStatValueAbstract(StatDefOf.Insulation_Cold, stuff)
                + apparel.def.GetStatValueAbstract(StatDefOf.Insulation_Heat, stuff);

            // Context-based scoring
            switch (context)
            {
                case GearContext.Combat:
                    // Combat: armor is king
                    score += armor * 200f;
                    // Move speed offset matters in combat (apparel affects MoveSpeed via equippedStatOffsets)
                    float moveSpeedOffset = 0f;
                    if (apparel.def.equippedStatOffsets != null)
                    {
                        foreach (var mod in apparel.def.equippedStatOffsets)
                            if (mod.stat == StatDefOf.MoveSpeed) moveSpeedOffset = mod.value;
                    }
                    score += moveSpeedOffset * 20f;
                    break;

                case GearContext.Work:
                    // Work: prefer stat bonuses for current job
                    score += ScoreApparelForWork(pawn, apparel, role);
                    // Light armor still nice
                    score += armor * 30f;
                    // Move speed offset matters for workers
                    float workMoveOffset = 0f;
                    if (apparel.def.equippedStatOffsets != null)
                    {
                        foreach (var mod in apparel.def.equippedStatOffsets)
                            if (mod.stat == StatDefOf.MoveSpeed) workMoveOffset = mod.value;
                    }
                    score += workMoveOffset * 15f;
                    break;

                case GearContext.Cold:
                    // Cold: insulation is critical
                    score += apparel.def.GetStatValueAbstract(StatDefOf.Insulation_Cold, stuff) * 50f;
                    score += armor * 20f;
                    break;

                case GearContext.Hot:
                    // Hot: heat insulation + light clothing
                    score += apparel.def.GetStatValueAbstract(StatDefOf.Insulation_Heat, stuff) * 50f;
                    // Penalize heavy armor in heat
                    score -= armor * 30f;
                    break;

                default:
                    // Balanced: some of everything
                    score += armor * 50f;
                    score += insulation * 10f;
                    break;
            }

            // Quality bonus
            QualityCategory quality;
            if (apparel.TryGetQuality(out quality))
                score += ((int)quality - 2) * 15f;

            // Beauty (for pawns who care)
            if (pawn.story?.traits?.HasTrait(TraitDef.Named("Beauty"), 2) == true)
                score += apparel.GetStatValue(StatDefOf.Beauty) * 5f;

            // Ideology: nudity preferences, required apparel, modesty
            score += ScoreApparelForIdeology(pawn, apparel);

            // Royal title requirements
            score += ScoreApparelForRoyalty(pawn, apparel);

            // Already wearing it: small tiebreaker bonus.
            // Stats are now calculated consistently (GetStatValueAbstract), so this
            // only needs to prevent swapping between items with nearly identical scores.
            if (pawn.apparel?.WornApparel?.Contains(apparel) == true)
                score += 5f;

            // HP condition: penalize damaged gear
            if (apparel.HitPoints < apparel.MaxHitPoints)
            {
                float hpPct = (float)apparel.HitPoints / apparel.MaxHitPoints;
                score *= hpPct;
            }

            // Tainted: big penalty
            if (apparel.WornByCorpse)
                score -= 100f;

            // Log suspicious scores (negative for non-tainted apparel)
            if (score <= 0f && !apparel.WornByCorpse)
            {
                Log.Warning($"[SmartGear] ScoreApparel: suspicious score {score:F1} for {pawn.LabelShort} + '{apparel.def.defName}' (role={role}, context={context})");
            }

            return score;
        }

        private static float ScoreApparelForWork(Pawn pawn, Apparel apparel, Role role)
        {
            float score = 0f;

            switch (role)
            {
                case Role.Doctor:
                    score += apparel.GetStatValue(StatDefOf.MedicalSurgerySuccessChance, true, -1) * 100f;
                    score += apparel.GetStatValue(StatDefOf.MedicalTendQuality, true, -1) * 80f;
                    break;

                case Role.Worker:
                    // General work speed
                    score += apparel.GetStatValue(StatDefOf.WorkSpeedGlobal, true, -1) * 60f;
                    // Mining/construction speed from apparel is rare but valued
                    break;

                case Role.Shooter:
                case Role.Brawler:
                    // Combat roles still value armor even while working
                    score += apparel.GetStatValue(StatDefOf.ArmorRating_Sharp) * 80f;
                    break;
            }

            return score;
        }

        private static float ScoreApparelForIdeology(Pawn pawn, Apparel apparel)
        {
            float score = 0f;

            if (pawn.Ideo == null) return score;

            try
            {
                // Check ideo role required apparel
                var ideoRole = pawn.Ideo.GetRole(pawn);
                if (ideoRole != null)
                {
                    // If the pawn has an ideo role, apparel that matches role style gets a bonus
                    score += 20f; // General bonus for having role-appropriate gear
                }

                // Check if apparel has ideo style
                if (apparel.StyleDef != null && pawn.Ideo.style != null)
                {
                    score += 10f; // Bonus for ideo-styled apparel
                }
            }
            catch { }

            return score;
        }

        private static float ScoreApparelForRoyalty(Pawn pawn, Apparel apparel)
        {
            // Check if pawn has a royal title that requires certain apparel
            if (pawn.royalty == null) return 0f;

            float score = 0f;
            foreach (var title in pawn.royalty.AllTitlesForReading)
            {
                if (title.def.requiredApparel != null)
                {
                    foreach (var req in title.def.requiredApparel)
                    {
                        if (req.ApparelMeetsRequirement(apparel.def, false))
                            score += 40f;
                    }
                }
            }
            return score;
        }

        // ===================== SIDEARM SCORING =====================

        /// <summary>
        /// Score a weapon as a sidearm (secondary weapon). Melee sidearms for ranged pawns,
        /// ranged sidearms for melee pawns.
        /// </summary>
        public static float ScoreSidearm(Pawn pawn, Thing weapon, Role role)
        {
            if (weapon?.def == null) return -1000f;

            bool isMelee = weapon.def.IsMeleeWeapon;
            bool isRanged = weapon.def.IsRangedWeapon;
            bool wantsMelee = RoleDetector.PrefersMelee(role);

            float score = 0f;

            // Sidearm should be the OPPOSITE type of primary
            if (wantsMelee && isRanged)
                score += 50f; // Brawler wants a ranged backup
            else if (!wantsMelee && isMelee)
                score += 50f; // Shooter wants a melee backup
            else
                score -= 30f; // Same type as primary = less useful as sidearm

            // Melee sidearms: prefer fast, light weapons (knives, short swords)
            if (isMelee)
            {
                float dps = weapon.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS);
                score += dps * 3f;
                // Prefer lighter weapons as sidearms
                float mass = weapon.GetStatValue(StatDefOf.Mass);
                score -= mass * 5f;
            }

            // Ranged sidearms: prefer pistols/SMGs (short range, fast)
            if (isRanged)
            {
                float cooldown = weapon.GetStatValue(StatDefOf.RangedWeapon_Cooldown);
                if (cooldown < 1.5f) score += 20f; // Fast weapons
                float mass = weapon.GetStatValue(StatDefOf.Mass);
                score -= mass * 3f;
            }

            // Quality
            QualityCategory quality;
            if (weapon.TryGetQuality(out quality))
                score += ((int)quality - 2) * 5f;

            return score;
        }
    }
}
