using RimWorld;
using Verse;

namespace SmartGear
{
    public enum GearContext
    {
        Normal,
        Combat,
        Work,
        Hunting,
        Cold,
        Hot
    }

    public static class ContextDetector
    {
        /// <summary>
        /// Determine the current gear context for a pawn.
        /// </summary>
        public static GearContext GetContext(Pawn pawn)
        {
            if (pawn == null) return GearContext.Normal;

            // Combat: drafted or fleeing
            if (pawn.Drafted)
                return GearContext.Combat;

            // Hunting job
            if (SGSettings.huntingWeapon && IsHunting(pawn))
                return GearContext.Hunting;

            // Temperature check
            if (SGSettings.temperatureAware && pawn.Map != null)
            {
                float ambientTemp = pawn.AmbientTemperature;
                FloatRange comfortRange = pawn.ComfortableTemperatureRange();

                if (ambientTemp < comfortRange.min - SGSettings.tempDangerMargin)
                    return GearContext.Cold;
                if (ambientTemp > comfortRange.max + SGSettings.tempDangerMargin)
                    return GearContext.Hot;
            }

            // Working
            if (pawn.CurJob != null && !pawn.CurJob.def.alwaysShowWeapon)
                return GearContext.Work;

            return GearContext.Normal;
        }

        public static bool IsHunting(Pawn pawn)
        {
            if (pawn?.CurJob == null) return false;
            return pawn.CurJob.def == JobDefOf.Hunt
                || pawn.CurJob.def == JobDefOf.PredatorHunt;
        }

        /// <summary>
        /// Check if a pawn is being attacked in melee range (for sidearm drawing).
        /// </summary>
        public static bool IsUnderMeleeAttack(Pawn pawn)
        {
            if (pawn?.Map == null) return false;

            foreach (var threat in pawn.Map.attackTargetsCache.GetPotentialTargetsFor(pawn))
            {
                Pawn attacker = threat.Thing as Pawn;
                if (attacker == null || attacker.Dead || attacker.Downed) continue;
                if (!attacker.HostileTo(pawn)) continue;

                // Check if attacker is in melee range (adjacent)
                if (attacker.Position.DistanceTo(pawn.Position) <= 1.5f)
                {
                    // Check if attacker is using melee
                    if (attacker.CurrentEffectiveVerb?.IsMeleeAttack == true)
                        return true;
                }
            }
            return false;
        }
    }
}
