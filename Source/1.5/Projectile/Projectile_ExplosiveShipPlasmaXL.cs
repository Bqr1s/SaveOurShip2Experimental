using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;

namespace SaveOurShip2
{
    class Projectile_ExplosiveShipPlasmaXL : Projectile_ExplosiveShip
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {

            float dx = destination.x - origin.x;
            float dz = destination.z - origin.z;
            float length = (destination - origin).magnitude;
            dx /= length;
            dz /= length;
            IntVec3 forwardPosition = base.Position;
            forwardPosition.x += (int)(dx * 6);
            forwardPosition.z += (int)(dz * 6);
            //Log.Warning("++Spinal primary location:: " + Position.ToString());
            Map map = base.Map;


            Building b = hitThing as Building;
            CellRect buildingRect = new CellRect();
            if (b != null)
            {
                buildingRect = b.OccupiedRect();
                //Log.Warning("HP Before hit:" + b.hitPointsInt);

            }
            hitThing.TakeDamage(new DamageInfo(DefDatabase<DamageDef>.GetNamed("ShipPlasmaLarge"), DamageAmount));
            // Clean up slags left after (maybe) destroyed building, so they don't mess with explosion and not reduce penetration specifically for spinal plasma,
            // which is meant to be formidable weapon against hull
            foreach (IntVec3 cell in buildingRect)
            {
                List<Thing> debrisList = cell.GetThingList(map);
                for (int i = 0; i < debrisList.Count; i++)
                {
                    if (debrisList[i] != hitThing)
                        debrisList[i].TakeDamage(new DamageInfo(DefDatabase<DamageDef>.GetNamed("ShipPlasmaLarge"), DamageAmount));
                }
            }
            base.Impact(hitThing);

            //GenExplosion.DoExplosion(base.Position, map, base.def.projectile.explosionRadius, DefDatabase<DamageDef>.GetNamed("ShipPlasmaLarge"), base.launcher, base.DamageAmount, base.ArmorPenetration, null, base.equipmentDef, base.def, postExplosionSpawnThingDef: ThingDefOf.Filth_Fuel, intendedTarget: intendedTarget.Thing, postExplosionSpawnChance: 0.2f, postExplosionSpawnThingCount: 1, applyDamageToExplosionCellsNeighbors: false, preExplosionSpawnThingDef: null, preExplosionSpawnChance: 0f, preExplosionSpawnThingCount: 1, chanceToStartFire: 0.4f);
            if (b != null)
            {
                //Log.Warning("HP After hit:" + b.hitPointsInt);
            }
            CellRect cellRect = CellRect.CenteredOn(forwardPosition, 10);
            cellRect.ClipInsideMap(map);
            for (int i = 0; i < 5; i++)
            {
                IntVec3 randomCell = cellRect.RandomCell;
                DoFireExplosionTo(base.Position, randomCell, map, 3.3f);
            }
            // Log.Warning("Spinal plasma projectile with dmg: " + DamageAmount);
            // Destroy();
        }

        protected void DoFireExplosionTo(IntVec3 originalPos, IntVec3 targetPos, Map map, float radius)
        {
            // disallow moving along exactly 45 deg diagonal, as may pass between diagonally-adjacent walls
            if (targetPos.x - originalPos.x == targetPos.z - originalPos.z)
            {
                if (targetPos.z > originalPos.z)
                    targetPos.z--;
                else
                    targetPos.z++;
            }
            IntVec3 pos = targetPos;
            // When no path from primary hit location to target explosion, fall back to line traversal, as simplest pathfinding doesn't give an approximation where plasma
            // would stop on the way to target
            float beforePathfinding = Time.realtimeSinceStartup;
            if (!HasPlasmaPath(originalPos, targetPos, map))
            {
                pos = originalPos;
                Log.Warning("Path Time: " + (Time.realtimeSinceStartup - beforePathfinding));
                // Traverse map from original pos (spinal hit itself) to target pos (random tile nearby), fairly stopping at impassable objects
                const int steps = 300;
                for (int i = 0; i < steps; i++)
                {
                    IntVec3 newPos = originalPos;
                    float dx = targetPos.x - originalPos.x;
                    float dz = targetPos.z - originalPos.z;
                    newPos.x += (int)Mathf.Round(dx * (float)i / steps);
                    newPos.z += (int)Mathf.Round(dz * (float)i / steps);
                    // move along the supposed plasma path in very small steps, doing checks only when actually moved to next tile 
                    if (newPos == pos)
                        continue;
                    if (newPos.Impassable(map) || newPos.GetRoom(map).IsDoorway)
                        break;
                    // assign after check, so that explosion happens before impassable tile, not in it, if not original ile.
                    pos = newPos;
                }
            }
            //Log.Warning("Spinal secondary location:: " + pos.ToString());
            GenExplosion.DoExplosion(pos, map, radius * Mathf.Min(weaponDamageMultiplier, 2), DefDatabase<DamageDef>.GetNamed("ShipPlasmaSmall"), launcher, base.DamageAmount, base.ArmorPenetration, null, equipmentDef, def, intendedTarget.Thing);
        }
        protected bool HasPlasmaPath(IntVec3 originalPos, IntVec3 targetPos, Map map)
        {
            //Log.Warning("Pathfinding");
            const int depth = 25;
            List<IntVec3> prevFront = new List<IntVec3>();
            List<IntVec3> front = new List<IntVec3>();
            front.Add(originalPos);
            for (int i = 0; i < depth; i++)
            {
                List<IntVec3> nextFront = new List<IntVec3>();
                foreach (IntVec3 cell in front)
                {
                    IEnumerable<IntVec3> adjacent = GenAdj.CellsAdjacentCardinal(cell, new Rot4(Rot4.NorthInt), new IntVec2(1, 1));
                    //Log.Error(adjacent.Count().ToString());
                    //return false;
                    foreach (IntVec3 adjacentCell in adjacent)
                    {
                        if (prevFront.Contains(adjacentCell) || front.Contains(adjacentCell) || nextFront.Contains(adjacentCell))
                            continue;
                        if (adjacentCell.Impassable(map) || (bool)(adjacentCell.GetRoom(map)?.IsDoorway ?? false))
                            continue;
                        // if candidate cell is too far away to reach target within search depth
                        if (Math.Abs(targetPos.x - adjacentCell.x) + Math.Abs(targetPos.z - adjacentCell.z) > depth - i - 1)
                            continue;
                        nextFront.Add(adjacentCell);
                    }
                }
                if (nextFront.Contains(targetPos))
                    return true;
                prevFront = front;
                front = nextFront;
                // Log.Warning("FC:" + front.Count());
                if (front.Count() == 0)
                    return false;
            }
            return false;
        }
    }
}

