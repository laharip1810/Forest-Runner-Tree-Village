using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Adds the correct UI input module at runtime so buttons work whether the project
/// uses the new Input System or the legacy Input Manager.
/// </summary>
[RequireComponent(typeof(EventSystem))]
public class EventSystemSetup : MonoBehaviour
{
    void Awake()
    {
        if (GetComponent<BaseInputModule>() != null) return;
#if ENABLE_INPUT_SYSTEM
        var module = gameObject.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
#else
        gameObject.AddComponent<StandaloneInputModule>();
#endif
    }
}
