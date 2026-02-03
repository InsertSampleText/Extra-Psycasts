using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Noise;
using Verse.Sound;

namespace EvenMorePsycasts
{

    public class EMP_CompAbilityEffect_WaterSpew : CompAbilityEffect
    {
        private readonly List<IntVec3> tmpCells = new List<IntVec3>();

        private new EMP_CompProperties_AbilityWaterSpew Props => (EMP_CompProperties_AbilityWaterSpew)props;

        private Pawn Pawn => parent.pawn;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            IntVec3 cell = target.Cell;
            Map mapHeld = parent.pawn.MapHeld;
            DamageDef damageDef = Props.damageDef;
            Pawn pawn = Pawn;
            ThingDef filthDef = Props.filthDef;
            int damAmount = Props.damAmount;
            bool setdamageFalloff = Props.damageFalloff;


            SimpleCurve flammabilityAttachFireChanceCurve = parent.verb.verbProps.flammabilityAttachFireChanceCurve;
            List<IntVec3> overrideCells = AffectedCells(target);
            GenExplosion.DoExplosion(cell, mapHeld, 0f, damageDef, pawn, damAmount, -1f, null, null, null, null, filthDef, 1f, 1, null, null, 255, applyDamageToExplosionCellsNeighbors: false, null, 0f, 1, 1f, damageFalloff: setdamageFalloff, null, null, null, doVisualEffects: false, 0.6f, 0f, doSoundEffects: false, null, 1f, flammabilityAttachFireChanceCurve, overrideCells);
            base.Apply(target, dest);

            //extinguish and reveal invis
            Map map = parent.pawn.Map;
            foreach (IntVec3 item in AffectedCells(target))
            {
                if (!item.InBounds(map))
                {
                    continue;
                }
                List<Thing> thingList = item.GetThingList(map);
                for (int num = thingList.Count - 1; num >= 0; num--)
                {
                    if (thingList[num] is Fire)
                    {
                        thingList[num].Destroy();
                    }
                    else if (thingList[num] is Pawn otherpawn)
                    {
                        otherpawn.GetInvisibilityComp()?.DisruptInvisibility();
                    }
                }
                if (!item.Filled(map))
                {
                    FilthMaker.TryMakeFilth(item, map, filthDef);
                }
                FleckCreationData dataStatic = FleckMaker.GetDataStatic(item.ToVector3Shifted(), map, FleckDefOf.WaterskipSplashParticles);
                dataStatic.rotationRate = Rand.Range(-30, 30);
                dataStatic.rotation = 90 * Rand.RangeInclusive(0, 3);
                map.flecks.CreateFleck(dataStatic);
                CompAbilityEffect_Teleport.SendSkipUsedSignal(item, parent.pawn);
            }
        }

        public override IEnumerable<PreCastAction> GetPreCastActions()
        {
            if (Props.effecterDef != null)
            {
                yield return new PreCastAction
                {
                    action = delegate (LocalTargetInfo a, LocalTargetInfo b)
                    {
                        parent.AddEffecterToMaintain(Props.effecterDef.Spawn(parent.pawn.Position, a.Cell, parent.pawn.Map), Pawn.Position, a.Cell, 17, Pawn.MapHeld);
                    },
                    ticksAwayFromCast = 17
                };
            }
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            GenDraw.DrawFieldEdges(AffectedCells(target));
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            if (Pawn.Faction != null)
            {
                foreach (IntVec3 item in AffectedCells(target))
                {
                    List<Thing> thingList = item.GetThingList(Pawn.Map);
                    for (int i = 0; i < thingList.Count; i++)
                    {
                        if (thingList[i].Faction == Pawn.Faction)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private List<IntVec3> AffectedCells(LocalTargetInfo target)
        {
            tmpCells.Clear();
            Vector3 vector = Pawn.Position.ToVector3Shifted().Yto0();
            IntVec3 intVec = target.Cell.ClampInsideMap(Pawn.Map);
            if (Pawn.Position == intVec)
            {
                return tmpCells;
            }
            float lengthHorizontal = (intVec - Pawn.Position).LengthHorizontal;
            float num = (float)(intVec.x - Pawn.Position.x) / lengthHorizontal;
            float num2 = (float)(intVec.z - Pawn.Position.z) / lengthHorizontal;
            intVec.x = Mathf.RoundToInt((float)Pawn.Position.x + num * Props.range);
            intVec.z = Mathf.RoundToInt((float)Pawn.Position.z + num2 * Props.range);
            float target2 = Vector3.SignedAngle(intVec.ToVector3Shifted().Yto0() - vector, Vector3.right, Vector3.up);
            float num3 = Props.lineWidthEnd / 2f;
            float num4 = Mathf.Sqrt(Mathf.Pow((intVec - Pawn.Position).LengthHorizontal, 2f) + Mathf.Pow(num3, 2f));
            float num5 = 57.29578f * Mathf.Asin(num3 / num4);
            int num6 = GenRadial.NumCellsInRadius(Props.range);
            for (int i = 0; i < num6; i++)
            {
                IntVec3 intVec2 = Pawn.Position + GenRadial.RadialPattern[i];
                if (CanUseCell(intVec2) && Mathf.Abs(Mathf.DeltaAngle(Vector3.SignedAngle(intVec2.ToVector3Shifted().Yto0() - vector, Vector3.right, Vector3.up), target2)) <= num5)
                {
                    tmpCells.Add(intVec2);
                }
            }
            List<IntVec3> list = GenSight.BresenhamCellsBetween(Pawn.Position, intVec);
            for (int j = 0; j < list.Count; j++)
            {
                IntVec3 intVec3 = list[j];
                if (!tmpCells.Contains(intVec3) && CanUseCell(intVec3))
                {
                    tmpCells.Add(intVec3);
                }
            }
            return tmpCells;
            bool CanUseCell(IntVec3 c)
            {
                if (!c.InBounds(Pawn.Map))
                {
                    return false;
                }
                if (c == Pawn.Position)
                {
                    return false;
                }
                if (!Props.canHitFilledCells && c.Filled(Pawn.Map))
                {
                    return false;
                }
                if (!c.InHorDistOf(Pawn.Position, Props.range))
                {
                    return false;
                }
                ShootLine resultingLine;
                return parent.verb.TryFindShootLineFromTo(parent.pawn.Position, c, out resultingLine);
            }
        }
    }


    public class EMP_CompProperties_AbilityWaterSpew : CompProperties_AbilityEffect
    {
        public float range;

        public float lineWidthEnd;

        public ThingDef filthDef;

        public int damAmount = -1;

        public EffecterDef effecterDef;

        public bool canHitFilledCells;

        public DamageDef damageDef;

        public bool damageFalloff = true;

        public EMP_CompProperties_AbilityWaterSpew()
        {
            compClass = typeof(EMP_CompAbilityEffect_WaterSpew);
        }
    }



    public class EMP_HeavyGravityPinhole : ThingWithComps
    {

        private const float radius = 8.9f;
        private const int UpdateHediffInterval = 30;

        protected override void Tick()
        {
            base.Tick();
            //if (!this.IsHashIntervalTick(UpdateHediffInterval))
            //{
            //    return;
            //}
            List<Pawn> allPawns = base.Map.mapPawns.AllPawns;
            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn pawn = allPawns[i];
                if ((pawn.RaceProps.Humanlike || pawn.RaceProps.Animal) && base.Position.InHorDistOf(pawn.PositionHeld, radius))
                {
                    ((EMP_Hediff_HeavyGravity)pawn.health.GetOrAddHediff(HediffDefOf.EMP_HeavyGravity)).lastTickInRangeOfGravPinhole = GenTicks.TicksGame;
                }

            }
        }


    }

    public static class HediffDefOf
    {
        public static HediffDef EMP_HeavyGravity;
    }

    public class EMP_Hediff_HeavyGravity : Hediff
    {
        public int lastTickInRangeOfGravPinhole;

        private const int PinholeBufferTicks = 60;

        public bool InRangeOfGravPinhole => GenTicks.TicksGame <= lastTickInRangeOfGravPinhole + PinholeBufferTicks;


        public override bool ShouldRemove
        {
            get
            {
                if (!InRangeOfGravPinhole)
                {
                    return base.ShouldRemove;
                }
                return false;
            }
        }
        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!InRangeOfGravPinhole)
            {
                pawn.health.RemoveHediff(this);
            }

        }


        //public override void ExposeData()
        //{
        //    base.ExposeData();
        //    Scribe_Values.Look(ref lastTickInRangeOfGravPinhole, "lastTickInRangeOfGravPinhole", 0);
        //}



    }


    public class EMP_CompAbilityEffect_LiquidSkip : CompAbilityEffect
    {

        private new EMP_CompProperties_AbilityLiquidSkip Props => (EMP_CompProperties_AbilityLiquidSkip)props;
        private Pawn Pawn => parent.pawn;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {


            ThingDef filthDef = Props.filthDef;
            int damAmount = Props.damAmount;
            DamageDef damageDef = Props.damageDef;
            FleckDef fleckDef = Props.fleckDef;
            bool Dousefire = Props.douseFire;

            Pawn pawn = Pawn;
            IntVec3 cell = target.Cell;
            Map mapHeld = parent.pawn.MapHeld;

            if (Props.fleckDef == null)
            {
                Log.Error("EvenMorePsycasts No FleckDef assigned for LiquidSkip ability.");
                return;
            }

            if (Props.filthDef == null)
            {
                Log.Error("EvenMorePsycasts No FleckDef assigned for LiquidSkip ability.");
                return;
            }

            SimpleCurve flammabilityAttachFireChanceCurve = parent.verb.verbProps.flammabilityAttachFireChanceCurve;
            GenExplosion.DoExplosion(cell, mapHeld, 0f, damageDef, pawn, damAmount, -1f, null, null, null, null, null, 1f, 1, null, null, 255, applyDamageToExplosionCellsNeighbors: false, null, 0f, 1, 1f, damageFalloff: false, null, null, null, doVisualEffects: false, 0.6f, 0f, doSoundEffects: false, null, 1f, flammabilityAttachFireChanceCurve);
            base.Apply(target, dest);
            Map map = parent.pawn.Map;


            

            foreach (IntVec3 item in AffectedCells(target, map))
            {
                if (!item.InBounds(map))
                {
                    continue;
                }
                List<Thing> thingList = item.GetThingList(map);
                for (int num = thingList.Count - 1; num >= 0; num--)
                {
                    if (thingList[num] is Fire && Dousefire == true)
                    {
                        thingList[num].Destroy();
                    }
                    else if (thingList[num] is Pawn otherpawn)
                    {
                        otherpawn.GetInvisibilityComp()?.DisruptInvisibility();
                    }
                }
                if (!item.Filled(map))
                {
                    FilthMaker.TryMakeFilth(item, map, filthDef);
                }

                //FleckMaker.Static(target.Cell, parent.pawn.Map, Props.fleckDef, 1f);
                FleckCreationData dataStatic = FleckMaker.GetDataStatic(item.ToVector3Shifted(), map, fleckDef);
                dataStatic.rotationRate = Rand.Range(-30, 30);
                dataStatic.rotation = 90 * Rand.RangeInclusive(0, 3);
                map.flecks.CreateFleck(dataStatic);
                CompAbilityEffect_Teleport.SendSkipUsedSignal(item, parent.pawn);
            }
        }

        private IEnumerable<IntVec3> AffectedCells(LocalTargetInfo target, Map map)
        {
            if (!target.Cell.InBounds(map) || target.Cell.Filled(parent.pawn.Map))
            {
                yield break;
            }
            foreach (IntVec3 item in GenRadial.RadialCellsAround(target.Cell, parent.def.EffectRadius, useCenter: true))
            {
                if (item.InBounds(map) && GenSight.LineOfSightToEdges(target.Cell, item, map, skipFirstCell: true))
                {
                    yield return item;
                }
            }
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            GenDraw.DrawFieldEdges(AffectedCells(target, parent.pawn.Map).ToList(), Valid(target) ? Color.white : Color.red);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (target.Cell.Filled(parent.pawn.Map))
            {
                if (throwMessages)
                {
                    Messages.Message("AbilityOccupiedCells".Translate(parent.def.LabelCap), target.ToTargetInfo(parent.pawn.Map), MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }
            return true;
        }

    }

    public class EMP_CompProperties_AbilityLiquidSkip : CompProperties_AbilityEffect
    {

        public ThingDef filthDef;

        public int damAmount = -1;

        public EffecterDef effecterDef;

        public DamageDef damageDef;

        public FleckDef fleckDef;

        public bool douseFire;


        public EMP_CompProperties_AbilityLiquidSkip()
        {
            compClass = typeof(EMP_CompAbilityEffect_WaterSpew);
        }

    }
}
