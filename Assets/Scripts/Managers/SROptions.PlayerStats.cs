using System;
using System.ComponentModel;
using Player;
using UnityEngine;

public partial class SROptions
{
    private PlayerStatsData CurrentPlayerStatsData
    {
        get
        {
            var player = UnityEngine.Object.FindFirstObjectByType<global::PlayerController>();
            if (player == null)
            {
                return null;
            }

            return player.GetPlayerStatsData();
        }
    }

    [Category("プレイヤー調整①")]
    [DisplayName("現在のステータスデータ")]
    [Sort(0)]
    public string CurrentPlayerStatsDataName
    {
        get
        {
            var statsData = CurrentPlayerStatsData;
            return statsData != null ? statsData.name : "未設定";
        }
    }

    [Category("プレイヤー調整①")]
    [DisplayName("移動速度")]
    [Sort(1)]
    [Increment(0.1)]
    public float MoveSpeed
    {
        get => CurrentPlayerStatsData != null ? CurrentPlayerStatsData.MoveSpeed : 0f;
        set => UpdateCurrentPlayerStatsData(stats => stats.SetMoveSpeed(value));
    }

    [Category("プレイヤー調整①")]
    [DisplayName("滑空移動速度")]
    [Sort(2)]
    [Increment(0.1)]
    public float GlideMoveSpeed
    {
        get => CurrentPlayerStatsData != null ? CurrentPlayerStatsData.GlideMoveSpeed : 0f;
        set => UpdateCurrentPlayerStatsData(stats => stats.SetGlideMoveSpeed(value));
    }

    [Category("プレイヤー調整①")]
    [DisplayName("滑空落下上限")]
    [Sort(3)]
    [Increment(0.1)]
    public float FallSpeed
    {
        get => CurrentPlayerStatsData != null ? CurrentPlayerStatsData.FallSpeed : 0f;
        set => UpdateCurrentPlayerStatsData(stats => stats.SetFallSpeed(value));
    }

    [Category("プレイヤー調整①")]
    [DisplayName("ジャンプ力")]
    [Sort(4)]
    [Increment(0.1)]
    public float JumpForce
    {
        get => CurrentPlayerStatsData != null ? CurrentPlayerStatsData.JumpForce : 0f;
        set => UpdateCurrentPlayerStatsData(stats => stats.SetJumpForce(value));
    }

    [Category("プレイヤー調整②")]
    [DisplayName("回避距離")]
    [Sort(0)]
    [Increment(0.1)]
    public float DodgeDistance
    {
        get => CurrentPlayerStatsData != null ? CurrentPlayerStatsData.DodgeDistance : 0f;
        set => UpdateCurrentPlayerStatsData(stats => stats.SetDodgeDistance(value));
    }

    [Category("プレイヤー調整②")]
    [DisplayName("回避時間")]
    [Sort(1)]
    [Increment(0.01)]
    public float DodgeDuration
    {
        get => CurrentPlayerStatsData != null ? CurrentPlayerStatsData.DodgeDuration : 0f;
        set => UpdateCurrentPlayerStatsData(stats => stats.SetDodgeDuration(value));
    }

    [Category("プレイヤー調整②")]
    [DisplayName("回避クールタイム")]
    [Sort(2)]
    [Increment(0.01)]
    public float DodgeCooldown
    {
        get => CurrentPlayerStatsData != null ? CurrentPlayerStatsData.DodgeCooldown : 0f;
        set => UpdateCurrentPlayerStatsData(stats => stats.SetDodgeCooldown(value));
    }

    [Category("プレイヤー調整②")]
    [DisplayName("攻撃間隔(秒)")]
    [Sort(3)]
    [Increment(0.01)]
    public float AttackSecondsPerAttack
    {
        get => CurrentPlayerStatsData != null ? CurrentPlayerStatsData.AttackSecondsPerAttack : 0f;
        set => UpdateCurrentPlayerStatsData(stats => stats.SetAttackSecondsPerAttack(value));
    }

    [Category("プレイヤー調整②")]
    [DisplayName("傘攻撃持続")]
    [Sort(4)]
    [Increment(0.01)]
    public float UmbrellaAttackDuration
    {
        get => CurrentPlayerStatsData != null ? CurrentPlayerStatsData.UmbrellaAttackDuration : 0f;
        set => UpdateCurrentPlayerStatsData(stats => stats.SetUmbrellaAttackDuration(value));
    }

    [Category("プレイヤー調整②")]
    [DisplayName("Player Attack Damage")]
    [Sort(5)]
    [Increment(1)]
    public int PlayerAttackDamage
    {
        get => CurrentPlayerStatsData != null ? CurrentPlayerStatsData.PlayerAttackDamage : 0;
        set => UpdateCurrentPlayerStatsData(stats => stats.SetPlayerAttackDamage(value));
    }

    [Category("プレイヤー調整②")]
    [DisplayName("銃反動")]
    [Sort(6)]
    [Increment(0.1)]
    public float GunRecoilForce
    {
        get => CurrentPlayerStatsData != null ? CurrentPlayerStatsData.GunRecoilForce : 0f;
        set => UpdateCurrentPlayerStatsData(stats => stats.SetGunRecoilForce(value));
    }

    [Category("プレイヤー調整③")]
    [DisplayName("反動時間")]
    [Sort(0)]
    [Increment(0.01)]
    public float GunRecoilDuration
    {
        get => CurrentPlayerStatsData != null ? CurrentPlayerStatsData.GunRecoilDuration : 0f;
        set => UpdateCurrentPlayerStatsData(stats => stats.SetGunRecoilDuration(value));
    }

    [Category("プレイヤー調整③")]
    [DisplayName("リロード秒数")]
    [Sort(1)]
    [Increment(0.01)]
    public float ReloadSeconds
    {
        get => CurrentPlayerStatsData != null ? CurrentPlayerStatsData.ReloadSeconds : 0f;
        set => UpdateCurrentPlayerStatsData(stats => stats.SetReloadSeconds(value));
    }

    [Category("プレイヤー調整③")]
    [DisplayName("パリィ有効時間")]
    [Sort(2)]
    [Increment(0.01)]
    public float ParryDuration
    {
        get => CurrentPlayerStatsData != null ? CurrentPlayerStatsData.ParryDuration : 0f;
        set => UpdateCurrentPlayerStatsData(stats => stats.SetParryDuration(value));
    }

    [Category("プレイヤー調整③")]
    [DisplayName("パリィフラッシュ時間")]
    [Sort(3)]
    [Increment(0.01)]
    public float ParryFlashDuration
    {
        get => CurrentPlayerStatsData != null ? CurrentPlayerStatsData.ParryFlashDuration : 0f;
        set => UpdateCurrentPlayerStatsData(stats => stats.SetParryFlashDuration(value));
    }

    [Category("プレイヤー調整③")]
    [DisplayName("最大HP")]
    [Sort(4)]
    [Increment(1)]
    public int MaxHealth
    {
        get => CurrentPlayerStatsData != null ? CurrentPlayerStatsData.MaxHealth : 0;
        set => UpdateCurrentPlayerStatsData(stats => stats.SetMaxHealth(value));
    }

    private static void UpdateCurrentPlayerStatsData(Action<PlayerStatsData> updater)
    {
        if (updater == null)
        {
            return;
        }

        var player = UnityEngine.Object.FindFirstObjectByType<global::PlayerController>();
        if (player == null)
        {
            return;
        }

        var statsData = player.GetPlayerStatsData();
        if (statsData == null)
        {
            return;
        }

        updater(statsData);
        player.SetPlayerStatsData(statsData);
    }
}
