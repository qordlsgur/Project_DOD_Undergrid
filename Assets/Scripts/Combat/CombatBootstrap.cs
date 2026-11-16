using UnityEngine;
using Prohect_DOD_Undergird.DOD.Entity;
using Prohect_DOD_Undergird.DOD.Combat;

namespace Prohect_DOD_Undergird.Combat
{
    /// <summary>
    /// 씬에서 공유할 EntityManager와 CombatManager를 생성하고 보관합니다.
    /// </summary>
    public class CombatBootstrap : MonoBehaviour
    {
        /// <summary>
        /// 게임 엔티티의 식별자와 생명주기를 관리하는 공유 매니저입니다.
        /// </summary>
        public EntityManager EntityManager { get; private set; }

        /// <summary>
        /// 엔티티의 체력과 전투 상태를 관리하는 공유 매니저입니다.
        /// </summary>
        public CombatManager CombatManager { get; private set; }

        private void Awake()
        {
            EntityManager = new EntityManager();
            CombatManager = new CombatManager(EntityManager);
        }
    }
}
