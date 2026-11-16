using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Prohect_DOD_Undergird.DOD.Combat;
using Prohect_DOD_Undergird.Combat;

namespace Prohect_DOD_Undergird.Combat.Tests
{
    public class CombatBootstrapPlayModeTests
    {
        private GameObject bootstrapObject;

        [UnityTest]
        public IEnumerator Awake_CreatesSharedManagers()
        {
            bootstrapObject = new GameObject("CombatBootstrapPlayModeTest");
            CombatBootstrap bootstrap = bootstrapObject.AddComponent<CombatBootstrap>();

            yield return null;

            Assert.That(bootstrap.EntityManager, Is.Not.Null);
            Assert.That(bootstrap.CombatManager, Is.Not.Null);

            FieldInfo entityManagerField = typeof(CombatManager).GetField(
                "entityManager",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(entityManagerField, Is.Not.Null);
            Assert.That(entityManagerField.GetValue(bootstrap.CombatManager), Is.SameAs(bootstrap.EntityManager));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (bootstrapObject != null)
            {
                Object.Destroy(bootstrapObject);
                yield return null;
            }
        }
    }
}
