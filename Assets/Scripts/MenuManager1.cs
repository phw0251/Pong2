using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public class MenuManager1 : MonoBehaviour
{
    [Header("UI Elements")]
    public Text infoText;                  // 상태 표시 텍스트
    public InputField joinCodeInputField;  // 클라이언트가 입력할 JoinCode

    private void Awake()
    {
        infoText.text = string.Empty;

        // 네트워크 매니저 설정
        NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
        NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
    }

    private void OnEnable()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectCallback;
    }

    // 연결 승인 체크: 최대 2명까지만 허용
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        if (NetworkManager.Singleton.ConnectedClientsList.Count < 2)
        {
            response.Approved = true;
            response.CreatePlayerObject = false;
        }
        else
        {
            response.Approved = false;
            response.Reason = "Max Player in session is 2";
        }
    }

    // 클라이언트 접속 종료 시
    private void OnClientDisconnectCallback(ulong clientId)
    {
        var reason = NetworkManager.Singleton.DisconnectReason;
        infoText.text = $"Disconnected: {reason}";
        Debug.Log($"Client {clientId} disconnected: {reason}");
    }

    // ================== Relay 호스트 시작 ==================
    public async void CreateGameAsRelayHost()
    {
        infoText.text = "Initializing Unity Services...";
        await InitUnityServices();

        try
        {
            // 최대 2명 (Host + 1 Client)
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            RelaySessionData.Instance.joinCode = joinCode;
            
            // UnityTransport 설정
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                true // DTLS 사용
            );

            // 호스트 시작
            if (NetworkManager.Singleton.StartHost())
            {
                infoText.text = $"Join Code: {joinCode}";
                Debug.Log($"Host started with JoinCode: {joinCode}");
                NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
            }
            else
            {
                infoText.text = "Host failed to start.";
                Debug.LogError("Host failed to start.");
            }
        }
        catch (System.Exception e)
        {
            infoText.text = "Relay host error.";
            Debug.LogError($"Relay host exception: {e.Message}");
        }
    }

    // ================== Relay 클라이언트 참여 ==================
    public async void JoinGameAsRelayClient()
    {
        string joinCode = joinCodeInputField.text.Trim();
        if (string.IsNullOrEmpty(joinCode))
        {
            infoText.text = "Join Code is empty!";
            return;
        }

        infoText.text = "Initializing Unity Services...";
        await InitUnityServices();

        try
        {
            JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode);
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            transport.SetClientRelayData(
                joinAlloc.RelayServer.IpV4,
                (ushort)joinAlloc.RelayServer.Port,
                joinAlloc.AllocationIdBytes,
                joinAlloc.Key,
                joinAlloc.ConnectionData,
                joinAlloc.HostConnectionData,
                true // DTLS 사용
            );

            if (NetworkManager.Singleton.StartClient())
            {
                infoText.text = "Client started. Connecting...";
                Debug.Log("Client started successfully.");
            }
            else
            {
                infoText.text = "Client failed to start.";
                Debug.LogError("Client failed to start.");
            }
        }
        catch (System.Exception e)
        {
            infoText.text = "Relay join error.";
            Debug.LogError($"Relay client exception: {e.Message}");
        }
    }

    // ================== Unity Services 초기화 ==================
    private async Task InitUnityServices()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }
}
