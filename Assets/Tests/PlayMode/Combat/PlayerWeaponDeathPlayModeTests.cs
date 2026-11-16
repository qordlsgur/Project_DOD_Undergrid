using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Prohect_DOD_Undergird.Combat;
using Prohect_DOD_Undergird.Player;

namespace Prohect_DOD_Undergird.Combat.Tests
{
    public class PlayerWeaponDeathPlayModeTests
    {
        private GameObject bootstrapObject;
        private GameObject playerObject;
        private GameObject weaponViewObject;
        private GameObject cameraObject;
        private WeaponData weaponData;
        private WeaponView weaponView;
        private WeaponAttack weaponAttack;
        private MeleeAttackResolver meleeAttackResolver;
        private CombatReceiver playerReceiver;

        [UnityTest]
        public IEnumerator Death_StopsWeaponAimAndHeldAttackButLivingPlayerKeepsBoth()
        {
            CombatBootstrap bootstrap = CreateBootstrap();
            yield return null;

            CreatePlayer(bootstrap);
            int attackRequestCount = 0;
            weaponAttack.OnAttackRequested += _ => attackRequestCount++;

            weaponAttack.SetAttackInput(true);
            Assert.That(attackRequestCount, Is.EqualTo(1));
            yield return new WaitForSeconds(0.12f);

            Assert.That(attackRequestCount, Is.GreaterThan(1));
            Assert.That(weaponView.enabled, Is.True);
            Assert.That(weaponAttack.enabled, Is.True);
            Assert.That(meleeAttackResolver.enabled, Is.True);

            yield return null;
            weaponView.transform.position = playerObject.transform.position;
            yield return new WaitForEndOfFrame();
            Assert.That(
                Vector2.Distance(playerObject.transform.position, weaponView.transform.position),
                Is.EqualTo(0.5f).Within(0.01f));

            int requestsAtDeath = attackRequestCount;
            Assert.That(playerReceiver.ReceiveDamage(playerReceiver.CurrentHealth), Is.True);
            Assert.That(playerReceiver.IsDead, Is.True);
            Assert.That(weaponView.enabled, Is.False);
            Assert.That(weaponAttack.enabled, Is.False);
            Assert.That(meleeAttackResolver.enabled, Is.False);

            Vector3 frozenWeaponPosition = new Vector3(4f, 3f, 0f);
            weaponView.transform.position = frozenWeaponPosition;
            yield return new WaitForSeconds(0.12f);

            Assert.That(attackRequestCount, Is.EqualTo(requestsAtDeath));
            Assert.That(weaponView.transform.position, Is.EqualTo(frozenWeaponPosition));
            Assert.That(playerObject == null, Is.False);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (playerObject != null)
            {
                Object.Destroy(playerObject);
            }

            if (bootstrapObject != null)
            {
                Object.Destroy(bootstrapObject);
            }

            if (cameraObject != null)
            {
                Object.Destroy(cameraObject);
            }

            if (weaponData != null)
            {
                Object.Destroy(weaponData);
            }

            yield return null;
        }

        private CombatBootstrap CreateBootstrap()
        {
            bootstrapObject = new GameObject("WeaponDeathTestBootstrap");
            return bootstrapObject.AddComponent<CombatBootstrap>();
        }

        private void CreatePlayer(CombatBootstrap bootstrap)
        {
            cameraObject = new GameObject("WeaponDeathTestCamera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(-100f, -100f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;

            playerObject = new GameObject("WeaponDeathTestPlayer");
            playerReceiver = playerObject.AddComponent<CombatReceiver>();
            PlayerCombatInitializer initializer = playerObject.AddComponent<PlayerCombatInitializer>();
            SetField(initializer, "combatBootstrap", bootstrap);
            Assert.That(initializer.Initialize(), Is.True);

            AudioSource inputHandler = playerObject.AddComponent<AudioSource>();
            Animator movement = playerObject.AddComponent<Animator>();

            WeaponManager weaponManager = playerObject.AddComponent<WeaponManager>();
            weaponData = ScriptableObject.CreateInstance<WeaponData>();
            SetField(weaponData, "attackInterval", 0.05f);
            weaponManager.SetSlotWeapon(1, weaponData);
            weaponAttack = playerObject.AddComponent<WeaponAttack>();

            weaponViewObject = new GameObject("WeaponView");
            weaponViewObject.SetActive(false);
            weaponViewObject.transform.SetParent(playerObject.transform, false);
            weaponViewObject.AddComponent<SpriteRenderer>();
            weaponView = weaponViewObject.AddComponent<WeaponView>();
            SetField(weaponView, "weaponManager", weaponManager);
            weaponViewObject.SetActive(true);

            meleeAttackResolver = playerObject.AddComponent<MeleeAttackResolver>();
            PlayerDeathHandler deathHandler = playerObject.AddComponent<PlayerDeathHandler>();
            SetField(deathHandler, "playerInputHandler", inputHandler);
            SetField(deathHandler, "playerMovement", movement);
            SetField(deathHandler, "weaponView", weaponView);
            SetField(deathHandler, "weaponAttack", weaponAttack);
            SetField(deathHandler, "meleeAttackResolver", meleeAttackResolver);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
