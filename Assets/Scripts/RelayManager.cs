using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance { get; private set; }
    [SerializeField] private UnityTransport transport;
    public string JoinCode { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // 연결을 직접 승인하도록 설정
        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
        NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
    }

    private async void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        }
        else
        {
            Debug.LogWarning("No NetworkManager found. Please add one to the scene.");
        }
        await InitServices();
    }
    
    private void OnEnable()
    {
        // 접속이 종료된 경우 호출되는 콜백을 등록
        NetworkManager.Singleton.OnClientDisconnectCallback
            += OnClientDisconnectCallback;
    }

    private void OnDisable()
    {
        // 게임 종료시 네트워크 매니저가 먼저 파괴되는 경우에 대한 예외 처리
        if (NetworkManager.Singleton != null)
        {
            // 접속이 종료된 경우 호출되는 콜백을 해제
            NetworkManager.Singleton.OnClientDisconnectCallback
                -= OnClientDisconnectCallback;
        }
    }

    private async Task InitServices()
    {
        if (UnityServices.State == ServicesInitializationState.Initialized)
            return;

        try
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("UGS 로그인 성공");
        }
        catch (Exception e)
        {
            Debug.LogError($"UGS 초기화 실패: {e}");
        }
    }

    // 호스트 시작
    public async Task<string> StartHostAsync(int maxConnections = 1)
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            Debug.Log($"Relay 할당 성공 / JoinCode: {JoinCode}");

            transport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("Host 시작");
                NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
                return JoinCode;
            }
            else
            {
                Debug.LogError("Host 시작 실패");
                return null;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Relay Host 생성 실패: {e}");
            return null;
        }
    }

    // 클라이언트 시작
    public async Task<bool> StartClientAsync(string joinCode)
    {
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            transport.SetClientRelayData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData
            );

            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log("Client 시작");
                return true;
            }
            else
            {
                Debug.LogError("Client 시작 실패");
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Relay Client 연결 실패: {e}");
            return false;
        }
    }
    
    // 클라이언트가 연결을 끊었을 때 호출되는 콜백
    private void OnClientDisconnectCallback(ulong obj)
    {
        // 연결 종료 이유를 가져옴
        var disconnectReason = NetworkManager.Singleton.DisconnectReason;
        Debug.Log(disconnectReason);
    }
    
    // 연결을 승인할 때 호출되는 콜백
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        // 총 플레이어 수가 2명 이상이면 연결을 거부
        if (NetworkManager.Singleton.ConnectedClientsList.Count < 2)
        {
            response.Approved = true;
            // 플레이어 오브젝트는 코드로 직접 생성, 자동x
            response.CreatePlayerObject = false;
        }
        else
        {
            response.Approved = false;
            response.Reason = "Max Player in session is 2";
        }
    }
}
