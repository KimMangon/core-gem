using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>Shared boulder health. Deliberately does not implement player-weapon IDamageable.</summary>
[RequireComponent(typeof(BoulderSlopeDrag))]
public class BoulderHealth : NetworkBehaviour
{
    public static BoulderHealth Instance { get; private set; }
    [SerializeField, Min(1)] private int maximumHealth = 5;
    private readonly NetworkVariable<int> health = new(5);
    private double invulnerableUntil;
    private bool recovering;
    private int recoveryGeneration;
    private MeshRenderer stoneRenderer;
    private Collider[] stoneColliders;
    private bool[] colliderStates;
    private Rigidbody stoneBody;
    private bool bodyWasKinematic;
    private bool frozenForBreak;

    public int CurrentHealth => health.Value;
    public int MaximumHealth => maximumHealth;
    public bool IsRecovering => recovering || health.Value <= 0;

    public override void OnNetworkSpawn()
    {
        Instance = this;
        stoneRenderer=GetComponent<MeshRenderer>();stoneBody=GetComponent<Rigidbody>();
        stoneColliders=GetComponentsInChildren<Collider>(true);colliderStates=new bool[stoneColliders.Length];
        health.OnValueChanged+=OnHealthChanged;
        if (IsServer) health.Value = maximumHealth;
        SetStoneVisible(health.Value>0);
    }

    public override void OnNetworkDespawn()
    {
        StopAllCoroutines();
        health.OnValueChanged-=OnHealthChanged;
        RestoreBreakPhysics();SetStoneVisible(true);
        recoveryGeneration++;
        recovering = false;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (IsServer && IsSpawned && StageManager.Instance != null &&
            !StageManager.Instance.IsGameStarted && health.Value != maximumHealth)
            RestoreHealth(maximumHealth);
    }

    public void TakeMonsterDamage(BoulderMonster attacker, int amount)
    {
        StageManager stage = StageManager.Instance;
        if (!IsServer || !IsSpawned || amount <= 0 || recovering ||
            attacker == null || !attacker.IsSpawned || attacker.IsDead ||
            stage == null || !stage.IsGameStarted || stage.RoomTimerFinished ||
            NetworkManager.ServerTime.Time < invulnerableUntil)
            return;
        ApplyDamage(amount);
    }

    public void TakeCannonDamage(Stage5Projectile projectile, int amount)
    {
        StageManager stage = StageManager.Instance;
        if (!IsServer || !IsSpawned || recovering || amount <= 0 || projectile == null ||
            !projectile.IsSpawned || projectile.ActiveSection != 8 || stage == null ||
            !stage.IsGameStarted || stage.CurrentSection != 8 || stage.RoomTimerFinished ||
            NetworkManager.ServerTime.Time < invulnerableUntil) return;
        ApplyDamage(amount);
    }

    private void ApplyDamage(int amount)
    {
        health.Value = Mathf.Max(0, health.Value - amount);
        if (health.Value == 0)
        {
            recovering = true;
            FreezeForBreak();
            BreakPresentationRpc(transform.position,transform.rotation,transform.lossyScale);
            StartCoroutine(RecoverAtCheckpoint(++recoveryGeneration));
        }
    }

    private IEnumerator RecoverAtCheckpoint(int generation)
    {
        yield return new WaitForSeconds(1f);
        if (!IsServer || !IsSpawned || generation != recoveryGeneration) yield break;
        RestoreBreakPhysics();
        StageManager stage = StageManager.Instance;
        if (stage != null && stage.IsGameStarted && !stage.RoomTimerFinished)
        {
            stage.RespawnBoulder(this);
            MonsterSpawnZone.ResetAllForRetry();
            Stage8CannonControls.ResetForBoulderRetry();
        }
        RestoreHealth(maximumHealth);
        invulnerableUntil = NetworkManager.ServerTime.Time + 2d;
    }

    public void RestoreHealth(int savedHealth)
    {
        if (!IsServer || !IsSpawned) return;
        RestoreBreakPhysics();
        recoveryGeneration++;
        health.Value = Mathf.Clamp(savedHealth <= 0 ? maximumHealth : savedHealth,
            1, maximumHealth);
        recovering = false;
    }

    public void ResetAfterFall()
    {
        if (!IsServer || !IsSpawned || recovering) return;
        RestoreHealth(maximumHealth);
        invulnerableUntil = NetworkManager.ServerTime.Time + 2d;
        Stage8CannonControls.ResetForBoulderRetry();
    }

    private void OnHealthChanged(int previous,int current) => SetStoneVisible(current>0);
    private void SetStoneVisible(bool visible) {if(stoneRenderer!=null)stoneRenderer.enabled=visible;}

    [Rpc(SendTo.ClientsAndHost)]
    private void BreakPresentationRpc(Vector3 position,Quaternion rotation,Vector3 worldScale)
    {
        SetStoneVisible(false);
        BoulderBreakEffect.Play(stoneRenderer,position,rotation,worldScale);
    }

    private void FreezeForBreak()
    {
        if(frozenForBreak)return;
        frozenForBreak=true;
        if(stoneBody!=null)
        {
            bodyWasKinematic=stoneBody.isKinematic;
            if(!stoneBody.isKinematic){stoneBody.linearVelocity=Vector3.zero;stoneBody.angularVelocity=Vector3.zero;}
            stoneBody.isKinematic=true;
        }
        for(int i=0;i<stoneColliders.Length;i++)
        {
            colliderStates[i]=stoneColliders[i].enabled;stoneColliders[i].enabled=false;
        }
    }

    private void RestoreBreakPhysics()
    {
        if(!frozenForBreak)return;
        frozenForBreak=false;
        if(stoneBody!=null)stoneBody.isKinematic=bodyWasKinematic;
        for(int i=0;i<stoneColliders.Length;i++)if(stoneColliders[i]!=null)stoneColliders[i].enabled=colliderStates[i];
    }
}
