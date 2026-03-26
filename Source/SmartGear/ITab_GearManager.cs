using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace SmartGear
{
    [StaticConstructorOnStartup]
    public class ITab_GearManager : ITab
    {
        private const float TabWidth = 350f;
        private const float TabHeight = 300f;

        public ITab_GearManager()
        {
            size = new Vector2(TabWidth, TabHeight);
            labelKey = "SG_Tab";
        }

        public override bool IsVisible
        {
            get
            {
                if (SelThing is Pawn p && !p.Dead)
                    return p.GetComp<CompGearManager>() != null;
                return false;
            }
        }

        protected override void FillTab()
        {
            Pawn pawn = SelThing as Pawn;
            if (pawn == null) return;

            var comp = pawn.GetComp<CompGearManager>();
            if (comp == null) return;

            Rect rect = new Rect(0f, 0f, TabWidth, TabHeight).ContractedBy(10f);
            Listing_Standard l = new Listing_Standard();
            l.Begin(rect);

            // Title
            Text.Font = GameFont.Medium;
            l.Label("SG_TabTitle".Translate());
            Text.Font = GameFont.Small;
            l.Gap(4f);

            // Role
            Role role = comp.CurrentRole;
            GUI.color = GetRoleColor(role);
            l.Label("SG_CurrentRole".Translate() + ": " + ("SG_Role_" + role).Translate());
            GUI.color = Color.white;

            // Context
            GearContext ctx = ContextDetector.GetContext(pawn);
            l.Label("SG_CurrentContext".Translate() + ": " + ("SG_Context_" + ctx).Translate());

            l.GapLine();

            // Lock toggle
            l.CheckboxLabeled("SG_LockGear".Translate(), ref comp.locked, "SG_LockGear_Desc".Translate());

            // Role override
            l.CheckboxLabeled("SG_OverrideRole".Translate(), ref comp.overrideRole);
            if (comp.overrideRole)
            {
                foreach (Role r in Enum.GetValues(typeof(Role)))
                {
                    if (l.RadioButton(("SG_Role_" + r).Translate(), comp.manualRole == r))
                        comp.manualRole = r;
                }
            }

            l.GapLine();

            // Current gear summary
            Thing weapon = pawn.equipment?.Primary;
            l.Label("SG_PrimaryWeapon".Translate() + ": " +
                (weapon?.LabelCap ?? "SG_None".Translate()));

            if (comp.sidearm != null)
                l.Label("SG_Sidearm".Translate() + ": " + comp.sidearm.LabelCap);

            // Medicine count
            int meds = 0;
            foreach (Thing item in pawn.inventory.innerContainer)
            {
                if (item.def.IsMedicine) meds += item.stackCount;
            }
            if (meds > 0)
                l.Label("SG_Medicine".Translate() + ": " + meds);

            l.End();
        }

        private Color GetRoleColor(Role role)
        {
            switch (role)
            {
                case Role.Shooter: return new Color(0.9f, 0.5f, 0.3f);
                case Role.Brawler: return new Color(0.9f, 0.3f, 0.3f);
                case Role.Doctor: return new Color(0.3f, 0.8f, 0.3f);
                case Role.Hunter: return new Color(0.7f, 0.6f, 0.3f);
                case Role.Worker: return new Color(0.4f, 0.6f, 0.9f);
                case Role.Pacifist: return new Color(0.7f, 0.7f, 0.9f);
                default: return Color.white;
            }
        }
    }
}
