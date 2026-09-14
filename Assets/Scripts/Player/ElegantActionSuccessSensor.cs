using System;
using System.Collections.Generic;
using GameName.Enemy;
using Metroidvania.Enemy;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public sealed class ElegantActionSuccessSensor : MonoBehaviour
{
    [SerializeField, Min(0f)] private float minimumHeadClearance = 0.05f;
    [SerializeField, Min(0.1f)] private float maximumHeadClearance = 2f;
    [SerializeField, Min(0.1f)] private float backKillWindowSeconds = 2f;
    [SerializeField, Min(0.1f)] private float teleportDistance = 4f;
    private readonly Collider2D[] overlaps = new Collider2D[128];
    private readonly RaycastHit2D[] sweepHits = new RaycastHit2D[128];
    private readonly Dictionary<Component, float> crossedEnemies = new Dictionary<Component, float>();
    private readonly HashSet<Collider2D> dodgeCandidates = new HashSet<Collider2D>();
    private readonly List<Component> expired = new List<Component>();
    private DodgeController dodge;
    private PlayerController player;
    private Collider2D body;
    private Vector2 previousCenter;
    private Vector2 previousFeet;
    private Vector2 dodgeOrigin;
    private bool trackingDodge;
    private bool dodgeSucceeded;
    private bool previouslyAirborne;
    private float finishDodgeAfter;
    private ContactFilter2D filter;

    public event Action<ElegantActionType> Succeeded;
    public bool CurrentDodgeSucceeded => dodgeSucceeded;

    private void OnEnable()
    {
        player = GetComponent<PlayerController>();
        dodge = GetComponent<DodgeController>();
        foreach (Collider2D candidate in GetComponents<Collider2D>())
            if (!candidate.isTrigger) { body = candidate; break; }
        filter = new ContactFilter2D { useTriggers = true, useLayerMask = false };
        previousCenter = BodyBounds.center;
        previousFeet = Feet(BodyBounds);
        if (dodge != null)
        {
            dodge.Started += BeginDodge;
            dodge.Ended += EndDodge;
        }
    }

    private void OnDisable()
    {
        if (dodge != null)
        {
            dodge.Started -= BeginDodge;
            dodge.Ended -= EndDodge;
        }
        trackingDodge = false;
        dodgeCandidates.Clear();
        crossedEnemies.Clear();
    }

    private Bounds BodyBounds => body != null ? body.bounds : new Bounds(transform.position, new Vector3(0.5f, 1f, 1f));
    private static Vector2 Feet(Bounds bounds) => new Vector2(bounds.center.x, bounds.min.y);

    private void BeginDodge()
    {
        trackingDodge = true;
        dodgeSucceeded = false;
        finishDodgeAfter = float.PositiveInfinity;
        dodgeOrigin = BodyBounds.center;
        previousCenter = dodgeOrigin;
        dodgeCandidates.Clear();
    }

    private void EndDodge()
    {
        // MovePosition's final step is applied by physics after the coroutine finishes.
        finishDodgeAfter = Time.fixedTime + Time.fixedDeltaTime;
    }

    private void LateUpdate()
    {
        if (Time.deltaTime <= 0f) return;
        Bounds bounds = BodyBounds;
        Vector2 center = bounds.center;
        Vector2 feet = Feet(bounds);
        if (Vector2.Distance(center, previousCenter) > teleportDistance)
        {
            trackingDodge = false;
            crossedEnemies.Clear();
            dodgeCandidates.Clear();
        }
        else
        {
            if (trackingDodge && !dodgeSucceeded) SampleDodge(bounds, center);
            if (player != null && (!player.IsGrounded || previouslyAirborne)) SampleOverhead(feet);
        }
        previouslyAirborne = player != null && !player.IsGrounded;
        if (trackingDodge && Time.fixedTime >= finishDodgeAfter) trackingDodge = false;
        previousCenter = center;
        previousFeet = feet;
        expired.Clear();
        foreach (var pair in crossedEnemies)
            if (pair.Key == null || Time.time - pair.Value > backKillWindowSeconds) expired.Add(pair.Key);
        foreach (Component key in expired) crossedEnemies.Remove(key);
    }

    private void SampleDodge(Bounds bounds, Vector2 center)
    {
        Vector2 delta = center - previousCenter;
        int count = Physics2D.BoxCast(previousCenter, bounds.size, 0f, delta.normalized, filter, sweepHits, delta.magnitude);
        for (int i = 0; i < count; i++) AddDodgeCandidate(sweepHits[i].collider);
        count = Physics2D.OverlapBox(center, bounds.size, 0f, filter, overlaps);
        for (int i = 0; i < count; i++) AddDodgeCandidate(overlaps[i]);
        foreach (Collider2D candidate in dodgeCandidates)
        {
            if (candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy) continue;
            EnemyBullet bullet = candidate.GetComponentInParent<EnemyBullet>();
            if (bullet != null && bullet.IsReflectedByPlayer) continue;
            if (bullet == null && ResolveEnemy(candidate) == null) continue;
            if (!ElegantActionGeometry.PassedThrough(candidate.bounds, dodgeOrigin, center, bounds.extents)) continue;
            CompleteDodge(bullet != null ? ElegantActionType.DodgeProjectile : ElegantActionType.Dodge);
            break;
        }
    }

    private void AddDodgeCandidate(Collider2D candidate)
    {
        if (candidate == null) return;
        EnemyBullet bullet = candidate.GetComponentInParent<EnemyBullet>();
        if (bullet != null)
        {
            if (!bullet.IsReflectedByPlayer) dodgeCandidates.Add(candidate);
            return;
        }
        Component enemy = ResolveEnemy(candidate);
        if (enemy == null) return;
        Collider2D enemyBody = enemy.GetComponent<Collider2D>();
        if (enemyBody != null) dodgeCandidates.Add(enemyBody);
        else if (!candidate.isTrigger) dodgeCandidates.Add(candidate);
    }

    // Moving bullets can hit the body between render samples and are destroyed on contact.
    public void NotifyDodgedProjectile()
    {
        if (isActiveAndEnabled && dodge != null && dodge.IsDodging() && Time.timeScale > 0f)
            CompleteDodge(ElegantActionType.DodgeProjectile);
    }

    private void CompleteDodge(ElegantActionType action)
    {
        if (!trackingDodge || dodgeSucceeded) return;
        dodgeSucceeded = true;
        Succeeded?.Invoke(action);
    }

    private void SampleOverhead(Vector2 feet)
    {
        Vector2 center = (previousFeet + feet) * 0.5f + Vector2.down * (maximumHeadClearance * 0.5f);
        Vector2 size = new Vector2(Mathf.Abs(feet.x - previousFeet.x) + 2f,
            Mathf.Abs(feet.y - previousFeet.y) + maximumHeadClearance + 2f);
        int count = Physics2D.OverlapBox(center, size, 0f, filter, overlaps);
        for (int i = 0; i < count; i++)
        {
            Component enemy = ResolveEnemy(overlaps[i]);
            if (enemy == null) continue;
            Collider2D enemyBody = enemy.GetComponent<Collider2D>();
            if (enemyBody == null) continue;
            int facing = enemy is EnemyController normal ? normal.FacingDirection : ((LastBossController)enemy).FacingDirection;
            if (ElegantActionGeometry.CrossedOverHead(enemyBody.bounds, facing, previousFeet, feet,
                minimumHeadClearance, maximumHeadClearance)) crossedEnemies[enemy] = Time.time;
        }
    }

    public bool ConsumeOverheadCrossing(Component enemy)
    {
        if (enemy == null || !crossedEnemies.TryGetValue(enemy, out float crossedAt)) return false;
        crossedEnemies.Remove(enemy);
        return Time.time - crossedAt <= backKillWindowSeconds;
    }

    private static Component ResolveEnemy(Collider2D candidate)
    {
        if (candidate == null) return null;
        EnemyController enemy = candidate.GetComponentInParent<EnemyController>();
        if (enemy != null) return enemy.CurrentHealth > 0 ? enemy : null;
        LastBossController boss = candidate.GetComponentInParent<LastBossController>();
        return boss != null && boss.CurrentHealth > 0 ? boss : null;
    }
}
