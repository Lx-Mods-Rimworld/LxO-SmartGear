using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SmartGear
{
    public class CompProperties_GearManager : CompProperties
    {
        public CompProperties_GearManager() { compClass = typeof(CompGearManager); }
    }

    /// <summary>
    /// Per-pawn component that manages gear decisions.
    /// Evaluates role, context, and available gear periodically.
    /// </summary>
    public class CompGearManager : ThingComp
    {
        // Cached role (recalculated periodically)
        public Role cachedRole = Role.Default;
        private int roleCacheTick = -9999;
        private const int RoleCacheInterval = 2500;

        // Last context (for detecting context changes)
        private GearContext lastContext = GearContext.Normal;

        // Sidearm tracking
        public Thing sidearm;
        public Thing primaryWeapon;

        // Lock: player can lock a pawn to disable auto-gear
        public bool locked;

        // Per-pawn overrides
        public bool overrideRole;
        public Role manualRole = Role.Default;

        private int tickOffset = -1;

        public Pawn Pawn => (Pawn)parent;

        public Role CurrentRole
        {
            get
            {
                if (overrideRole) return manualRole;
                int tick = Find.TickManager.TicksGame;
                if (tick - roleCacheTick > RoleCacheInterval)
                {
                    cachedRole = RoleDetector.DetectRole(Pawn);
                    roleCacheTick = tick;
                }
                return cachedRole;
            }
        }

        public override void CompTick()
        {
            if (!SGSettings.enabled || locked) return;
            if (Pawn.Dead || Pawn.Downed || Pawn.Map == null) return;

            if (tickOffset < 0)
                tickOffset = parent.thingIDNumber % SGSettings.evaluateInterval;
            if ((Find.TickManager.TicksGame + tickOffset) % SGSettings.evaluateInterval != 0) return;

            try
            {
                // SAFETY CHECK: detect and fix non-weapon items in equipment slot
                FixBogusEquipment();

                GearContext context = ContextDetector.GetContext(Pawn);
                Role role = CurrentRole;

                // Context change triggers immediate gear evaluation
                bool contextChanged = context != lastContext;
                lastContext = context;

                // Drafted = player has manual control. Don't interfere with gear.
                // Only sidearm auto-draw in melee is allowed (survival reflex).
                if (Pawn.Drafted)
                {
                    if (SGSettings.sidearms && SGSettings.autoMeleeSidearm)
                        CheckMeleeSidearm(role);
                    return;
                }

                // Undrafted: auto-manage gear normally
                // Weapons handled colony-wide by MapComponent_WeaponAssigner
                // Per-pawn only on context change (hunting)
                if (SGSettings.autoWeapons && contextChanged)
                    EvaluateWeapon(role, context, contextChanged);

                if (SGSettings.autoApparel)
                    EvaluateApparel(role, context, contextChanged);

                if (SGSettings.autoInventory)
                    EvaluateInventory(role);

                // Pick up sidearm if we don't have one
                if (SGSettings.sidearms)
                    EvaluateSidearm(role);

                // (sidearm melee draw handled in drafted block above)
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("[SmartGear] Error evaluating gear for " + Pawn.LabelShort + ": " + ex.Message,
                    Pawn.thingIDNumber ^ 0x5347);
            }
        }

        // ===================== BOGUS EQUIPMENT FIX =====================

        /// <summary>
        /// Detect if the pawn has a non-weapon item (wood, steel, food, etc.) in their
        /// equipment slot and remove it. This can happen from hauling/inventory bugs.
        /// </summary>
        private void FixBogusEquipment()
        {
            ThingWithComps equipped = Pawn.equipment?.Primary;
            if (equipped == null) return;

            // Log EVERY equipment check so we can see what's happening
            bool isRanged = equipped.def.IsRangedWeapon;
            bool isMelee = equipped.def.IsMeleeWeapon;
            bool isWeapon = equipped.def.IsWeapon;

            if (!isRanged && !isMelee)
            {
                // This is NOT a real weapon -- remove it
                Log.Warning("[SmartGear] BOGUS EQUIP on " + Pawn.LabelShort
                    + ": '" + equipped.def.defName + "' (label=" + equipped.def.label
                    + " IsWeapon=" + isWeapon
                    + " IsRanged=" + isRanged
                    + " IsMelee=" + isMelee
                    + " category=" + equipped.def.category
                    + " thingClass=" + equipped.def.thingClass?.Name
                    + "). Dropping it now. CurJob=" + (Pawn.CurJob?.def?.defName ?? "none")
                    + " LastJob=" + (Pawn.jobs?.curDriver?.GetType()?.Name ?? "none"));

                ThingWithComps dropped;
                Pawn.equipment.TryDropEquipment(equipped, out dropped, Pawn.Position, false);
            }
        }

        // ===================== WEAPONS =====================

        private void EvaluateWeapon(Role role, GearContext context, bool contextChanged)
        {
            Thing currentWeapon = Pawn.equipment?.Primary;
            float currentScore = currentWeapon != null
                ? GearScorer.ScoreWeapon(Pawn, currentWeapon, role, context) : -500f;

            // Find best available weapon on map
            Thing bestWeapon = null;
            float bestScore = currentScore;
            float threshold = contextChanged ? 0f : SGSettings.upgradeThreshold;

            // Check map for weapons
            foreach (Thing thing in Pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon))
            {
                // Only consider actual weapons, not items that happen to be in the weapon group
                if (!thing.def.IsWeapon) continue;
                if (!thing.def.IsRangedWeapon && !thing.def.IsMeleeWeapon) continue;
                if (thing.IsForbidden(Pawn)) continue;
                if (!Pawn.CanReserve(thing) || !Pawn.CanReach(thing, PathEndMode.ClosestTouch, Danger.Some)) continue;
                if (thing.def.IsRangedWeapon && Pawn.WorkTagIsDisabled(WorkTags.Violent)) continue;
                if (thing.def.IsMeleeWeapon && Pawn.WorkTagIsDisabled(WorkTags.Violent)) continue;

                float score = GearScorer.ScoreWeapon(Pawn, thing, role, context);
                if (score > bestScore * (1f + threshold))
                {
                    bestScore = score;
                    bestWeapon = thing;
                }
            }

            // Also check items in nearby stockpiles the pawn already passed
            // (handled by the listerThings scan above)

            if (bestWeapon != null && bestWeapon != currentWeapon)
            {
                Log.Message("[SmartGear] " + Pawn.LabelShort + " EvaluateWeapon: equipping '"
                    + bestWeapon.def.defName + "' (IsRanged=" + bestWeapon.def.IsRangedWeapon
                    + " IsMelee=" + bestWeapon.def.IsMeleeWeapon
                    + " score=" + bestScore.ToString("F0") + ")");
                var job = JobMaker.MakeJob(JobDefOf.Equip, bestWeapon);
                Pawn.jobs.TryTakeOrderedJob(job, Verse.AI.JobTag.Misc);
            }
        }

        // ===================== APPAREL =====================

        private void EvaluateApparel(Role role, GearContext context, bool contextChanged)
        {
            if (Pawn.apparel == null) return;

            // Don't evaluate every tick even on context change -- apparel is slower to swap
            if (!contextChanged && Find.TickManager.TicksGame % (SGSettings.evaluateInterval * 3) != 0)
                return;

            // Check ideology nudity preference
            bool prefersNudity = false;
            if (Pawn.Ideo != null)
            {
                foreach (var precept in Pawn.Ideo.PreceptsListForReading)
                {
                    if (precept.def.defName.Contains("Nudity") && precept.def.defName.Contains("Approved"))
                        prefersNudity = true;
                }
            }
            if (Pawn.story?.traits?.HasTrait(TraitDef.Named("Nudist")) == true)
                prefersNudity = true;

            if (prefersNudity) return; // Don't force clothes on nudists

            // For each body slot, find best available apparel
            foreach (Thing thing in Pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel))
            {
                Apparel apparel = thing as Apparel;
                if (apparel == null) continue;
                if (apparel.IsForbidden(Pawn)) continue;
                if (!Pawn.CanReserve(apparel) && Pawn.CanReach(apparel, PathEndMode.ClosestTouch, Danger.Some)) continue;

                // Check if pawn can wear it
                if (!ApparelUtility.HasPartsToWear(Pawn, apparel.def)) continue;

                float newScore = GearScorer.ScoreApparel(Pawn, apparel, role, context);

                // Compare to currently worn apparel in same slot
                bool betterThanCurrent = true;
                foreach (Apparel worn in Pawn.apparel.WornApparel)
                {
                    if (!ApparelUtility.CanWearTogether(worn.def, apparel.def, Pawn.RaceProps.body))
                    {
                        float wornScore = GearScorer.ScoreApparel(Pawn, worn, role, context);
                        if (newScore <= wornScore * (1f + SGSettings.upgradeThreshold))
                        {
                            betterThanCurrent = false;
                            break;
                        }
                    }
                }

                if (betterThanCurrent && newScore > 0f)
                {
                    var job = JobMaker.MakeJob(JobDefOf.Wear, apparel);
                    Pawn.jobs.TryTakeOrderedJob(job, Verse.AI.JobTag.Misc);
                    return; // One apparel change per evaluation to avoid spam
                }
            }
        }

        // ===================== INVENTORY =====================

        private void EvaluateInventory(Role role)
        {
            if (!SGSettings.carryMedicine) return;

            // Don't interrupt current job to grab medicine
            if (Pawn.CurJob != null && Pawn.CurJob.def == JobDefOf.TakeCountToInventory)
                return;

            // Doctors and fighters with medicine skill should carry medicine
            bool shouldCarryMeds = role == Role.Doctor
                || (Pawn.skills?.GetSkill(SkillDefOf.Medicine)?.Level >= 4
                    && !Pawn.WorkTagIsDisabled(WorkTags.Caring));

            if (!shouldCarryMeds) return;

            // Count medicine in inventory
            int medsInInventory = 0;
            foreach (Thing item in Pawn.inventory.innerContainer)
            {
                if (item.def.IsMedicine)
                    medsInInventory += item.stackCount;
            }

            // Also count medicine being carried in hands
            if (Pawn.carryTracker?.CarriedThing?.def?.IsMedicine == true)
                medsInInventory += Pawn.carryTracker.CarriedThing.stackCount;

            if (medsInInventory >= SGSettings.medicineCount) return;

            // Don't pick up medicine we already have enough of
            int needed = SGSettings.medicineCount - medsInInventory;
            if (needed <= 0) return;

            // Find medicine to pick up (not from our own inventory)
            Thing bestMed = GenClosest.ClosestThingReachable(
                Pawn.Position, Pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Medicine),
                PathEndMode.ClosestTouch,
                TraverseParms.For(Pawn),
                30f,
                t => !t.IsForbidden(Pawn) && Pawn.CanReserve(t) && t.stackCount > 0
                    && !Pawn.inventory.innerContainer.Contains(t));

            if (bestMed != null)
            {
                Log.Message("[SmartGear] " + Pawn.LabelShort + " picking up " + needed
                    + "x " + bestMed.def.label + " (has " + medsInInventory + "/" + SGSettings.medicineCount + ")");
                var job = JobMaker.MakeJob(JobDefOf.TakeCountToInventory, bestMed);
                job.count = Math.Min(needed, bestMed.stackCount);
                Pawn.jobs.TryTakeOrderedJob(job, Verse.AI.JobTag.Misc);
            }
        }

        // ===================== SIDEARMS =====================

        /// <summary>
        /// Ensure pawn has a sidearm in inventory (opposite type of primary).
        /// Called during regular gear evaluation.
        /// </summary>
        private void EvaluateSidearm(Role role)
        {
            if (!SGSettings.sidearms) return;
            if (Pawn.WorkTagIsDisabled(WorkTags.Violent)) return;

            Thing primary = Pawn.equipment?.Primary;
            if (primary == null) return;

            // Check if already carrying a sidearm in inventory
            bool hasMeleeSidearm = false;
            bool hasRangedSidearm = false;
            foreach (Thing item in Pawn.inventory.innerContainer)
            {
                if (item.def.IsMeleeWeapon) hasMeleeSidearm = true;
                if (item.def.IsRangedWeapon) hasRangedSidearm = true;
            }

            // Determine what sidearm we need
            bool needMelee = primary.def.IsRangedWeapon && !hasMeleeSidearm;
            bool needRanged = primary.def.IsMeleeWeapon && !hasRangedSidearm;

            if (!needMelee && !needRanged) return;

            // Find best sidearm on map
            Thing bestSidearm = null;
            float bestScore = 0f;

            foreach (Thing weapon in Pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon))
            {
                if (weapon.IsForbidden(Pawn)) continue;
                if (!Pawn.CanReserve(weapon)) continue;
                if (weapon.Position.DistanceTo(Pawn.Position) > 30f) continue;

                if (needMelee && !weapon.def.IsMeleeWeapon) continue;
                if (needRanged && !weapon.def.IsRangedWeapon) continue;

                float score = GearScorer.ScoreSidearm(Pawn, weapon, role);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSidearm = weapon;
                }
            }

            if (bestSidearm != null)
            {
                // Pick up sidearm to inventory
                var job = JobMaker.MakeJob(JobDefOf.TakeCountToInventory, bestSidearm);
                job.count = 1;
                Pawn.jobs.TryTakeOrderedJob(job, Verse.AI.JobTag.Misc);
            }
        }

        private void CheckMeleeSidearm(Role role)
        {
            if (!ContextDetector.IsUnderMeleeAttack(Pawn)) return;

            Thing currentWeapon = Pawn.equipment?.Primary;
            if (currentWeapon == null) return;

            // If already using melee, no need to switch
            if (currentWeapon.def.IsMeleeWeapon) return;

            // Find best melee weapon in inventory
            Thing bestMelee = null;
            float bestScore = 0f;

            foreach (Thing item in Pawn.inventory.innerContainer)
            {
                if (!item.def.IsMeleeWeapon) continue;
                float score = GearScorer.ScoreSidearm(Pawn, item, role);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMelee = item;
                }
            }

            if (bestMelee != null)
            {
                // Store current weapon as primary (to re-equip later)
                primaryWeapon = currentWeapon;

                // Swap: unequip ranged, equip melee from inventory
                ThingWithComps droppedWep;
                Pawn.equipment.TryDropEquipment(currentWeapon as ThingWithComps, out droppedWep, Pawn.Position);
                if (droppedWep != null)
                    Pawn.inventory.innerContainer.TryAdd(droppedWep);

                Pawn.inventory.innerContainer.Remove(bestMelee);
                Pawn.equipment.AddEquipment(bestMelee as ThingWithComps);

                sidearm = bestMelee;
            }
        }

        /// <summary>
        /// Called when pawn is undrafted. Restore primary weapon if sidearm was drawn.
        /// </summary>
        public void OnUndraft()
        {
            if (sidearm == null || primaryWeapon == null) return;

            Thing currentWeapon = Pawn.equipment?.Primary;
            if (currentWeapon == sidearm)
            {
                // Swap back: unequip sidearm, re-equip primary
                ThingWithComps droppedSidearm;
                Pawn.equipment.TryDropEquipment(currentWeapon as ThingWithComps, out droppedSidearm, Pawn.Position);
                if (droppedSidearm != null)
                    Pawn.inventory.innerContainer.TryAdd(droppedSidearm);

                if (Pawn.inventory.innerContainer.Contains(primaryWeapon))
                {
                    Pawn.inventory.innerContainer.Remove(primaryWeapon);
                    Pawn.equipment.AddEquipment(primaryWeapon as ThingWithComps);
                }
            }

            sidearm = null;
            primaryWeapon = null;
        }

        // ===================== SAVE/LOAD =====================

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref locked, "sg_locked", false);
            Scribe_Values.Look(ref overrideRole, "sg_overrideRole", false);
            Scribe_Values.Look(ref manualRole, "sg_manualRole", Role.Default);
            Scribe_References.Look(ref sidearm, "sg_sidearm");
            Scribe_References.Look(ref primaryWeapon, "sg_primaryWeapon");
        }

        public override string CompInspectStringExtra()
        {
            if (!(parent is Pawn)) return null;
            if (!SGSettings.enabled || Pawn.Dead) return null;
            return "SG_Role".Translate(CurrentRole.ToString());
        }
    }
}
