using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace SmartGear
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        static HarmonyInit()
        {
            var harmony = new Harmony("Lexxers.SmartGear");

            try
            {
                harmony.PatchAll(Assembly.GetExecutingAssembly());

                // Add comp to all humanlike pawns
                foreach (var def in DefDatabase<ThingDef>.AllDefs)
                {
                    if (def.race == null || def.race.intelligence != Intelligence.Humanlike) continue;
                    if (def.comps == null) continue;
                    if (def.comps.Any(c => c is CompProperties_GearManager)) continue;
                    def.comps.Add(new CompProperties_GearManager());
                }

                Log.Message("[SmartGear] Initialized. Patches applied, comp added to humanlike pawns.");
            }
            catch (Exception ex)
            {
                Log.Error("[SmartGear] Init failed: " + ex);
            }
        }
    }

    /// <summary>
    /// Detect when a pawn is undrafted to restore primary weapon from sidearm.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.Drafted), MethodType.Setter)]
    public static class Patch_DraftedSet
    {
        public static void Postfix(Pawn_DraftController __instance, bool value)
        {
            try
            {
                if (value) return; // Only care about undrafting
                if (!SGSettings.sidearms) return;

                Pawn pawn = __instance.pawn;
                if (pawn == null) return;

                var comp = pawn.GetComp<CompGearManager>();
                comp?.OnUndraft();
            }
            catch (Exception) { }
        }
    }

    /// <summary>
    /// When a pawn starts a hunting job, trigger weapon evaluation for hunting context.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    public static class Patch_StartJob_Hunting
    {
        public static void Postfix(Pawn_JobTracker __instance, Verse.AI.Job newJob)
        {
            try
            {
                if (!SGSettings.huntingWeapon || !SGSettings.autoWeapons) return;
                if (newJob?.def != JobDefOf.Hunt) return;

                Pawn pawn = HarmonyLib.Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
                if (pawn == null) return;

                var comp = pawn.GetComp<CompGearManager>();
                if (comp == null || comp.locked) return;

                // Find best hunting weapon and equip immediately
                Thing bestHunting = null;
                float bestScore = -999f;
                Role role = comp.CurrentRole;

                // Check current weapon
                Thing current = pawn.equipment?.Primary;
                if (current != null)
                    bestScore = GearScorer.ScoreWeapon(pawn, current, role, GearContext.Hunting);

                // Check inventory for better hunting weapon
                foreach (Thing item in pawn.inventory.innerContainer)
                {
                    if (!item.def.IsRangedWeapon) continue;
                    float score = GearScorer.ScoreWeapon(pawn, item, role, GearContext.Hunting);
                    if (score > bestScore * 1.1f)
                    {
                        bestScore = score;
                        bestHunting = item;
                    }
                }

                // Check map for nearby hunting weapons
                if (bestHunting == null && pawn.Map != null)
                {
                    foreach (Thing thing in pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon))
                    {
                        if (!thing.def.IsRangedWeapon) continue;
                        if (thing.IsForbidden(pawn)) continue;
                        if (thing.Position.DistanceTo(pawn.Position) > 20f) continue;
                        if (!pawn.CanReserve(thing)) continue;

                        float score = GearScorer.ScoreWeapon(pawn, thing, role, GearContext.Hunting);
                        if (score > bestScore * 1.1f)
                        {
                            bestScore = score;
                            bestHunting = thing;
                        }
                    }
                }

                if (bestHunting != null)
                {
                    // Swap to hunting weapon
                    if (pawn.inventory.innerContainer.Contains(bestHunting))
                    {
                        ThingWithComps currentWep = pawn.equipment?.Primary;
                        if (currentWep != null)
                        {
                            ThingWithComps droppedWep;
                            pawn.equipment.TryDropEquipment(currentWep, out droppedWep, pawn.Position);
                            if (droppedWep != null)
                                pawn.inventory.innerContainer.TryAdd(droppedWep);
                        }
                        pawn.inventory.innerContainer.Remove(bestHunting);
                        pawn.equipment.AddEquipment(bestHunting as ThingWithComps);
                    }
                }
            }
            catch (Exception) { }
        }
    }
}
