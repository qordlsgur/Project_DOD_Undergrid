using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Prohect_DOD_Undergird.Player.Tests
{
    public class WeaponAttackTests
    {
        private GameObject playerObject;
        private WeaponManager weaponManager;
        private WeaponAttack weaponAttack;
        private WeaponData firstWeapon;
        private WeaponData secondWeapon;

        [SetUp]
        public void SetUp()
        {
            playerObject = new GameObject("WeaponAttackTestPlayer");
            weaponManager = playerObject.AddComponent<WeaponManager>();
            weaponAttack = playerObject.AddComponent<WeaponAttack>();
            firstWeapon = ScriptableObject.CreateInstance<WeaponData>();
            secondWeapon = ScriptableObject.CreateInstance<WeaponData>();

            weaponManager.SetSlotWeapon(1, firstWeapon);
            weaponManager.SetSlotWeapon(2, secondWeapon);

            Assert.That(weaponManager.CurrentWeapon, Is.SameAs(firstWeapon));

            // EditMode 수명 주기 콜백이 지연되어도 공개 입력 경로에서 참조와 이벤트를 초기화합니다.
            weaponAttack.SetAttackInput(false);
            FieldInfo weaponManagerField = typeof(WeaponAttack).GetField(
                "weaponManager",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(weaponManagerField, Is.Not.Null);
            Assert.That(weaponManagerField.GetValue(weaponAttack), Is.SameAs(weaponManager));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(firstWeapon);
            Object.DestroyImmediate(secondWeapon);
        }

        [Test]
        public void SetAttackInput_PressedWithWeapon_RequestsAttackImmediately()
        {
            weaponManager.SelectSlot(1);
            int attackCount = 0;
            weaponAttack.OnAttackRequested += weapon => attackCount++;

            weaponAttack.SetAttackInput(true);

            Assert.That(attackCount, Is.EqualTo(1));
        }

        [Test]
        public void UpdateAttackCycle_HeldInput_RequestsAttackAtWeaponInterval()
        {
            weaponManager.SelectSlot(1);
            int attackCount = 0;
            weaponAttack.OnAttackRequested += weapon => attackCount++;
            weaponAttack.SetAttackInput(true);
            float firstRepeatTime = Time.time + firstWeapon.AttackInterval;

            InvokeAttackCycle(firstRepeatTime - 0.001f);
            Assert.That(attackCount, Is.EqualTo(1));

            InvokeAttackCycle(firstRepeatTime);
            Assert.That(attackCount, Is.EqualTo(2));

            InvokeAttackCycle(firstRepeatTime + firstWeapon.AttackInterval);
            Assert.That(attackCount, Is.EqualTo(3));
        }

        [Test]
        public void SetAttackInput_Released_StopsRepeatingAttacks()
        {
            weaponManager.SelectSlot(1);
            int attackCount = 0;
            weaponAttack.OnAttackRequested += weapon => attackCount++;
            weaponAttack.SetAttackInput(true);
            weaponAttack.SetAttackInput(false);

            InvokeAttackCycle(Time.time + firstWeapon.AttackInterval * 2f);

            Assert.That(attackCount, Is.EqualTo(1));
        }

        [Test]
        public void WeaponChangedWhileHeld_CancelsUntilInputIsReleasedAndPressedAgain()
        {
            weaponManager.SelectSlot(1);
            int attackCount = 0;
            WeaponData lastAttackedWeapon = null;
            weaponAttack.OnAttackRequested += weapon =>
            {
                attackCount++;
                lastAttackedWeapon = weapon;
            };
            weaponAttack.SetAttackInput(true);

            weaponManager.SelectSlot(2);
            weaponAttack.SetAttackInput(true);
            InvokeAttackCycle(Time.time + firstWeapon.AttackInterval * 2f);
            Assert.That(attackCount, Is.EqualTo(1));

            weaponAttack.SetAttackInput(false);
            weaponAttack.SetAttackInput(true);

            Assert.That(attackCount, Is.EqualTo(2));
            Assert.That(lastAttackedWeapon, Is.SameAs(secondWeapon));
        }

        [Test]
        public void SetAttackInput_PressedWithoutWeapon_DoesNotRequestAttack()
        {
            weaponManager.SelectSlot(3);
            int attackCount = 0;
            weaponAttack.OnAttackRequested += weapon => attackCount++;

            weaponAttack.SetAttackInput(true);
            InvokeAttackCycle(Time.time + 1f);

            Assert.That(attackCount, Is.Zero);
        }

        private void InvokeAttackCycle(float currentTime)
        {
            // Reflection으로 비공개 주기 메서드에 고정 시간을 전달해 대기 없이 공격 간격을 검증합니다.
            MethodInfo updateAttackCycle = typeof(WeaponAttack).GetMethod(
                "UpdateAttackCycle",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(updateAttackCycle, Is.Not.Null);
            updateAttackCycle.Invoke(weaponAttack, new object[] { currentTime });
        }
    }
}
