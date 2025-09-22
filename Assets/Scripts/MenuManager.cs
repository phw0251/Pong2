using UnityEngine;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private InputField joinCodeInput;
    [SerializeField] private Text infoText;

    private void Start()
    {
        hostButton.onClick.AddListener(OnHost);
        joinButton.onClick.AddListener(OnJoin);
    }

    private async void OnHost()
    {
        infoText.text = "Starting Host...";
        string code = await RelayManager.Instance.StartHostAsync(1);
        if (!string.IsNullOrEmpty(code))
            infoText.text = $"Host started with JoinCode: {code}";
        else
            infoText.text = "Host failed to start.";
    }

    private async void OnJoin()
    {
        string code = joinCodeInput.text.Trim();
        if (string.IsNullOrEmpty(code))
        {
            infoText.text = "Join Code is empty!";
            return;
        }

        bool success = await RelayManager.Instance.StartClientAsync(code);
        infoText.text = success ? "Client started. Connecting..." : "Client failed to start.";
    }
}
