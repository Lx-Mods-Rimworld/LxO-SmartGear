using System.Collections.Generic;
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
        // Track how long a pawn has been in extreme temperature (per pawn ID)
        private static readonly Dictionary<int, int> coldSinceTick = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> hotSinceTick = new Dictionary<int, int>();

        // Track last context per pawn for change-only logging
        private static readonly Dictionary<int, GearContext> lastLoggedContext = new Dictionary<int, GearContext>();

        // Require sustained exposure before triggering Cold/Hot context (2500 ticks = ~42 seconds)
        private const int TempSustainTicks = 2500;

        /// <summary>
        /// Determine the current gear context for a pawn.
        /// </summary>
        public static GearContext GetContext(Pawn pawn)
        {
            if (pawn == null) return GearContext.Normal;

            // Combat: drafted or fleeing
            if (pawn.Drafted)
                return LogContextIfChanged(pawn, GearContext.Combat, "drafted");

            // Hunting job
            if (SGSettings.huntingWeapon && IsHunting(pawn))
                return LogContextIfChanged(pawn, GearContext.Hunting, "hunting job active");

            // Temperature check -- only triggers after sustained exposure
            if (SGSettings.temperatureAware && pawn.Map != null)
            {
                float ambientTemp = pawn.AmbientTemperature;
                FloatRange comfortRange = pawn.ComfortableTemperatureRange();
                int tick = Find.TickManager.TicksGame;
                int pawnId = pawn.thingIDNumber;

                bool isCold = ambientTemp < comfortRange.min - SGSettings.tempDangerMargin;
                bool isHot = ambientTemp > comfortRange.max + SGSettings.tempDangerMargin;

                if (isCold)
                {
                    if (!coldSinceTick.ContainsKey(pawnId))
                        coldSinceTick[pawnId] = tick;
                    if (tick - coldSinceTick[pawnId] >= TempSustainTicks)
                        return LogContextIfChanged(pawn, GearContext.Cold,
                            $"ambient={ambientTemp:F1}C, comfort min={comfortRange.min:F1}C");
                }
                else
                {
                    coldSinceTick.Remove(pawnId);
                }

                if (isHot)
                {
                    if (!hotSinceTick.ContainsKey(pawnId))
                        hotSinceTick[pawnId] = tick;
                    if (tick - hotSinceTick[pawnId] >= TempSustainTicks)
                        return LogContextIfChanged(pawn, GearContext.Hot,
                            $"ambient={ambientTemp:F1}C, comfort max={comfortRange.max:F1}C");
                }
                else
                {
                    hotSinceTick.Remove(pawnId);
                }
            }

            // Working
            if (pawn.CurJob != null && !pawn.CurJob.def.alwaysShowWeapon)
                return LogContextIfChanged(pawn, GearContext.Work, $"job={pawn.CurJob.def.defName}");

            return LogContextIfChanged(pawn, GearContext.Normal, null);
        }

        private static GearContext LogContextIfChanged(Pawn pawn, GearContext newContext, string reason)
        {
            int pawnId = pawn.thingIDNumber;
            GearContext prev;
            if (lastLoggedContext.TryGetValue(pawnId, out prev))
            {
                if (prev != newContext)
                {
                    Log.Message($"[SmartGear] {pawn.LabelShort} context changed: {prev} -> {newContext}"
                        + (reason != null ? $" ({reason})" : ""));
                    lastLoggedContext[pawnId] = newContext;
                }
            }
            else
            {
                // First time seeing this pawn -- log the initial context
                Log.Message($"[SmartGear] {pawn.LabelShort} initial context: {newContext}"
                    + (reason != null ? $" ({reason})" : ""));
                lastLoggedContext[pawnId] = newContext;
            }
            return newContext;
        }

        public static bool IsHunting(Pawn pawn)
        {
            if (pawn?.CurJob == null) return false;
            return pawn.CurJob.def == JobDefOf.Hunt
                || pawn.CurJob.def == JobDefOf.PredatorHunt;
        }

        /// <summary>
        /// Check if a pawn is under melee threat (for sidearm drawing).
        /// Returns true if a hostile is adjacent and melee attacking, OR
        /// if a melee-only hostile is closing within 3 tiles.
        /// </summary>
        public static bool IsUnderMeleeAttack(Pawn pawn)
        {
            if (pawn?.Map == null) return false;

            foreach (var threat in pawn.Map.attackTargetsCache.GetPotentialTargetsFor(pawn))
            {
                Pawn attacker = threat.Thing as Pawn;
                if (attacker == null || attacker.Dead || attacker.Downed) continue;
                if (!attacker.HostileTo(pawn)) continue;

                float dist = attacker.Position.DistanceTo(pawn.Position);

                // Adjacent and melee attacking -- immediate threat
                if (dist <= 1.5f && attacker.CurrentEffectiveVerb?.IsMeleeAttack == true)
                    return true;

                // Closing within 3 tiles and has no ranged weapon -- about to melee
                if (dist <= 3f)
                {
                    bool attackerHasRanged = attacker.equipment?.Primary?.def.IsRangedWeapon == true;
                    if (!attackerHasRanged)
                        return true;
                }
            }
            return false;
        }
    }
}
