using System;
using System.Collections.Generic;

namespace Prohect_DOD_Undergird.DOD.Entity
{
    /// <summary>
    /// 엔티티의 생명주기와 슬롯 할당을 관리하는 매니저 클래스입니다.
    /// UnityEngine에 의존하지 않으며 데이터 지향 설계(DOD) 원칙에 따라 별도의 배열과 FreeList 스택으로 상태를 관리합니다.
    /// </summary>
    public class EntityManager
    {
        private const int DefaultInitialCapacity = 128;

        private int[] generations;
        private EntityState[] states;
        private readonly Stack<int> freeList;
        private int capacity;

        /// <summary>
        /// 현재 할당된 총 엔티티 슬롯 용량을 반환합니다.
        /// </summary>
        public int Capacity => capacity;

        /// <summary>
        /// EntityManager의 기본 생성자입니다.
        /// 기본 초기 용량(128)으로 슬롯 배열 및 FreeList를 초기화합니다.
        /// </summary>
        public EntityManager() : this(DefaultInitialCapacity)
        {
        }

        /// <summary>
        /// 지정한 초기 용량으로 EntityManager를 생성합니다.
        /// </summary>
        /// <param name="initialCapacity">초기 슬롯 용량 (1 이상이어야 함)</param>
        public EntityManager(int initialCapacity)
        {
            if (initialCapacity <= 0)
            {
                initialCapacity = DefaultInitialCapacity;
            }

            capacity = initialCapacity;
            generations = new int[capacity];
            states = new EntityState[capacity];
            freeList = new Stack<int>(capacity);

            // 초기 슬롯 설정: 세대는 1부터 시작하며 상태는 Released로 초기화합니다.
            // 0번 인덱스부터 순차적으로 꺼낼 수 있도록 역순으로 스택에 푸시합니다.
            for (int i = capacity - 1; i >= 0; i--)
            {
                generations[i] = 1;
                states[i] = EntityState.Released;
                freeList.Push(i);
            }
        }

        /// <summary>
        /// 새 엔티티를 생성하고 고유한 EntityId를 반환합니다.
        /// 빈 슬롯이 없으면 배열을 2배로 확장합니다.
        /// Generation은 Create()에서 변경하지 않으며 기존 슬롯의 세대를 유지합니다.
        /// </summary>
        /// <returns>생성된 유효한 EntityId</returns>
        public EntityId Create()
        {
            if (freeList.Count == 0)
            {
                Grow();
            }

            int index = freeList.Pop();
            states[index] = EntityState.Alive;

            return new EntityId(index, generations[index]);
        }

        /// <summary>
        /// 지정한 엔티티가 현재 유효하며 활성(Alive) 상태인지 확인합니다.
        /// 인덱스 유효 범위, Generation 일치 여부, Alive 상태 여부를 모두 검증합니다.
        /// </summary>
        /// <param name="id">검사할 EntityId</param>
        /// <returns>모든 조건이 만족되면 true, 아니면 false</returns>
        public bool IsAlive(EntityId id)
        {
            if (id.Index < 0 || id.Index >= capacity)
            {
                return false;
            }

            if (id.Generation <= 0 || id.Generation != generations[id.Index])
            {
                return false;
            }

            return states[id.Index] == EntityState.Alive;
        }

        /// <summary>
        /// 지정한 엔티티의 현재 생명주기 상태를 반환합니다.
        /// 유효하지 않은 ID가 전달되면 Released를 반환합니다.
        /// </summary>
        /// <param name="id">상태를 조회할 EntityId</param>
        /// <returns>엔티티의 EntityState (유효하지 않은 경우 Released)</returns>
        public EntityState GetState(EntityId id)
        {
            if (id.Index < 0 || id.Index >= capacity)
            {
                return EntityState.Released;
            }

            if (id.Generation <= 0 || id.Generation != generations[id.Index])
            {
                return EntityState.Released;
            }

            return states[id.Index];
        }

        /// <summary>
        /// 활성(Alive) 상태의 엔티티를 사망(Dead) 상태로 전환합니다.
        /// 유효하지 않은 ID이거나 Alive 상태가 아닌 경우 예외 없이 false를 반환합니다.
        /// </summary>
        /// <param name="id">사망 처리할 EntityId</param>
        /// <returns>성공적으로 Dead 상태로 전환되면 true, 실패 시 false</returns>
        public bool MarkDead(EntityId id)
        {
            if (id.Index < 0 || id.Index >= capacity)
            {
                return false;
            }

            if (id.Generation <= 0 || id.Generation != generations[id.Index])
            {
                return false;
            }

            if (states[id.Index] != EntityState.Alive)
            {
                return false;
            }

            states[id.Index] = EntityState.Dead;
            return true;
        }

        /// <summary>
        /// 사망(Dead) 상태의 엔티티 슬롯을 해제하고 FreeList에 반환합니다.
        /// Generation 증가는 이 메서드 안에서만 수행됩니다.
        /// Dead 상태가 아니거나 잘못된 ID인 경우 예외 없이 false를 반환합니다.
        /// </summary>
        /// <param name="id">해제할 EntityId</param>
        /// <returns>해제 성공 시 true, 실패 시 false</returns>
        public bool Release(EntityId id)
        {
            if (id.Index < 0 || id.Index >= capacity)
            {
                return false;
            }

            if (id.Generation <= 0 || id.Generation != generations[id.Index])
            {
                return false;
            }

            // Release는 Dead 상태에서만 성공합니다.
            if (states[id.Index] != EntityState.Dead)
            {
                return false;
            }

            states[id.Index] = EntityState.Released;
            generations[id.Index]++;
            freeList.Push(id.Index);

            return true;
        }

        /// <summary>
        /// 슬롯 배열이 부족할 때 용량을 2배로 확장하고 새 슬롯들을 초기화하여 FreeList에 등록합니다.
        /// </summary>
        private void Grow()
        {
            int oldCapacity = capacity;
            int newCapacity = oldCapacity * 2;

            Array.Resize(ref generations, newCapacity);
            Array.Resize(ref states, newCapacity);
            capacity = newCapacity;

            // 새로 추가된 슬롯 초기화: 세대는 1, 상태는 Released로 설정합니다.
            // 낮은 인덱스부터 할당되도록 역순으로 스택에 푸시합니다.
            for (int i = newCapacity - 1; i >= oldCapacity; i--)
            {
                generations[i] = 1;
                states[i] = EntityState.Released;
                freeList.Push(i);
            }
        }
    }
}
