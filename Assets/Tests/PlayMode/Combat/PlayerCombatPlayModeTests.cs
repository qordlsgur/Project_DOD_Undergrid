using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Prohect_DOD_Undergird.Combat;

namespace Prohect_DOD_Undergird.Combat.Tests
{
    public class PlayerCombatPlayModeTests
    {
        private GameObject bootstrapObject;
        private GameObject playerObject;

        [UnityTest]
        public IEnumerator Initialize_RegistersPlayerWithDefaultHealthAndPreventsDuplicateEntity()
        {
            CombatBootstrap bootstrap = CreateBootstrap();
            yield return null;

            playerObject = new GameObject("PlayerCombatTestPlayer");
            CombatReceiver receiver = playerObject.AddComponent<CombatReceiver>();
            PlayerCombatInitializer initializer = playerObject.AddComponent<PlayerCombatInitializer>();
            SetField(initializer, "combatBootstrap", bootstrap);

            Assert.That(initializer.Initialize(), Is.True);
            Assert.That(receiver.CurrentHealth, Is.EqualTo(100));
            Assert.That(receiver.MaxHealth, Is.EqualTo(100));
            Assert.That(bootstrap.EntityManager.IsAlive(initializer.EntityId), Is.True);
            Assert.That(receiver.EntityId, Is.EqualTo(initializer.EntityId));

            Prohect_DOD_Undergird.DOD.Entity.EntityId firstId = initializer.EntityId;
            Assert.That(initializer.Initialize(), Is.False);
            Assert.That(initializer.EntityId, Is.EqualTo(firstId));
            Assert.That(receiver.CurrentHealth, Is.EqualTo(100));
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

            yield return null;
        }

        private CombatBootstrap CreateBootstrap()
        {
            bootstrapObject = new GameObject("PlayerCombatTestBootstrap");
            return bootstrapObject.AddComponent<CombatBootstrap>();
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

    public class PlayerDeathHandlerPlayModeTests
    {
        private GameObject bootstrapObject;
        private GameObject playerObject;

        [UnityTest]
        public IEnumerator OnDied_DisablesPlayerMovementAndInputOnce()
        {
            bootstrapObject = new GameObject("PlayerDeathTestBootstrap");
            CombatBootstrap bootstrap = bootstrapObject.AddComponent<CombatBootstrap>();
            yield return null;

            playerObject = new GameObject("PlayerDeathTestPlayer");
            CombatReceiver receiver = playerObject.AddComponent<CombatReceiver>();
            PlayerCombatInitializer initializer = playerObject.AddComponent<PlayerCombatInitializer>();
            SetField(initializer, "combatBootstrap", bootstrap);
            AudioSource movement = playerObject.AddComponent<AudioSource>();
            Animator inputHandler = playerObject.AddComponent<Animator>();
            PlayerDeathHandler deathHandler = playerObject.AddComponent<PlayerDeathHandler>();
            SetField(deathHandler, "playerMovement", movement);
            SetField(deathHandler, "playerInputHandler", inputHandler);

            yield return null;
            Assert.That(receiver.MaxHealth, Is.EqualTo(100));
            Assert.That(movement.enabled, Is.True);
            Assert.That(inputHandler, Is.Not.Null);
            Assert.That(inputHandler.enabled, Is.True);

            int deathEventCount = 0;
            receiver.OnDied += () => deathEventCount++;
            Assert.That(receiver.ReceiveDamage(100), Is.True);
            Assert.That(movement.enabled, Is.False);
            Assert.That(inputHandler.enabled, Is.False);
            Assert.That(deathEventCount, Is.EqualTo(1));
            Assert.That(receiver.ReceiveDamage(1), Is.False);
            Assert.That(deathEventCount, Is.EqualTo(1));
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

            yield return null;
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
