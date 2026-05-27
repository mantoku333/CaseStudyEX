using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 下から並んだブロック群を段階的に上げ下げするシャッター壁
public class ShutterWallBlockRise : MonoBehaviour
{
    // 下から上の順で扱うブロック一覧
    [SerializeField] private List<Transform> blocksFromBottom = new List<Transform>();
    // オンのとき、子オブジェクトから一覧を再構築する
    [SerializeField] private bool autoCollectChildren = true;

    [Header("Motion")]
    // 起動時に開いた状態として扱うか
    [SerializeField] private bool startsOpened = false;
    // 1ステージ移動にかける時間
    [SerializeField, Min(0.01f)] private float riseDurationPerBlock = 0.12f;
    // ステージ間の待機時間
    [SerializeField, Min(0f)] private float intervalBetweenBlocks = 0.06f;
    // オンの場合、開いた後は閉じない
    [SerializeField] private bool lockAfterOpen = true;

    // 各ブロックの閉状態ローカル座標（初期値）を保持
    private readonly List<Vector3> targetLocalPositions = new List<Vector3>();

    // 開閉コルーチンが設定されていれば移動中
    private Coroutine openRoutine;
    // 現在の論理状態
    private bool isOpen;
    // 初期化済みフラグ
    private bool initialized;

    // 現在開閉中かどうか
    public bool IsTransitioning
    {
        get
        {
            InitializeIfNeeded();
            return openRoutine != null;
        }
    }

    // 現在開いているかどうか
    public bool IsOpen
    {
        get
        {
            InitializeIfNeeded();
            return isOpen;
        }
    }

    private void Awake()
    {
        InitializeIfNeeded();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && autoCollectChildren && (blocksFromBottom == null || blocksFromBottom.Count == 0))
        {
            RebuildBlockList();
        }
    }
#endif

    public bool TryOpen()
    {
        InitializeIfNeeded();

        // 移動中は受け付けない
        if (openRoutine != null)
        {
            return false;
        }

        // 既に開いている場合は何もしない
        if (isOpen)
        {
            return false;
        }

        openRoutine = StartCoroutine(OpenRoutine(riseDurationPerBlock, intervalBetweenBlocks));
        return true;
    }

    public bool TryClose()
    {
        return TryClose(riseDurationPerBlock, intervalBetweenBlocks);
    }

    public bool TryClose(float durationPerBlock, float intervalBetweenBlocksOverride)
    {
        InitializeIfNeeded();

        // 移動中は受け付けない
        if (openRoutine != null)
        {
            return false;
        }

        // 「開いた後は閉じない」設定が有効なとき、または閉状態のときは閉じ処理を開始しない
        if (lockAfterOpen || !isOpen)
        {
            return false;
        }

        openRoutine = StartCoroutine(CloseRoutine(durationPerBlock, intervalBetweenBlocksOverride));
        return true;
    }

    public void Open()
    {
        // 既存呼び出しとの互換用。成否が必要なら TryOpen を利用する
        TryOpen();
    }

    public void Close()
    {
        // 既存呼び出しとの互換用。成否が必要なら TryClose を利用する
        TryClose();
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        if (autoCollectChildren || blocksFromBottom.Count == 0)
        {
            RebuildBlockList();
        }

        // 現在の配置を閉状態の目標位置として記録
        targetLocalPositions.Clear();

        for (int i = 0; i < blocksFromBottom.Count; i++)
        {
            Transform block = blocksFromBottom[i];
            Vector3 target = block != null ? block.localPosition : Vector3.zero;
            targetLocalPositions.Add(target);
        }

        isOpen = startsOpened;
        if (startsOpened)
        {
            ApplyOpenPositionImmediate();
        }
    }

    private IEnumerator OpenRoutine(float durationPerBlock, float intervalBetweenBlocksOverride)
    {
        int blockCount = blocksFromBottom.Count;
        if (blockCount == 0)
        {
            openRoutine = null;
            yield break;
        }

        List<Vector3> openStartPositions = new List<Vector3>(blockCount);
        for (int i = 0; i < blockCount; i++)
        {
            Transform block = blocksFromBottom[i];
            openStartPositions.Add(block != null ? block.localPosition : Vector3.zero);
        }

        float stepHeight = ResolveStepHeight(openStartPositions);

        if (blockCount == 1)
        {
            yield return MoveBlocksToY(1, openStartPositions[0].y + stepHeight, durationPerBlock);
            isOpen = true;
            openRoutine = null;
            yield break;
        }

        // ステージ1: 最下段のみを2段目に重ねる
        // ステージ2: 下2段を3段目に重ねる以降同様
        for (int stage = 1; stage < blockCount; stage++)
        {
            float overlapY = openStartPositions[stage].y;
            yield return MoveBlocksToY(stage, overlapY, durationPerBlock);

            if (intervalBetweenBlocksOverride > 0f)
            {
                yield return new WaitForSeconds(intervalBetweenBlocksOverride);
            }
        }

        // 最終ステージ: 全段を最上段のさらに1段上まで持ち上げる
        float finalOpenY = openStartPositions[blockCount - 1].y + stepHeight;
        yield return MoveBlocksToY(blockCount, finalOpenY, durationPerBlock);

        isOpen = true;
        openRoutine = null;
    }

    private IEnumerator CloseRoutine(float durationPerBlock, float intervalBetweenBlocksOverride)
    {
        int blockCount = blocksFromBottom.Count;
        if (blockCount == 0)
        {
            openRoutine = null;
            yield break;
        }

        if (targetLocalPositions.Count < blockCount)
        {
            openRoutine = null;
            yield break;
        }

        if (blockCount == 1)
        {
            yield return MoveBlocksToY(1, targetLocalPositions[0].y, durationPerBlock);
            isOpen = false;
            openRoutine = null;
            yield break;
        }

        // 開き処理の逆順:
        // 1) 全段持ち上げを戻す
        // 2) 上から順に重なりを解いて元位置へ戻す
        float topClosedY = targetLocalPositions[blockCount - 1].y;
        yield return MoveBlocksToY(blockCount, topClosedY, durationPerBlock);

        if (intervalBetweenBlocksOverride > 0f)
        {
            yield return new WaitForSeconds(intervalBetweenBlocksOverride);
        }

        for (int stage = blockCount - 1; stage >= 1; stage--)
        {
            float targetY = targetLocalPositions[stage - 1].y;
            yield return MoveBlocksToY(stage, targetY, durationPerBlock);

            if (stage > 1 && intervalBetweenBlocksOverride > 0f)
            {
                yield return new WaitForSeconds(intervalBetweenBlocksOverride);
            }
        }

        isOpen = false;
        openRoutine = null;
    }

    private IEnumerator MoveBlocksToY(int countFromBottom, float targetY, float durationPerBlock)
    {
        if (countFromBottom <= 0)
        {
            yield break;
        }

        List<Transform> movingBlocks = new List<Transform>(countFromBottom);
        List<Vector3> startPositions = new List<Vector3>(countFromBottom);

        for (int i = 0; i < countFromBottom && i < blocksFromBottom.Count; i++)
        {
            Transform block = blocksFromBottom[i];
            if (block == null)
            {
                continue;
            }

            movingBlocks.Add(block);
            startPositions.Add(block.localPosition);
        }

        if (movingBlocks.Count == 0)
        {
            yield break;
        }

        // なめらかな補間で急な動きを抑える
        float duration = Mathf.Max(0.01f, durationPerBlock);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            for (int i = 0; i < movingBlocks.Count; i++)
            {
                Vector3 start = startPositions[i];
                Vector3 target = new Vector3(start.x, targetY, start.z);
                movingBlocks[i].localPosition = Vector3.LerpUnclamped(start, target, eased);
            }

            yield return null;
        }

        for (int i = 0; i < movingBlocks.Count; i++)
        {
            Vector3 start = startPositions[i];
            movingBlocks[i].localPosition = new Vector3(start.x, targetY, start.z);
        }
    }

    private void ApplyOpenPositionImmediate()
    {
        int blockCount = blocksFromBottom.Count;
        if (blockCount == 0 || targetLocalPositions.Count < blockCount)
        {
            return;
        }

        float stepHeight = ResolveStepHeight(targetLocalPositions);
        float finalOpenY = targetLocalPositions[blockCount - 1].y + stepHeight;

        for (int i = 0; i < blockCount; i++)
        {
            Transform block = blocksFromBottom[i];
            if (block == null)
            {
                continue;
            }

            Vector3 localPosition = block.localPosition;
            block.localPosition = new Vector3(localPosition.x, finalOpenY, localPosition.z);
        }
    }

    private float ResolveStepHeight(List<Vector3> referencePositions)
    {
        // まず現在配置から段差を推定
        float step = FindMinPositiveYStep(referencePositions);

        // 推定不可なら初期配置から再推定
        if (!float.IsFinite(step) || step <= 0.001f)
        {
            step = FindMinPositiveYStep(targetLocalPositions);
        }

        // それでも不可なら安全値を採用
        if (!float.IsFinite(step) || step <= 0.001f)
        {
            step = 1f;
        }

        return step;
    }

    private static float FindMinPositiveYStep(List<Vector3> positions)
    {
        if (positions == null || positions.Count < 2)
        {
            return float.PositiveInfinity;
        }

        // 隣接ブロック間の最小正差分を段差として使う
        float minStep = float.PositiveInfinity;

        for (int i = 1; i < positions.Count; i++)
        {
            float step = Mathf.Abs(positions[i].y - positions[i - 1].y);
            if (step > 0.001f)
            {
                minStep = Mathf.Min(minStep, step);
            }
        }

        return minStep;
    }

    private void RebuildBlockList()
    {
        blocksFromBottom.Clear();

        // 子オブジェクトを収集し、Y昇順（下→上）に並べる
        foreach (Transform child in transform)
        {
            blocksFromBottom.Add(child);
        }

        blocksFromBottom.Sort((a, b) =>
        {
            float ay = a != null ? a.localPosition.y : float.PositiveInfinity;
            float by = b != null ? b.localPosition.y : float.PositiveInfinity;
            return ay.CompareTo(by);
        });
    }
}
