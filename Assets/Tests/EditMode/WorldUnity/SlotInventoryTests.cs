using NUnit.Framework;
using Prohect_DOD_Undergird.DOD.World;

namespace Prohect_DOD_Undergird.World.Tests
{
    /// <summary>
    /// 고정 슬롯 배열의 중첩, 용량, 제거 및 자원 수량 조회를 검증합니다.
    /// </summary>
    public sealed class SlotInventoryTests
    {
        private static readonly ItemId Copper = new ItemId(1001);
        private static readonly ItemId Iron = new ItemId(1002);

        [Test]
        public void TryAdd_FillsExistingStackBeforeUsingNextSlot()
        {
            SlotInventory inventory = CreateInventory(3, 5);

            Assert.IsTrue(inventory.TryAdd(Copper, 4));
            Assert.IsTrue(inventory.TryAdd(Copper, 3));

            AssertSlot(inventory, 0, Copper, 5);
            AssertSlot(inventory, 1, Copper, 2);
            AssertSlot(inventory, 2, default, 0);
        }

        [Test]
        public void TryAdd_UsesFollowingSlotWhenMaximumStackIsExceeded()
        {
            SlotInventory inventory = CreateInventory(2, 999);

            Assert.IsTrue(inventory.TryAdd(Copper, 1000));

            AssertSlot(inventory, 0, Copper, 999);
            AssertSlot(inventory, 1, Copper, 1);
        }

        [Test]
        public void TryAdd_RejectsCapacityOverflowWithoutPartialChange()
        {
            SlotInventory inventory = CreateInventory(1, 5);
            Assert.IsTrue(inventory.TryAdd(Copper, 4));

            Assert.IsFalse(inventory.TryAdd(Copper, 2));

            AssertSlot(inventory, 0, Copper, 4);
            Assert.AreEqual(4, inventory.GetTotalCount(Copper));
        }

        [Test]
        public void TryRemove_ClearsEmptySlotAndAllowsItToBeReused()
        {
            SlotInventory inventory = CreateInventory(1, 5);
            Assert.IsTrue(inventory.TryAdd(Copper, 5));

            Assert.IsTrue(inventory.TryRemove(Copper, 5));
            AssertSlot(inventory, 0, default, 0);
            Assert.IsTrue(inventory.TryAdd(Iron, 2));
            AssertSlot(inventory, 0, Iron, 2);
        }

        [Test]
        public void GetTotalCount_SumsStacksAndKeepsItemIdsIndependent()
        {
            SlotInventory inventory = CreateInventory(3, 5);
            Assert.IsTrue(inventory.TryAdd(Copper, 8));
            Assert.IsTrue(inventory.TryAdd(Iron, 3));

            Assert.AreEqual(8, inventory.GetTotalCount(Copper));
            Assert.AreEqual(3, inventory.GetTotalCount(Iron));
            Assert.AreEqual(0, inventory.GetTotalCount(new ItemId(9000)));
        }

        [Test]
        public void ReservedReward_BlocksCompetingChangesUntilCommittedOrCancelled()
        {
            SlotInventory inventory = CreateInventory(1, 2);

            Assert.IsTrue(inventory.TryReserveAdd(Copper, 2));
            Assert.IsFalse(inventory.TryAdd(Copper, 1));
            Assert.IsFalse(inventory.TryRemove(Copper, 1));
            Assert.IsTrue(inventory.CommitReservedAdd(Copper, 2));
            Assert.AreEqual(2, inventory.GetTotalCount(Copper));
            Assert.IsTrue(inventory.TryRemove(Copper, 2));

            Assert.IsTrue(inventory.TryReserveAdd(Iron, 1));
            Assert.IsTrue(inventory.CancelReservedAdd(Iron, 1));
            Assert.IsTrue(inventory.TryAdd(Copper, 1));
        }

        private static SlotInventory CreateInventory(int capacity, int maxStack)
        {
            return new SlotInventory(capacity, new[]
            {
                new InventoryItemDefinition(Copper, maxStack),
                new InventoryItemDefinition(Iron, maxStack)
            });
        }

        private static void AssertSlot(SlotInventory inventory, int index, ItemId itemId, int amount)
        {
            Assert.IsTrue(inventory.TryGetSlot(index, out InventorySlot slot));
            Assert.AreEqual(itemId, slot.ItemId);
            Assert.AreEqual(amount, slot.Amount);
        }
    }
}
