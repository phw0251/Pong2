using UnityEngine;

public class RelaySessionData : MonoBehaviour
{
    public static RelaySessionData Instance;
    public string joinCode;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject); // 중복 방지
        }
    }
}
