using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Keyboard input that works with either the new Input System or the legacy Input Manager,
/// whichever one the project has enabled.
/// </summary>
public static class InputHelper
{
#if ENABLE_INPUT_SYSTEM
    static bool Pressed(Key k)
    {
        var kb = Keyboard.current;
        return kb != null && kb[k].wasPressedThisFrame;
    }

    public static bool LeftPressed()  { return Pressed(Key.A) || Pressed(Key.LeftArrow); }
    public static bool RightPressed() { return Pressed(Key.D) || Pressed(Key.RightArrow); }
    public static bool JumpPressed()  { return Pressed(Key.Space) || Pressed(Key.W) || Pressed(Key.UpArrow); }
    public static bool ConfirmPressed() { return Pressed(Key.Enter) || Pressed(Key.NumpadEnter) || Pressed(Key.Space); }
    public static bool RestartPressed() { return Pressed(Key.R); }
    public static bool BackPressed()    { return Pressed(Key.Escape); }
#else
    public static bool LeftPressed()  { return Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow); }
    public static bool RightPressed() { return Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow); }
    public static bool JumpPressed()  { return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow); }
    public static bool ConfirmPressed() { return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space); }
    public static bool RestartPressed() { return Input.GetKeyDown(KeyCode.R); }
    public static bool BackPressed()    { return Input.GetKeyDown(KeyCode.Escape); }
#endif
}
