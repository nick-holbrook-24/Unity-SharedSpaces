using UnityEngine;

public class DoNotDestroy : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    public void AddDoNotDestroyToGO(GameObject go)
    {
        if (go == null)
        {
            Debug.LogError("GameObject is null");
            return;
        }

        if (go.GetComponent<DoNotDestroy>() == null)
        {
            DontDestroyOnLoad(go);
        }
    }
}
