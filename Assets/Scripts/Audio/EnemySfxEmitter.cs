using GameName.Enemy;
using UnityEngine;

namespace GameName.Audio
{
    /// <summary>
    /// 各敵prefabに付けて、敵のイベントをEnemySfxManagerへ橋渡しするコンポーネント。
    /// 「近くにいる」は、現在のメインカメラ画面内に見えているかで判定する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySfxEmitter : MonoBehaviour
    {
        /// <summary>
        /// 死亡SEの種類を切り替えるための敵種別。
        /// 通常敵だけが咆哮候補になる。
        /// </summary>
        private enum EnemySfxKind
        {
            NormalEnemy,
            StageBoss,
            LastBoss
        }

        [SerializeField] private EnemySfxKind enemyKind = EnemySfxKind.NormalEnemy;
        [SerializeField] private bool participatesInGrowls = true;
        [SerializeField] private EnemySfxProfile profile;
        [SerializeField] private bool autoCollectRenderers = true;
        [SerializeField] private Renderer[] visibilityRenderers;

        private EnemyController enemyController;
        private EnemyRangedAttack rangedAttack;
        private EnemyTackleAttack tackleAttack;
        private StageBossAttack stageBossAttack;
        private LastBossController lastBossController;

        private void Awake()
        {
            CacheComponents();
            RefreshVisibilityRenderersIfNeeded();
        }

        private void OnEnable()
        {
            CacheComponents();
            SubscribeEvents();

            if (participatesInGrowls)
            {
                // 通常敵だけを咆哮候補として登録する。実際に鳴るかは画面内判定で決まる。
                EnemySfxManager.GetOrCreate(profile).RegisterGrowlCandidate(this, profile);
            }
        }

        private void OnDisable()
        {
            UnsubscribeEvents();

            if (participatesInGrowls && EnemySfxManager.Instance != null)
            {
                EnemySfxManager.Instance.UnregisterGrowlCandidate(this);
            }
        }

        public bool IsVisibleToMainCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return false;
            }

            RefreshVisibilityRenderersIfNeeded();

            bool checkedRenderer = false;
            if (visibilityRenderers != null && visibilityRenderers.Length > 0)
            {
                // RendererのBoundsがカメラ視錐台に少しでも入っていれば「画面内」とみなす。
                Plane[] cameraPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
                for (int i = 0; i < visibilityRenderers.Length; i++)
                {
                    Renderer targetRenderer = visibilityRenderers[i];
                    if (targetRenderer == null || !targetRenderer.enabled || !targetRenderer.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    checkedRenderer = true;
                    if (GeometryUtility.TestPlanesAABB(cameraPlanes, targetRenderer.bounds))
                    {
                        return true;
                    }
                }
            }

            if (checkedRenderer)
            {
                return false;
            }

            // Rendererがない敵でも最低限動くよう、transform位置のViewport判定にフォールバックする。
            Vector3 viewportPosition = mainCamera.WorldToViewportPoint(transform.position);
            return viewportPosition.z > 0f &&
                   viewportPosition.x >= 0f && viewportPosition.x <= 1f &&
                   viewportPosition.y >= 0f && viewportPosition.y <= 1f;
        }

        private void CacheComponents()
        {
            if (enemyController == null)
            {
                enemyController = GetComponent<EnemyController>();
            }

            if (rangedAttack == null)
            {
                rangedAttack = GetComponent<EnemyRangedAttack>();
            }

            if (tackleAttack == null)
            {
                tackleAttack = GetComponent<EnemyTackleAttack>();
            }

            if (stageBossAttack == null)
            {
                stageBossAttack = GetComponent<StageBossAttack>();
            }

            if (lastBossController == null)
            {
                lastBossController = GetComponent<LastBossController>();
            }
        }

        private void SubscribeEvents()
        {
            // 付いている敵スクリプトだけ購読するので、同じEmitterを通常敵・ボスで使い回せる。
            if (enemyController != null)
            {
                enemyController.Died += HandleEnemyDied;
            }

            if (rangedAttack != null)
            {
                rangedAttack.ProjectileFired += HandleProjectileFired;
            }

            if (tackleAttack != null)
            {
                tackleAttack.ChargeStarted += HandleChargeStarted;
            }

            if (stageBossAttack != null)
            {
                stageBossAttack.ChargeStarted += HandleChargeStarted;
            }

            if (lastBossController != null)
            {
                lastBossController.Died += HandleLastBossDied;
            }
        }

        private void UnsubscribeEvents()
        {
            if (enemyController != null)
            {
                enemyController.Died -= HandleEnemyDied;
            }

            if (rangedAttack != null)
            {
                rangedAttack.ProjectileFired -= HandleProjectileFired;
            }

            if (tackleAttack != null)
            {
                tackleAttack.ChargeStarted -= HandleChargeStarted;
            }

            if (stageBossAttack != null)
            {
                stageBossAttack.ChargeStarted -= HandleChargeStarted;
            }

            if (lastBossController != null)
            {
                lastBossController.Died -= HandleLastBossDied;
            }
        }

        private void HandleEnemyDied()
        {
            if (!IsVisibleToMainCamera())
            {
                return;
            }

            EnemySfxManager manager = EnemySfxManager.GetOrCreate(profile);
            if (enemyKind == EnemySfxKind.StageBoss)
            {
                // StageBossはEnemyControllerで死亡するが、通常敵死亡SEではなく専用SEを鳴らす。
                manager.PlayStageBossDead(profile);
                return;
            }

            if (enemyKind == EnemySfxKind.NormalEnemy)
            {
                manager.PlayEnemyDead(profile);
            }
        }

        private void HandleLastBossDied()
        {
            if (enemyKind != EnemySfxKind.LastBoss || !IsVisibleToMainCamera())
            {
                return;
            }

            EnemySfxManager.GetOrCreate(profile).PlayLastBossDead(profile);
        }

        private void HandleProjectileFired()
        {
            if (IsVisibleToMainCamera())
            {
                EnemySfxManager.GetOrCreate(profile).PlayEnemyRangedAttack(profile);
            }
        }

        private void HandleChargeStarted()
        {
            if (IsVisibleToMainCamera())
            {
                EnemySfxManager.GetOrCreate(profile).PlayEnemyTackleAttack(profile);
            }
        }

        private void RefreshVisibilityRenderersIfNeeded()
        {
            if (!autoCollectRenderers)
            {
                return;
            }

            if (visibilityRenderers != null && visibilityRenderers.Length > 0)
            {
                return;
            }

            // prefabごとの手動設定を省くため、子オブジェクトのRendererを自動収集する。
            visibilityRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }
}
