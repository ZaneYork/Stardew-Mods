﻿using Netcode;
using System.Linq;
using StardewValley;
using Microsoft.Xna.Framework;
using StardewValley.TerrainFeatures;
using StardewValley.Buildings;
using StardewValley.Characters;
using SObject = StardewValley.Object;

namespace BetterJunimos {
    public class JunimoPlanter {
        public static bool IsPlantable(Vector2 pos, JunimoHut hut) {
            Farm farm = Game1.getFarm();
            return farm.terrainFeatures.ContainsKey(pos) && farm.terrainFeatures[pos] is HoeDirt hd &&
                hd.crop == null && !farm.objects.ContainsKey(pos) && AreThereSeeds(hut);
        }

        public static bool AreThereSeeds(JunimoHut hut) {
            Farm farm = Game1.getFarm();
            NetObjectList<Item> chest = hut.output.Value.items;
            return chest.Any(item => item.category == SObject.SeedsCategory);
        }

        public static JunimoHut GetHutFromJunimo(JunimoHarvester junimo) {
            NetGuid netHome = BetterJunimos.instance.Helper.Reflection.GetField<NetGuid>(junimo, "netHome").GetValue();
            return Game1.getFarm().buildings[netHome.Value] as JunimoHut;
        }

        public static void UseItemFromHut(JunimoHut hut, Vector2 pos) {
            Farm farm = Game1.getFarm();
            NetObjectList<Item> chest = hut.output.Value.items;

            Item fertilizer = chest.FirstOrDefault(item => item.Category == SObject.fertilizerCategory);
            Item seeds = chest.FirstOrDefault(item => item.Category == SObject.SeedsCategory);

            if (farm.terrainFeatures[pos] is HoeDirt hd && BetterJunimos.instance.Config.FertilizeCrops) {
                if (fertilizer != null && hd.fertilizer.Value <= 0) {
                    hd.plant(fertilizer.ParentSheetIndex, (int)pos.X, (int)pos.Y, Game1.player, true, farm);
                    ReduceItemCount(chest, fertilizer);
                }
                else if (seeds != null && BetterJunimos.instance.Config.PlantCrops) {
                    hd.plant(seeds.ParentSheetIndex, (int)pos.X, (int)pos.Y, Game1.player, false, farm);
                    ReduceItemCount(chest, seeds);
                }
            }
        }

        private static void ReduceItemCount(NetObjectList<Item> chest, Item item) {
            if (!BetterJunimos.instance.Config.ConsumeItemsFromJunimoHut) { return; }
            item.Stack--;
            if (item.Stack == 0) {
                chest.Remove(item);
            }
        }
    }

    // JunimoHarvester - foundCropEndFunction
    public class PatchFindingCropEnd {
        public static void Postfix(ref PathNode currentNode, ref GameLocation location, JunimoHarvester __instance, ref bool __result) {
            JunimoHut hut = JunimoPlanter.GetHutFromJunimo(__instance);
            __result = __result || JunimoPlanter.IsPlantable(new Vector2(currentNode.x, currentNode.y), hut);
        }
    }

    // JunimoHarvester - tryToHarvestHere
    public class PatchHarvestAttemptToCustom {
        public static void Postfix(JunimoHarvester __instance) {
            Vector2 pos = __instance.getTileLocation();
            JunimoHut hut = JunimoPlanter.GetHutFromJunimo(__instance);
            if (JunimoPlanter.IsPlantable(pos, hut)) {
                var harvestTimer = BetterJunimos.instance.Helper.Reflection.GetField<int>(__instance, "harvestTimer");
                harvestTimer.SetValue(2000);
                JunimoPlanter.UseItemFromHut(hut, pos);
            }
        }
    }

    /* JunimoHarvester - update
    [HarmonyPatch(new Type[] { typeof(GameTime), typeof(GameLocation) })]
    public class PatchUpdateAndPlant {
        static void Postfix(JunimoHarvester __instance) {
        }
    }*/

    // JunimoHut - areThereMatureCropsWithinRadius
    public class PatchPathfindHut {
        public static void Postfix(JunimoHut __instance, ref bool __result) {
            if (__instance.lastKnownCropLocation.Equals(Point.Zero)) {
                __result = pathFindTilledDirt(__instance);
            }
        }

        internal static bool pathFindTilledDirt(JunimoHut hut) {
            for (int index1 = (int)((NetFieldBase<int, NetInt>)hut.tileX) + 1 - 8; index1 < (int)((NetFieldBase<int, NetInt>)hut.tileX) + 2 + 8; ++index1) {
                for (int index2 = (int)((NetFieldBase<int, NetInt>)hut.tileY) - 8 + 1; index2 < (int)((NetFieldBase<int, NetInt>)hut.tileY) + 2 + 8; ++index2) {
                    Vector2 pos = new Vector2((float)index1, (float)index2);
                    if (JunimoPlanter.IsPlantable(pos, hut)) {
                        hut.lastKnownCropLocation = new Point(index1, index2);
                        return true;
                    }
                }
            }
            return false;
        }
    }

    public class PatchJunimosInRain {
        public static void Postfix(JunimoHut __instance) {
            var junimoSendOutTimer = BetterJunimos.instance.Helper.Reflection.GetField<int>(__instance, "junimoSendOutTimer");
            if (junimoSendOutTimer.GetValue() > 0 || __instance.myJunimos.Count<JunimoHarvester>() >= 3 || Game1.IsWinter || (!__instance.areThereMatureCropsWithinRadius() || Game1.farmEvent != null)) 
                return;
            junimoSendOutTimer.SetValue(5000);
            BetterJunimos.instance.spawnJunimoAtHut(__instance);
        }
    }
}
