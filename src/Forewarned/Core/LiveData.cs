using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Forewarned.Core.Model;
using UnityEngine;

namespace Forewarned.Core
{
    /// <summary>
    /// PLAN.md §11.3: reads each ability's numbers from the boss's own inventory item, which every
    /// client holds (Humanoid.GiveDefaultItems runs everywhere). Missing items or components fall back
    /// to the offline numbers and are logged once.
    /// </summary>
    internal static class LiveData
    {
        private static readonly HashSet<string> Warned = new HashSet<string>();
        /// <summary>Prefabs whose live numbers have already been logged this session; the numbers
        /// themselves are still (re)set in the engine on every call.</summary>
        private static readonly HashSet<string> Logged = new HashSet<string>();

        /// <returns>False while the boss's inventory isn't filled yet, so BossWatch retries on its next scan.</returns>
        public static bool Read(Humanoid boss, string prefab)
        {
            if (boss == null)
                return true;
            List<ItemDrop.ItemData> items = boss.GetInventory()?.GetAllItems();
            if (items == null || items.Count == 0)
                return false;
            // ObjectDB may not have filled in every item's m_dropPrefab yet: that's not a missing
            // ability, it's not ready, so retry on the next scan instead of warning per ability.
            bool objectDbNotReady = items.TrueForAll(i => i.m_dropPrefab == null);
            if (objectDbNotReady)
            {
                if (Warned.Add(prefab + "/objectdb"))
                    ForewarnedPlugin.Log.LogWarning("Forewarned: " + prefab + "'s items have no drop prefab (ObjectDB not ready?); using offline numbers");
                return false;
            }
            BossModule module = Runtime.Engine.Registry.ModuleFor(prefab);
            StringBuilder log = Logged.Add(prefab) ? new StringBuilder("Forewarned: live numbers for " + prefab + ":") : null;
            foreach (AbilitySpec a in module.Abilities)
            {
                ItemDrop.ItemData item = items.Find(i => i.m_dropPrefab != null && i.m_dropPrefab.name == a.ItemPrefab);
                if (item == null)
                {
                    if (Warned.Add(prefab + "/" + a.ItemPrefab))
                        ForewarnedPlugin.Log.LogWarning("Forewarned: " + prefab + " has no item " + a.ItemPrefab + "; using offline numbers for " + a.Id);
                    continue;
                }
                AbilityNumbers n = Numbers(item.m_shared, a);
                Runtime.Engine.SetLiveNumbers(prefab, a.Id, n);
                if (log == null)
                    continue;
                log.Append(" ").Append(a.Id).Append(" cd ").Append(F(n.Cooldown)).Append(" range ").Append(F(n.AiRange))
                   .Append(" maxangle ").Append(F(n.MaxAngle)).Append(" hp ").Append(F(n.HpMin)).Append("-").Append(F(n.HpMax));
                if (n.Radius.HasValue) log.Append(" r ").Append(F(n.Radius));
                if (n.Range.HasValue) log.Append(" len ").Append(F(n.Range));
                if (n.Angle.HasValue) log.Append(" angle ").Append(F(n.Angle));
                if (n.Width.HasValue) log.Append(" width ").Append(F(n.Width));
                if (n.Offset.HasValue) log.Append(" offset ").Append(F(n.Offset));
                log.Append(";");
            }
            if (log != null)
                ForewarnedPlugin.Log.LogInfo(log.ToString());
            return true;
        }

        private static AbilityNumbers Numbers(ItemDrop.ItemData.SharedData s, AbilitySpec a)
        {
            var n = new AbilityNumbers
            {
                Cooldown = s.m_aiAttackInterval,
                HpMin = s.m_aiMinHealthPercentage,
                HpMax = s.m_aiMaxHealthPercentage,
                AiRange = s.m_aiAttackRange,
                MaxAngle = s.m_aiAttackMaxAngle
            };
            Attack at = s.m_attack;
            if (at == null)
                return n;
            switch (a.Shape.Source)
            {
                case ShapeSource.AttackCone:
                    n.Range = at.m_attackRange;
                    n.Angle = at.m_attackAngle;
                    break;
                case ShapeSource.AttackSphere:
                    n.Radius = at.m_attackRayWidth;
                    n.Offset = at.m_attackRange;
                    break;
                case ShapeSource.SpawnAbility:
                    SpawnShape(at.m_attackProjectile, n);
                    break;
                case ShapeSource.Aoe:
                    AoeShape(at.m_attackProjectile, n);
                    break;
            }
            return n;
        }

        /// <summary>A spread spawner (meteors, wall of fire) is sized by its spawn radius; a spawner that
        /// drops its AoEs on the target (fissure) by the AoE's own radius.</summary>
        private static void SpawnShape(GameObject projectile, AbilityNumbers n)
        {
            SpawnAbility sa = projectile != null ? projectile.GetComponent<SpawnAbility>() : null;
            if (sa == null)
                return;
            if (sa.m_spawnRadius > 0f)
            {
                n.Radius = sa.m_spawnRadius;
                return;
            }
            if (sa.m_spawnPrefab == null)
                return;
            foreach (GameObject spawned in sa.m_spawnPrefab)
            {
                Aoe aoe = spawned != null ? spawned.GetComponent<Aoe>() : null;
                if (aoe != null)
                {
                    n.Radius = aoe.m_radius;
                    return;
                }
            }
        }

        /// <summary>A trigger-box AoE (flame breath) gives a strip; a sphere AoE gives a circle.</summary>
        private static void AoeShape(GameObject projectile, AbilityNumbers n)
        {
            if (projectile == null)
                return;
            BoxCollider box = projectile.GetComponentInChildren<BoxCollider>(true);
            Aoe aoe = projectile.GetComponentInChildren<Aoe>(true);
            if (aoe != null && aoe.m_useTriggers && box != null)
            {
                n.Range = box.size.z;
                n.Width = box.size.x;
            }
            else if (aoe != null)
                n.Radius = aoe.m_radius;
        }

        private static string F(float? v) => v.HasValue ? v.Value.ToString("0.##", CultureInfo.InvariantCulture) : "-";
    }
}
