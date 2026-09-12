using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    [SerializeField] private float timeLimit = 3f; // 제한시간

    private List<ulong> turnOrder = new List<ulong>();
    private int currentTurnIndex = 0;
    private float roundElapsedTime = 0f;
    private float pressStartTime;
    private bool timerRunning = false;

    private void StartNewRound()
    {
        turnOrder = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);
        for (int i = 0; i < turnOrder.Count; i++) // 순서 랜덤 셔플
        {
            int rand = Random.Range(i, turnOrder.Count);
            (turnOrder[i], turnOrder[rand]) = (turnOrder[rand], turnOrder[i]);
        }
        currentTurnIndex = 0;
        roundElapsedTime = 0f;
        Debug.Log($"[서버] 새 라운드 시작, 순서: {string.Join(",", turnOrder)}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void StartRoundServerRpc()
    {
        StartNewRound();
    }

    [ServerRpc(RequireOwnership = false)]
    public void PressButtonServerRpc(ulong senderId)
    {
        if (turnOrder[currentTurnIndex] != senderId)
        {
            Debug.Log($"[서버] {senderId}의 차례가 아님, 무시");
            return;
        }

        if (!timerRunning) // 첫 번째 누름
        {
            pressStartTime = Time.time;
            timerRunning = true;
            Debug.Log($"[서버] {senderId} 타이머 시작");
        }
        else // 두 번째 누름
        {
            float segment = Time.time - pressStartTime; // 이번 플레이어 구간만의 시간
            roundElapsedTime += segment; // 라운드 누적에 더함 (초기화 안 함)
            timerRunning = false;
            Debug.Log($"[서버] {senderId} 정지, 이번 구간={segment:F2}초, 누적={roundElapsedTime:F2}초");

            if (roundElapsedTime > timeLimit) // 개인 시간이 아니라 누적 시간과 비교
                Debug.Log($"[서버] {senderId} 초과! (목숨 차감은 다음 단계에서 구현)");
            else
            {
                currentTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;
                Debug.Log($"[서버] 다음 차례: {turnOrder[currentTurnIndex]}");
            }
        }
    }
}