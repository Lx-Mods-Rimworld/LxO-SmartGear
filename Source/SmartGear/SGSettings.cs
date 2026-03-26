using UnityEngine;
using Verse;

namespace SmartGear
{
    public class SGSettings : ModSettings
    {
        // Master toggles
        public static bool enabled = true;
        public static bool autoWeapons = true;
        public static bool autoApparel = true;
        public static bool autoInventory = true;
        public static bool sidearms = true;

        // Context switching
        public static bool combatSwitch = true;      // Swap gear on draft/undraft
        public static bool huntingWeapon = true;      // Equip hunting weapon for hunting jobs
        public static bool temperatureAware = true;   // Seasonal clothing swap
        public static bool jobAwareApparel = true;    // Work-stat clothing per job

        // Sidearms
        public static bool autoMeleeSidearm = true;   // Auto-draw melee when attacked in melee
        public static bool carryMedicine = true;      // Doctors/fighters carry medicine
        public static int medicineCount = 3;          // How many to carry

        // Performance
        public static int evaluateInterval = 500;     // Ticks between gear evaluations

        // Thresholds
        public static float upgradeThreshold = 0.15f; // 15% better score to trigger swap
        public static float tempDangerMargin = 5f;    // Degrees C before comfort range triggers swap

        public override void ExposeData()
        {
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref autoWeapons, "autoWeapons", true);
            Scribe_Values.Look(ref autoApparel, "autoApparel", true);
            Scribe_Values.Look(ref autoInventory, "autoInventory", true);
            Scribe_Values.Look(ref sidearms, "sidearms", true);
            Scribe_Values.Look(ref combatSwitch, "combatSwitch", true);
            Scribe_Values.Look(ref huntingWeapon, "huntingWeapon", true);
            Scribe_Values.Look(ref temperatureAware, "temperatureAware", true);
            Scribe_Values.Look(ref jobAwareApparel, "jobAwareApparel", true);
            Scribe_Values.Look(ref autoMeleeSidearm, "autoMeleeSidearm", true);
            Scribe_Values.Look(ref carryMedicine, "carryMedicine", true);
            Scribe_Values.Look(ref medicineCount, "medicineCount", 3);
            Scribe_Values.Look(ref evaluateInterval, "evaluateInterval", 500);
            Scribe_Values.Look(ref upgradeThreshold, "upgradeThreshold", 0.15f);
            Scribe_Values.Look(ref tempDangerMargin, "tempDangerMargin", 5f);
            base.ExposeData();
        }

        public static void DrawSettings(Rect inRect)
        {
            Listing_Standard l = new Listing_Standard();
            l.Begin(inRect);

            l.CheckboxLabeled("SG_Enabled".Translate(), ref enabled);
            if (!enabled) { l.End(); return; }

            l.GapLine();
            l.Label("SG_AutoSystems".Translate());
            l.CheckboxLabeled("SG_AutoWeapons".Translate(), ref autoWeapons);
            l.CheckboxLabeled("SG_AutoApparel".Translate(), ref autoApparel);
            l.CheckboxLabeled("SG_AutoInventory".Translate(), ref autoInventory);
            l.CheckboxLabeled("SG_Sidearms".Translate(), ref sidearms);

            l.GapLine();
            l.Label("SG_Context".Translate());
            l.CheckboxLabeled("SG_CombatSwitch".Translate(), ref combatSwitch);
            l.CheckboxLabeled("SG_HuntingWeapon".Translate(), ref huntingWeapon);
            l.CheckboxLabeled("SG_TemperatureAware".Translate(), ref temperatureAware);
            l.CheckboxLabeled("SG_JobAwareApparel".Translate(), ref jobAwareApparel);

            l.GapLine();
            l.Label("SG_SidearmSettings".Translate());
            l.CheckboxLabeled("SG_AutoMelee".Translate(), ref autoMeleeSidearm);
            l.CheckboxLabeled("SG_CarryMedicine".Translate(), ref carryMedicine);
            if (carryMedicine)
            {
                l.Label("SG_MedicineCount".Translate() + ": " + medicineCount);
                medicineCount = (int)l.Slider(medicineCount, 1, 10);
            }

            l.GapLine();
            l.Label("SG_UpgradeThreshold".Translate() + ": " + (upgradeThreshold * 100f).ToString("F0") + "%");
            upgradeThreshold = l.Slider(upgradeThreshold, 0.05f, 0.50f);

            l.End();
        }
    }

    public class SmartGearMod : Mod
    {
        public static SGSettings settings;

        public SmartGearMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<SGSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            SGSettings.DrawSettings(inRect);
        }

        public override string SettingsCategory() => "LxO - Smart Gear";
    }
}
