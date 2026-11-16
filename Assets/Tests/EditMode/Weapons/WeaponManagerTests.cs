using NUnit.Framework;
using UnityEngine;

namespace Prohect_DOD_Undergird.Player.Tests
{
    public class WeaponManagerTests
    {
        private GameObject playerObject;
        private WeaponManager weaponManager;
        private WeaponData firstWeapon;
        private WeaponData secondWeapon;
        private WeaponData thirdWeapon;

        [SetUp]
        public void SetUp()
        {
            playerObject = new GameObject("WeaponManagerTestPlayer");
            weaponManager = playerObject.AddComponent<WeaponManager>();
            firstWeapon = ScriptableObject.CreateInstance<WeaponData>();
            secondWeapon = ScriptableObject.CreateInstance<WeaponData>();
            thirdWeapon = ScriptableObject.CreateInstance<WeaponData>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(firstWeapon);
            Object.DestroyImmediate(secondWeapon);
            Object.DestroyImmediate(thirdWeapon);
        }

        [Test]
        public void SelectSlot_EmptySlot_ChangesToUnarmed()
        {
            weaponManager.SetSlotWeapon(1, firstWeapon);
            weaponManager.SelectSlot(1);
            int changeCount = 0;
            weaponManager.OnWeaponChanged += (previous, current) => changeCount++;

            bool selected = weaponManager.SelectSlot(2);

            Assert.That(selected, Is.True);
            Assert.That(weaponManager.CurrentWeapon, Is.Null);
            Assert.That(weaponManager.SelectedSlotNumber, Is.EqualTo(2));
            Assert.That(changeCount, Is.EqualTo(1));
        }

        [Test]
        public void SelectSlot_SameWeapon_DoesNotRaiseChangeEvent()
        {
            weaponManager.SetSlotWeapon(1, firstWeapon);
            weaponManager.SetSlotWeapon(2, firstWeapon);
            weaponManager.SelectSlot(1);
            int changeCount = 0;
            weaponManager.OnWeaponChanged += (previous, current) => changeCount++;

            weaponManager.SelectSlot(2);

            Assert.That(weaponManager.SelectedSlotNumber, Is.EqualTo(2));
            Assert.That(weaponManager.CurrentWeapon, Is.SameAs(firstWeapon));
            Assert.That(changeCount, Is.Zero);
        }

        [Test]
        public void SetSlotWeapon_InactiveSlot_KeepsCurrentWeapon()
        {
            weaponManager.SetSlotWeapon(1, firstWeapon);
            weaponManager.SetSlotWeapon(2, secondWeapon);
            weaponManager.SelectSlot(1);
            int changeCount = 0;
            weaponManager.OnWeaponChanged += (previous, current) => changeCount++;

            weaponManager.SetSlotWeapon(2, thirdWeapon);

            Assert.That(weaponManager.CurrentWeapon, Is.SameAs(firstWeapon));
            Assert.That(weaponManager.SelectedSlotNumber, Is.EqualTo(1));
            Assert.That(changeCount, Is.Zero);
        }

        [Test]
        public void SelectSlot_SameSlot_DoesNotRaiseChangeEvent()
        {
            weaponManager.SetSlotWeapon(1, firstWeapon);
            weaponManager.SelectSlot(1);
            int changeCount = 0;
            weaponManager.OnWeaponChanged += (previous, current) => changeCount++;

            weaponManager.SelectSlot(1);

            Assert.That(weaponManager.CurrentWeapon, Is.SameAs(firstWeapon));
            Assert.That(changeCount, Is.Zero);
        }

        [Test]
        public void SelectSlot_InvalidNumber_IsRejected()
        {
            Assert.That(weaponManager.SelectSlot(0), Is.False);
            Assert.That(weaponManager.SelectSlot(11), Is.False);
            Assert.That(weaponManager.SetSlotWeapon(0, firstWeapon), Is.False);
            Assert.That(weaponManager.CurrentWeapon, Is.Null);
        }

        [Test]
        public void SelectNextWeapon_SkipsEmptySlotsAndWraps()
        {
            weaponManager.SetSlotWeapon(2, firstWeapon);
            weaponManager.SetSlotWeapon(5, secondWeapon);

            Assert.That(weaponManager.SelectNextWeapon(), Is.True);
            Assert.That(weaponManager.SelectedSlotNumber, Is.EqualTo(2));
            Assert.That(weaponManager.CurrentWeapon, Is.SameAs(firstWeapon));

            Assert.That(weaponManager.SelectNextWeapon(), Is.True);
            Assert.That(weaponManager.SelectedSlotNumber, Is.EqualTo(5));
            Assert.That(weaponManager.CurrentWeapon, Is.SameAs(secondWeapon));

            Assert.That(weaponManager.SelectNextWeapon(), Is.True);
            Assert.That(weaponManager.SelectedSlotNumber, Is.EqualTo(2));
        }

        [Test]
        public void SelectPreviousWeapon_SkipsEmptySlotsAndWraps()
        {
            weaponManager.SetSlotWeapon(2, firstWeapon);
            weaponManager.SetSlotWeapon(5, secondWeapon);
            weaponManager.SelectSlot(2);

            Assert.That(weaponManager.SelectPreviousWeapon(), Is.True);
            Assert.That(weaponManager.SelectedSlotNumber, Is.EqualTo(5));
            Assert.That(weaponManager.CurrentWeapon, Is.SameAs(secondWeapon));

            Assert.That(weaponManager.SelectPreviousWeapon(), Is.True);
            Assert.That(weaponManager.SelectedSlotNumber, Is.EqualTo(2));
        }

        [Test]
        public void SelectWheel_WhenNoWeaponsAreRegistered_DoesNotChangeState()
        {
            int changeCount = 0;
            weaponManager.OnWeaponChanged += (previous, current) => changeCount++;

            Assert.That(weaponManager.SelectNextWeapon(), Is.False);
            Assert.That(weaponManager.SelectPreviousWeapon(), Is.False);
            Assert.That(weaponManager.CurrentWeapon, Is.Null);
            Assert.That(changeCount, Is.Zero);
        }

        [Test]
        public void SetSlotWeapon_ActiveSlotChange_RaisesEventOnlyForActualChange()
        {
            weaponManager.SelectSlot(1);
            int changeCount = 0;
            weaponManager.OnWeaponChanged += (previous, current) => changeCount++;

            weaponManager.SetSlotWeapon(1, firstWeapon);
            weaponManager.SetSlotWeapon(1, firstWeapon);
            weaponManager.SetSlotWeapon(1, thirdWeapon);
            weaponManager.SetSlotWeapon(1, null);

            Assert.That(changeCount, Is.EqualTo(3));
            Assert.That(weaponManager.CurrentWeapon, Is.Null);
        }
    }
}
