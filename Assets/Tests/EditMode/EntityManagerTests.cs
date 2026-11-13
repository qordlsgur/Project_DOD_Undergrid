using System.Collections.Generic;
using NUnit.Framework;
using Prohect_DOD_Undergird.DOD.Entity;

namespace Prohect_DOD_Undergird.DOD.Entity.Tests
{
    /// <summary>
    /// EntityManager 및 EntityId의 핵심 생명주기 및 DOD 규칙을 검증하는 EditMode 단위 테스트입니다.
    /// </summary>
    [TestFixture]
    public class EntityManagerTests
    {
        private EntityManager manager;

        [SetUp]
        public void SetUp()
        {
            manager = new EntityManager();
        }

        /// <summary>
        /// 1. 최초 생성 시 Generation이 1인지 검증합니다.
        /// </summary>
        [Test]
        public void Create_InitialEntity_HasGenerationOne()
        {
            EntityId id = manager.Create();

            Assert.AreEqual(1, id.Generation);
            Assert.IsTrue(manager.IsAlive(id));
            Assert.AreEqual(EntityState.Alive, manager.GetState(id));
        }

        /// <summary>
        /// 2. 여러 엔티티를 독립적으로 생성할 수 있는지 검증합니다.
        /// </summary>
        [Test]
        public void Create_MultipleEntities_AllAreAliveAndUnique()
        {
            const int count = 10;
            List<EntityId> ids = new List<EntityId>(count);

            for (int i = 0; i < count; i++)
            {
                EntityId id = manager.Create();
                ids.Add(id);
            }

            Assert.AreEqual(count, ids.Count);

            // 모든 ID가 활성 상태이며 인덱스가 서로 고유해야 합니다.
            HashSet<int> indices = new HashSet<int>();
            foreach (EntityId id in ids)
            {
                Assert.IsTrue(manager.IsAlive(id));
                Assert.AreEqual(EntityState.Alive, manager.GetState(id));
                Assert.IsTrue(indices.Add(id.Index));
            }
        }

        /// <summary>
        /// 3. 유효하지 않은 ID(기본값, 음수 인덱스, 범위 초과 인덱스, 잘못된 세대)를 안전하게 거부하는지 검증합니다.
        /// </summary>
        [Test]
        public void InvalidId_RejectedSafelyWithoutExceptions()
        {
            // default(EntityId)는 Generation이 0이므로 무효입니다.
            EntityId defaultId = default;
            Assert.IsFalse(manager.IsAlive(defaultId));
            Assert.AreEqual(EntityState.Released, manager.GetState(defaultId));
            Assert.IsFalse(manager.MarkDead(defaultId));
            Assert.IsFalse(manager.Release(defaultId));

            // 음수 인덱스
            EntityId negativeIndexId = new EntityId(-1, 1);
            Assert.IsFalse(manager.IsAlive(negativeIndexId));
            Assert.AreEqual(EntityState.Released, manager.GetState(negativeIndexId));
            Assert.IsFalse(manager.MarkDead(negativeIndexId));
            Assert.IsFalse(manager.Release(negativeIndexId));

            // 용량 범위 초과 인덱스
            EntityId outOfBoundsId = new EntityId(manager.Capacity + 100, 1);
            Assert.IsFalse(manager.IsAlive(outOfBoundsId));
            Assert.AreEqual(EntityState.Released, manager.GetState(outOfBoundsId));
            Assert.IsFalse(manager.MarkDead(outOfBoundsId));
            Assert.IsFalse(manager.Release(outOfBoundsId));

            // 세대 번호 불일치 ID
            EntityId validId = manager.Create();
            EntityId wrongGenerationId = new EntityId(validId.Index, validId.Generation + 99);
            Assert.IsFalse(manager.IsAlive(wrongGenerationId));
            Assert.AreEqual(EntityState.Released, manager.GetState(wrongGenerationId));
            Assert.IsFalse(manager.MarkDead(wrongGenerationId));
            Assert.IsFalse(manager.Release(wrongGenerationId));
        }

        /// <summary>
        /// 4. Alive 상태에서 Dead 상태로 정상 전환되는지 검증합니다.
        /// </summary>
        [Test]
        public void MarkDead_FromAlive_TransitionsToDeadState()
        {
            EntityId id = manager.Create();

            bool markResult = manager.MarkDead(id);

            Assert.IsTrue(markResult);
            Assert.IsFalse(manager.IsAlive(id));
            Assert.AreEqual(EntityState.Dead, manager.GetState(id));
        }

        /// <summary>
        /// 5. Alive 상태에서 Release 시도가 거부되는지 검증합니다.
        /// </summary>
        [Test]
        public void Release_FromAliveState_ReturnsFalseAndPreservesAlive()
        {
            EntityId id = manager.Create();

            bool releaseResult = manager.Release(id);

            Assert.IsFalse(releaseResult);
            Assert.IsTrue(manager.IsAlive(id));
            Assert.AreEqual(EntityState.Alive, manager.GetState(id));
        }

        /// <summary>
        /// 6. Dead 상태에서 Release가 성공적으로 완료되는지 검증합니다.
        /// </summary>
        [Test]
        public void Release_FromDeadState_SucceedsAndFreesSlot()
        {
            EntityId id = manager.Create();
            manager.MarkDead(id);

            bool releaseResult = manager.Release(id);

            Assert.IsTrue(releaseResult);
            Assert.IsFalse(manager.IsAlive(id));
            // Release 후에는 해당 ID의 세대가 구세대가 되므로 GetState 호출 시 Released가 반환됩니다.
            Assert.AreEqual(EntityState.Released, manager.GetState(id));
        }

        /// <summary>
        /// 7. Release된 슬롯이 다음 Create 호출 시 재사용되는지 검증합니다.
        /// </summary>
        [Test]
        public void Release_FreesSlotForReuse()
        {
            EntityId firstId = manager.Create();
            manager.MarkDead(firstId);
            manager.Release(firstId);

            EntityId reusedId = manager.Create();

            Assert.AreEqual(firstId.Index, reusedId.Index);
            Assert.IsTrue(manager.IsAlive(reusedId));
        }

        /// <summary>
        /// 8. 슬롯 재사용 시 Generation이 1 증가하는지 검증합니다.
        /// </summary>
        [Test]
        public void ReuseSlot_IncreasesGenerationByOne()
        {
            EntityId firstId = manager.Create();
            int originalGeneration = firstId.Generation;

            manager.MarkDead(firstId);
            manager.Release(firstId);

            EntityId reusedId = manager.Create();

            Assert.AreEqual(firstId.Index, reusedId.Index);
            Assert.AreEqual(originalGeneration + 1, reusedId.Generation);
        }

        /// <summary>
        /// 9. 슬롯 재사용 후 이전 Generation의 ID가 더 이상 유효하지 않은지 검증합니다.
        /// </summary>
        [Test]
        public void PreviousGenerationId_IsInvalidAfterSlotReused()
        {
            EntityId oldId = manager.Create();
            manager.MarkDead(oldId);
            manager.Release(oldId);

            EntityId newId = manager.Create();

            // 이전 ID는 세대 불일치로 인해 완전히 무효화되어야 합니다.
            Assert.IsFalse(manager.IsAlive(oldId));
            Assert.IsFalse(manager.MarkDead(oldId));
            Assert.IsFalse(manager.Release(oldId));
            Assert.AreEqual(EntityState.Released, manager.GetState(oldId));

            // 새 세대 ID는 정상 동작해야 합니다.
            Assert.IsTrue(manager.IsAlive(newId));
        }

        /// <summary>
        /// 10. 초기 용량(128)을 초과할 때 배열이 2배로 확장되며 정상 동작하는지 검증합니다.
        /// </summary>
        [Test]
        public void Create_ExceedingInitialCapacity_GrowsArrayCapacityCorrectly()
        {
            int initialCapacity = manager.Capacity;
            Assert.AreEqual(128, initialCapacity);

            // 128개 엔티티 생성 (초기 용량 전체 소진)
            List<EntityId> ids = new List<EntityId>(initialCapacity + 10);
            for (int i = 0; i < initialCapacity; i++)
            {
                ids.Add(manager.Create());
            }

            Assert.AreEqual(initialCapacity, manager.Capacity);

            // 129번째 엔티티 생성 시 배열이 2배(256)로 확장되어야 합니다.
            EntityId extraId = manager.Create();
            ids.Add(extraId);

            Assert.AreEqual(initialCapacity * 2, manager.Capacity);
            Assert.IsTrue(manager.IsAlive(extraId));
            Assert.AreEqual(1, extraId.Generation);

            // 기존 엔티티와 새 엔티티가 모두 유효한지 확인합니다.
            foreach (EntityId id in ids)
            {
                Assert.IsTrue(manager.IsAlive(id));
            }
        }

        /// <summary>
        /// 11. 이미 Release된 엔티티에 대한 중복 Release 호출이 안전하게 거부되는지 검증합니다.
        /// </summary>
        [Test]
        public void Release_DuplicateCall_ReturnsFalse()
        {
            EntityId id = manager.Create();
            manager.MarkDead(id);

            bool firstRelease = manager.Release(id);
            bool secondRelease = manager.Release(id);

            Assert.IsTrue(firstRelease);
            Assert.IsFalse(secondRelease);
        }

        /// <summary>
        /// 12. 잘못된 상태 전이(Released 상태에서 MarkDead, Dead 상태에서 재 MarkDead 등)가 거부되는지 검증합니다.
        /// </summary>
        [Test]
        public void InvalidStateTransitions_AreRejected()
        {
            EntityId id = manager.Create();

            // 1. Alive -> Dead 정상 전이
            Assert.IsTrue(manager.MarkDead(id));

            // 2. Dead -> Dead 중복 전이 시도 (거부되어야 함)
            Assert.IsFalse(manager.MarkDead(id));

            // 3. Dead -> Release 정상 전이
            Assert.IsTrue(manager.Release(id));

            // 4. Released -> MarkDead 전이 시도 (거부되어야 함)
            Assert.IsFalse(manager.MarkDead(id));

            // 5. Released -> Release 중복 전이 시도 (거부되어야 함)
            Assert.IsFalse(manager.Release(id));
        }

        /// <summary>
        /// 13. FreeList가 Stack(LIFO) 방식으로 슬롯을 반환하고 재할당하는지 검증합니다.
        /// </summary>
        [Test]
        public void FreeList_OperatesInLifoOrder()
        {
            EntityId idA = manager.Create(); // Index 0
            EntityId idB = manager.Create(); // Index 1
            EntityId idC = manager.Create(); // Index 2

            // idB(Index 1)를 먼저 해제하고, 이어서 idA(Index 0)를 해제합니다.
            manager.MarkDead(idB);
            manager.Release(idB);

            manager.MarkDead(idA);
            manager.Release(idA);

            // Stack(LIFO) 구조이므로 나중에 해제된 idA(Index 0)가 먼저 재할당되어야 합니다.
            EntityId reusedFirst = manager.Create();
            Assert.AreEqual(idA.Index, reusedFirst.Index);

            // 그 다음으로 idB(Index 1)가 재할당되어야 합니다.
            EntityId reusedSecond = manager.Create();
            Assert.AreEqual(idB.Index, reusedSecond.Index);

            // 기존 idC(Index 2)는 영향을 받지 않고 계속 Alive여야 합니다.
            Assert.IsTrue(manager.IsAlive(idC));
        }

        /// <summary>
        /// 14. 배열 확장(Grow) 이후에도 기존 엔티티 및 신규 슬롯 엔티티의 생명주기가 정상 작동하는지 종합 검증합니다.
        /// </summary>
        [Test]
        public void ArrayGrowth_PreservesExistingEntitiesAndAllowsLifecycleOnExpandedSlots()
        {
            // 초기 용량(128)을 초과하여 150개 엔티티를 생성합니다.
            const int totalCount = 150;
            List<EntityId> ids = new List<EntityId>(totalCount);
            for (int i = 0; i < totalCount; i++)
            {
                ids.Add(manager.Create());
            }

            Assert.AreEqual(256, manager.Capacity);

            // 확장 전 생성된 엔티티(Index 0) 생명주기 검증
            EntityId oldEntity = ids[0];
            Assert.IsTrue(manager.MarkDead(oldEntity));
            Assert.IsTrue(manager.Release(oldEntity));

            // 확장 후 새로 할당된 영역의 엔티티(Index 140) 생명주기 검증
            EntityId newEntity = ids[140];
            Assert.IsTrue(manager.MarkDead(newEntity));
            Assert.IsTrue(manager.Release(newEntity));

            // 신규 엔티티 생성 시 최근에 해제된 newEntity의 슬롯(Index 140)이 먼저 재사용되는지 확인
            EntityId reusedEntity = manager.Create();
            Assert.AreEqual(newEntity.Index, reusedEntity.Index);
            Assert.AreEqual(2, reusedEntity.Generation);
            Assert.IsTrue(manager.IsAlive(reusedEntity));
        }

        /// <summary>
        /// 15. EntityId의 동등성 비교 및 연산자 오버로드가 정상 작동하는지 검증합니다.
        /// </summary>
        [Test]
        public void EntityId_EqualityAndOperatorOverloads_BehaveCorrectly()
        {
            EntityId id1 = new EntityId(5, 2);
            EntityId id2 = new EntityId(5, 2);
            EntityId differentIndex = new EntityId(6, 2);
            EntityId differentGen = new EntityId(5, 3);

            Assert.IsTrue(id1.Equals(id2));
            Assert.IsTrue(id1.Equals((object)id2));
            Assert.IsTrue(id1 == id2);
            Assert.IsFalse(id1 != id2);
            Assert.AreEqual(id1.GetHashCode(), id2.GetHashCode());

            Assert.IsFalse(id1 == differentIndex);
            Assert.IsTrue(id1 != differentIndex);

            Assert.IsFalse(id1 == differentGen);
            Assert.IsTrue(id1 != differentGen);

            // default(EntityId)와 EntityId.Invalid의 동등성 확인
            Assert.IsTrue(default(EntityId) == EntityId.Invalid);
            Assert.AreEqual(0, EntityId.Invalid.Generation);
        }
    }
}
