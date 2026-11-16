using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Prohect_DOD_Undergird.Combat;
using Prohect_DOD_Undergird.DOD.Combat;
using Prohect_DOD_Undergird.DOD.Entity;
using EntityId = Prohect_DOD_Undergird.DOD.Entity.EntityId;

namespace Prohect_DOD_Undergird.Player.Tests
{
    public class MeleeAttackResolverTests
    {
        private const int TargetHealth = 100;
        private const int AttackDamage = 10;

        private readonly List<GameObject> targetObjects = new List<GameObject>();
        private GameObject playerObject;
        private WeaponManager weaponManager;
        private WeaponAttack weaponAttack;
        private WeaponView weaponView;
        private MeleeAttackResolver resolver;
        private WeaponData weaponData;
        private EntityManager entityManager;
        private CombatManager combatManager;
        private int enemyLayer;

        [SetUp]
        public void SetUp()
        {
            enemyLayer = LayerMask.NameToLayer("Enemy");
            Assert.That(enemyLayer, Is.GreaterThanOrEqualTo(0), "Enemy Layer가 TagManager에 있어야 합니다.");

            entityManager = new EntityManager();
            combatManager = new CombatManager(entityManager);
            playerObject = new GameObject("MeleeAttackTestPlayer");
            weaponManager = playerObject.AddComponent<WeaponManager>();

            weaponData = ScriptableObject.CreateInstance<WeaponData>();
            SetField(weaponData, "weaponType", WeaponType.Melee);
            SetField(weaponData, "attackPower", AttackDamage);
            SetField(weaponData, "attackRange", 2f);
            SetField(weaponData, "attackAngle", 90f);
            SetField(weaponData, "thrustRange", 2f);
            SetField(weaponData, "thrustWidth", 0.5f);
            weaponManager.SetSlotWeapon(1, weaponData);

            weaponAttack = playerObject.AddComponent<WeaponAttack>();
            weaponAttack.SetAttackInput(false);

            GameObject weaponViewObject = new GameObject("WeaponView");
            weaponViewObject.transform.SetParent(playerObject.transform, false);
            weaponViewObject.AddComponent<SpriteRenderer>();
            weaponView = weaponViewObject.AddComponent<WeaponView>();
            SetField(weaponView, "aimCamera", (Camera)null);

            resolver = playerObject.AddComponent<MeleeAttackResolver>();
            LayerMask enemyLayerMask = 1 << enemyLayer;
            SetField(resolver, "enemyLayerMask", enemyLayerMask);
            resolver.ResolveAttack(null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(weaponData);

            for (int i = 0; i < targetObjects.Count; i++)
            {
                Object.DestroyImmediate(targetObjects[i]);
            }

            targetObjects.Clear();
        }

        [Test]
        public void AttackRequested_ArcDamagesMultipleTargetsOnlyInsideAngleAndRange()
        {
            SetField(weaponData, "meleeAttackType", MeleeAttackType.Arc);
            SetAimDirection(Vector2.right);

            CombatReceiver firstTarget = CreateTarget(new Vector2(1f, 0f));
            CombatReceiver secondTarget = CreateTarget(new Vector2(1f, 0.8f));
            CombatReceiver outsideAngle = CreateTarget(new Vector2(1f, 1.3f));
            CombatReceiver outsideRange = CreateTarget(new Vector2(2.5f, 0f));
            Physics2D.SyncTransforms();

            weaponAttack.SetAttackInput(true);

            Assert.That(firstTarget.CurrentHealth, Is.EqualTo(TargetHealth - AttackDamage));
            Assert.That(secondTarget.CurrentHealth, Is.EqualTo(TargetHealth - AttackDamage));
            Assert.That(outsideAngle.CurrentHealth, Is.EqualTo(TargetHealth));
            Assert.That(outsideRange.CurrentHealth, Is.EqualTo(TargetHealth));
        }

        [Test]
        public void ResolveAttack_ThrustDamagesTargetsInsideRectangleOnly()
        {
            SetField(weaponData, "meleeAttackType", MeleeAttackType.Thrust);
            SetAimDirection(Vector2.right);

            CombatReceiver inside = CreateTarget(new Vector2(1.5f, 0f));
            CombatReceiver outsideWidth = CreateTarget(new Vector2(1f, 0.4f));
            CombatReceiver outsideRange = CreateTarget(new Vector2(2.3f, 0f));
            Physics2D.SyncTransforms();

            resolver.ResolveAttack(weaponData);

            Assert.That(inside.CurrentHealth, Is.EqualTo(TargetHealth - AttackDamage));
            Assert.That(outsideWidth.CurrentHealth, Is.EqualTo(TargetHealth));
            Assert.That(outsideRange.CurrentHealth, Is.EqualTo(TargetHealth));
        }

        [Test]
        public void ResolveAttack_MultipleCollidersOnOneEnemy_DealsDamageOnce()
        {
            SetField(weaponData, "meleeAttackType", MeleeAttackType.Arc);
            SetAimDirection(Vector2.right);
            CombatReceiver target = CreateTarget(new Vector2(1f, 0f), colliderCount: 3);
            Physics2D.SyncTransforms();

            resolver.ResolveAttack(weaponData);

            Assert.That(target.CurrentHealth, Is.EqualTo(TargetHealth - AttackDamage));
        }

        [Test]
        public void ResolveAttack_UninitializedReceiver_IsIgnored()
        {
            SetAimDirection(Vector2.right);
            CombatReceiver uninitializedTarget = CreateTarget(new Vector2(1f, 0f), initializeReceiver: false);
            Physics2D.SyncTransforms();

            resolver.ResolveAttack(weaponData);

            Assert.That(uninitializedTarget.CurrentHealth, Is.Zero);
            Assert.That(uninitializedTarget.IsDead, Is.True);
        }

        [Test]
        public void ResolveAttack_ColliderOutsideEnemyLayer_IsIgnored()
        {
            SetAimDirection(Vector2.right);
            CombatReceiver nonEnemy = CreateTarget(new Vector2(1f, 0f), layer: 0);
            Physics2D.SyncTransforms();

            resolver.ResolveAttack(weaponData);

            Assert.That(nonEnemy.CurrentHealth, Is.EqualTo(TargetHealth));
        }

        [Test]
        public void ResolveAttack_ExpandsResultList_WithoutDroppingTargets()
        {
            SetAimDirection(Vector2.right);
            CombatReceiver[] targets = new CombatReceiver[24];
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i] = CreateTarget(new Vector2(1f + i * 0.01f, 0f));
            }

            Physics2D.SyncTransforms();
            resolver.ResolveAttack(weaponData);

            for (int i = 0; i < targets.Length; i++)
            {
                Assert.That(targets[i].CurrentHealth, Is.EqualTo(TargetHealth - AttackDamage));
            }
        }

        [Test]
        public void ResolveAttack_NonMeleeWeapon_IsIgnored()
        {
            SetField(weaponData, "weaponType", WeaponType.Projectile);
            SetAimDirection(Vector2.right);
            CombatReceiver target = CreateTarget(new Vector2(1f, 0f));
            Physics2D.SyncTransforms();

            resolver.ResolveAttack(weaponData);

            Assert.That(target.CurrentHealth, Is.EqualTo(TargetHealth));
        }

        [Test]
        public void AttackRequested_UsesWeaponViewAimDirection()
        {
            SetField(weaponData, "meleeAttackType", MeleeAttackType.Thrust);
            SetAimDirection(Vector2.up);
            CombatReceiver targetInAimDirection = CreateTarget(new Vector2(0f, 1.5f));
            CombatReceiver targetInPreviousDirection = CreateTarget(new Vector2(1.5f, 0f));
            Physics2D.SyncTransforms();

            weaponAttack.SetAttackInput(true);

            Assert.That(targetInAimDirection.CurrentHealth, Is.EqualTo(TargetHealth - AttackDamage));
            Assert.That(targetInPreviousDirection.CurrentHealth, Is.EqualTo(TargetHealth));
        }

        private CombatReceiver CreateTarget(
            Vector2 position,
            int colliderCount = 1,
            bool initializeReceiver = true,
            int layer = -1)
        {
            GameObject targetObject = new GameObject("EnemyTarget");
            targetObject.transform.position = position;
            targetObjects.Add(targetObject);

            CombatReceiver receiver = targetObject.AddComponent<CombatReceiver>();
            if (initializeReceiver)
            {
                EntityId entityId = entityManager.Create();
                receiver.Initialize(entityId, combatManager, TargetHealth);
            }

            for (int i = 0; i < colliderCount; i++)
            {
                GameObject colliderObject = new GameObject("EnemyCollider");
                colliderObject.layer = layer < 0 ? enemyLayer : layer;
                colliderObject.transform.SetParent(targetObject.transform, false);
                colliderObject.transform.localPosition = new Vector3(i * 0.05f, 0f, 0f);
                CircleCollider2D collider = colliderObject.AddComponent<CircleCollider2D>();
                collider.radius = 0.1f;
            }

            return receiver;
        }

        private void SetAimDirection(Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            weaponView.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static void SetField<T>(Object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"{fieldName} 필드를 찾을 수 없습니다.");
            Assert.That(
                field.FieldType,
                Is.EqualTo(typeof(T)),
                $"{fieldName} 필드 타입과 전달한 값 타입이 다릅니다.");
            field.SetValue(target, value);
        }
    }
}
