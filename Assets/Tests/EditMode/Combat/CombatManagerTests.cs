using NUnit.Framework;
using Prohect_DOD_Undergird.DOD.Entity;
using Prohect_DOD_Undergird.DOD.Combat;

namespace Prohect_DOD_Undergird.DOD.Combat.Tests
{
    /// <summary>
    /// CombatManager의 체력 관리, 피해 및 회복 연산, 사망 판정, Entity System 연동을 검증하는 EditMode 단위 테스트입니다.
    /// </summary>
    [TestFixture]
    public class CombatManagerTests
    {
        private EntityManager entityManager;
        private CombatManager combatManager;

        [SetUp]
        public void SetUp()
        {
            entityManager = new EntityManager();
            combatManager = new CombatManager(entityManager);
        }

        /// <summary>
        /// 1. 정상 엔티티에 초기 체력을 등록하고 현재 및 최대 체력이 올바르게 설정되는지 검증합니다.
        /// </summary>
        [Test]
        public void Register_ValidEntity_SetsInitialHealthCorrectly()
        {
            EntityId id = entityManager.Create();

            bool registered = combatManager.Register(id, 100);

            Assert.IsTrue(registered);
            Assert.AreEqual(100, combatManager.GetCurrentHealth(id));
            Assert.AreEqual(100, combatManager.GetMaxHealth(id));
            Assert.IsFalse(combatManager.IsDead(id));
        }

        /// <summary>
        /// 2. 무효한 ID이거나 최대 체력이 0 이하인 경우 등록이 안전하게 거부되는지 검증합니다.
        /// </summary>
        [Test]
        public void Register_InvalidEntityOrNonPositiveMaxHealth_Rejected()
        {
            EntityId validId = entityManager.Create();
            EntityId invalidId = default;

            // 최대 체력이 0 이하인 경우
            Assert.IsFalse(combatManager.Register(validId, 0));
            Assert.IsFalse(combatManager.Register(validId, -50));

            // 무효한 ID인 경우
            Assert.IsFalse(combatManager.Register(invalidId, 100));

            // 등록되지 않은 ID의 체력 조회 시 0 반환
            Assert.AreEqual(0, combatManager.GetCurrentHealth(invalidId));
        }

        /// <summary>
        /// 3. 동일한 엔티티에 대해 중복 Register 호출 시 안전하게 거부되는지 검증합니다.
        /// </summary>
        [Test]
        public void Register_DuplicateEntity_Rejected()
        {
            EntityId id = entityManager.Create();

            bool firstRegister = combatManager.Register(id, 100);
            bool secondRegister = combatManager.Register(id, 150);

            Assert.IsTrue(firstRegister);
            Assert.IsFalse(secondRegister);
            Assert.AreEqual(100, combatManager.GetCurrentHealth(id)); // 기존 값 유지
        }

        /// <summary>
        /// 4. 유효한 피해가 적용되어 체력이 정상 감소하고 OnDamaged 이벤트가 발생하는지 검증합니다.
        /// </summary>
        [Test]
        public void TakeDamage_ValidDamage_ReducesCurrentHealth()
        {
            EntityId id = entityManager.Create();
            combatManager.Register(id, 100);

            int eventReceivedDamage = 0;
            combatManager.OnDamaged += (target, amount) =>
            {
                if (target == id) eventReceivedDamage = amount;
            };

            bool damageSuccess = combatManager.TakeDamage(id, new DamageData(30));

            Assert.IsTrue(damageSuccess);
            Assert.AreEqual(70, combatManager.GetCurrentHealth(id));
            Assert.AreEqual(30, eventReceivedDamage);
        }

        /// <summary>
        /// 5. 0 이하의 피해량이 무시되고 체력이 변경되지 않는지 검증합니다.
        /// </summary>
        [Test]
        public void TakeDamage_NonPositiveDamage_Ignored()
        {
            EntityId id = entityManager.Create();
            combatManager.Register(id, 100);

            bool zeroDamage = combatManager.TakeDamage(id, new DamageData(0));
            bool negativeDamage = combatManager.TakeDamage(id, new DamageData(-20));

            Assert.IsFalse(zeroDamage);
            Assert.IsFalse(negativeDamage);
            Assert.AreEqual(100, combatManager.GetCurrentHealth(id));
        }

        /// <summary>
        /// 6. 치명적인 피해(체력 이상) 적용 시 체력이 0이 되고, EntityManager.MarkDead 및 OnDied 이벤트가 단 1회 발생하는지 검증합니다.
        /// </summary>
        [Test]
        public void TakeDamage_FatalDamage_TriggersMarkDeadAndOnDiedOnce()
        {
            EntityId id = entityManager.Create();
            combatManager.Register(id, 50);

            int diedEventCount = 0;
            combatManager.OnDied += (target) =>
            {
                if (target == id) diedEventCount++;
            };

            bool fatalSuccess = combatManager.TakeDamage(id, new DamageData(80));

            Assert.IsTrue(fatalSuccess);
            Assert.AreEqual(0, combatManager.GetCurrentHealth(id));
            Assert.AreEqual(1, diedEventCount);
            Assert.AreEqual(EntityState.Dead, entityManager.GetState(id));
            Assert.IsTrue(combatManager.IsDead(id));
            Assert.AreEqual(50, combatManager.GetMaxHealth(id));
        }

        /// <summary>
        /// 7. 이미 사망한 엔티티에 대한 추가 피해 적용 시 안전하게 거부되는지 검증합니다.
        /// </summary>
        [Test]
        public void TakeDamage_AlreadyDeadEntity_Rejected()
        {
            EntityId id = entityManager.Create();
            combatManager.Register(id, 50);
            combatManager.TakeDamage(id, new DamageData(50)); // 사망 처리

            // 추가 피해 시도
            bool damageAfterDeath = combatManager.TakeDamage(id, new DamageData(10));

            Assert.IsFalse(damageAfterDeath);
            Assert.AreEqual(0, combatManager.GetCurrentHealth(id));
        }

        /// <summary>
        /// 8. 체력 회복이 정상 동작하며 최대 체력을 초과하지 않는지 검증합니다.
        /// </summary>
        [Test]
        public void Heal_ValidAmount_IncreasesHealthUpToMax()
        {
            EntityId id = entityManager.Create();
            combatManager.Register(id, 100);
            combatManager.TakeDamage(id, new DamageData(60)); // 현재 체력 40

            int healedAmount = 0;
            combatManager.OnHealed += (target, amount) =>
            {
                if (target == id) healedAmount = amount;
            };

            // 30 회복 -> 현재 체력 70
            bool healSuccess = combatManager.Heal(id, 30);
            Assert.IsTrue(healSuccess);
            Assert.AreEqual(70, combatManager.GetCurrentHealth(id));
            Assert.AreEqual(30, healedAmount);

            // 최대치를 넘어서는 50 회복 시도 -> 최대 100까지만 회복되고 실제 적용량은 30
            bool overHealSuccess = combatManager.Heal(id, 50);
            Assert.IsTrue(overHealSuccess);
            Assert.AreEqual(100, combatManager.GetCurrentHealth(id));
            Assert.AreEqual(30, healedAmount);

            // 이미 풀피인 상태에서는 회복 실패
            bool fullHealthHeal = combatManager.Heal(id, 10);
            Assert.IsFalse(fullHealthHeal);
        }

        /// <summary>
        /// 9. 0 이하의 회복량이거나 이미 사망한 엔티티에 대한 회복 시도가 거부되는지 검증합니다.
        /// </summary>
        [Test]
        public void Heal_NonPositiveAmountOrDeadEntity_Rejected()
        {
            EntityId id = entityManager.Create();
            combatManager.Register(id, 100);
            combatManager.TakeDamage(id, new DamageData(50)); // 체력 50

            // 0 이하 회복
            Assert.IsFalse(combatManager.Heal(id, 0));
            Assert.IsFalse(combatManager.Heal(id, -30));

            // 사망 처리 후 회복 시도
            combatManager.TakeDamage(id, new DamageData(50));
            Assert.IsFalse(combatManager.Heal(id, 50));
            Assert.AreEqual(0, combatManager.GetCurrentHealth(id));
        }

        /// <summary>
        /// 10. 매우 큰 회복량이 들어와도 int.MaxValue 정수 오버플로 없이 최대 체력으로 안전하게 제한되는지 검증합니다.
        /// </summary>
        [Test]
        public void Heal_LargeAmount_PreventsIntegerOverflow()
        {
            EntityId id = entityManager.Create();
            combatManager.Register(id, 100);
            combatManager.TakeDamage(id, new DamageData(50)); // 체력 50

            // int.MaxValue 수준의 극단적인 회복량
            bool healSuccess = combatManager.Heal(id, int.MaxValue);

            Assert.IsTrue(healSuccess);
            Assert.AreEqual(100, combatManager.GetCurrentHealth(id));
        }

        /// <summary>
        /// 11. 슬롯 재사용 후 이전 세대의 EntityId로 새 체력 데이터에 접근할 수 없음을 검증합니다.
        /// </summary>
        [Test]
        public void SlotReuse_OldGenerationId_CannotAccessNewHealthData()
        {
            EntityId oldId = entityManager.Create(); // Index 0, Gen 1
            combatManager.Register(oldId, 100);
            combatManager.TakeDamage(oldId, new DamageData(100)); // 사망
            entityManager.Release(oldId); // 슬롯 반환

            // 새 엔티티 생성 (동일 Index 0, Gen 2)
            EntityId newId = entityManager.Create();
            combatManager.Register(newId, 200);

            // 구 세대 ID(oldId)로 접근 시 모두 거부되어야 합니다.
            Assert.AreEqual(0, combatManager.GetCurrentHealth(oldId));
            Assert.AreEqual(0, combatManager.GetMaxHealth(oldId));
            Assert.IsFalse(combatManager.TakeDamage(oldId, new DamageData(10)));
            Assert.IsFalse(combatManager.Heal(oldId, 10));

            // 새 세대 ID(newId)는 정상 동작해야 합니다.
            Assert.AreEqual(200, combatManager.GetCurrentHealth(newId));
            Assert.IsTrue(combatManager.TakeDamage(newId, new DamageData(30)));
            Assert.AreEqual(170, combatManager.GetCurrentHealth(newId));
        }

        /// <summary>
        /// 12. EntityManager가 기본 용량(128)을 초과하여 확장될 때 CombatManager의 체력 배열도 동반 확장되어 정상 동작하는지 검증합니다.
        /// </summary>
        [Test]
        public void EntityManager_Growth_ExpandsHealthDataSafely()
        {
            const int count = 150; // 초기 128 초과
            EntityId lastId = default;
            for (int i = 0; i < count; i++)
            {
                EntityId id = entityManager.Create();
                bool registered = combatManager.Register(id, 100 + i);
                Assert.IsTrue(registered);
                if (i == 149)
                {
                    lastId = id;
                }
            }

            // 149번째 엔티티(확장 영역)의 체력 연산 검증
            Assert.AreEqual(249, combatManager.GetCurrentHealth(lastId));
            Assert.IsTrue(combatManager.TakeDamage(lastId, new DamageData(49)));
            Assert.AreEqual(200, combatManager.GetCurrentHealth(lastId));
        }

        /// <summary>
        /// 13. IsDead 메서드가 정상 상태 및 사망 상태를 정확하게 판별하는지 검증합니다.
        /// </summary>
        [Test]
        public void IsDead_ReturnsCorrectStatus()
        {
            EntityId id = entityManager.Create();
            combatManager.Register(id, 100);

            Assert.IsFalse(combatManager.IsDead(id));

            combatManager.TakeDamage(id, new DamageData(100));

            Assert.IsTrue(combatManager.IsDead(id));
        }
    }
}
