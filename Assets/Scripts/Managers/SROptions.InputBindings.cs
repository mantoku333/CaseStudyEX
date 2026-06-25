using System.ComponentModel;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class SROptions
{
    private const string KeyboardMouseGroup = "Keyboard&Mouse";
    private const string GamepadGroup = "Gamepad";

    public enum KeyboardBindingChoice
    {
        Space,
        Enter,
        LeftShift,
        LeftCtrl,
        Q,
        E,
        R,
        F,
        Z,
        X,
        C,
        V,
        MouseLeft,
        MouseRight,
        Custom
    }

    public enum GamepadBindingChoice
    {
        ButtonSouth,
        ButtonEast,
        ButtonWest,
        ButtonNorth,
        LeftShoulder,
        RightShoulder,
        LeftTrigger,
        RightTrigger,
        LeftStickPress,
        RightStickPress,
        DpadUp,
        DpadDown,
        DpadLeft,
        DpadRight,
        Start,
        Select,
        Custom
    }

    [Category("キーコン(キーボード)")]
    [DisplayName("ジャンプ")]
    [Sort(0)]
    public KeyboardBindingChoice KeyboardJump
    {
        get => GetKeyboardChoice("Jump");
        set => SetKeyboardChoice("Jump", value);
    }

    [Category("キーコン(キーボード)")]
    [DisplayName("攻撃")]
    [Sort(1)]
    public KeyboardBindingChoice KeyboardAttack
    {
        get => GetKeyboardChoice("Attack");
        set => SetKeyboardChoice("Attack", value);
    }

    [Category("キーコン(キーボード)")]
    [DisplayName("回避")]
    [Sort(2)]
    public KeyboardBindingChoice KeyboardDodge
    {
        get => GetKeyboardChoice("Dodge");
        set => SetKeyboardChoice("Dodge", value);
    }

    [Category("キーコン(キーボード)")]
    [DisplayName("傘開閉/パリィ")]
    [Sort(3)]
    public KeyboardBindingChoice KeyboardUmbrellaToggle
    {
        get => GetKeyboardChoice("UmbrellaToggle");
        set => SetKeyboardChoice("UmbrellaToggle", value);
    }

    [Category("キーコン(キーボード)")]
    [DisplayName("リコイルジャンプ")]
    [Sort(4)]
    public KeyboardBindingChoice KeyboardRecoilJump
    {
        get => GetKeyboardChoice("RecoilJump");
        set => SetKeyboardChoice("RecoilJump", value);
    }

    [Category("キーコン(キーボード)")]
    [DisplayName("キーボード設定をリセット")]
    [Sort(10)]
    public void ResetKeyboardBindings()
    {
        var actions = GetCurrentPlayerActions();
        if (actions == null)
        {
            return;
        }

        PlayerInputBindingOverrides.ResetGroup(actions, KeyboardMouseGroup);
        NotifyKeyboardBindingsChanged();
    }

    [Category("キーコン(コントローラー)")]
    [DisplayName("ジャンプ")]
    [Sort(0)]
    public GamepadBindingChoice GamepadJump
    {
        get => GetGamepadChoice("Jump");
        set => SetGamepadChoice("Jump", value);
    }

    [Category("キーコン(コントローラー)")]
    [DisplayName("攻撃")]
    [Sort(1)]
    public GamepadBindingChoice GamepadAttack
    {
        get => GetGamepadChoice("Attack");
        set => SetGamepadChoice("Attack", value);
    }

    [Category("キーコン(コントローラー)")]
    [DisplayName("回避")]
    [Sort(2)]
    public GamepadBindingChoice GamepadDodge
    {
        get => GetGamepadChoice("Dodge");
        set => SetGamepadChoice("Dodge", value);
    }

    [Category("キーコン(コントローラー)")]
    [DisplayName("傘開閉/パリィ")]
    [Sort(3)]
    public GamepadBindingChoice GamepadUmbrellaToggle
    {
        get => GetGamepadChoice("UmbrellaToggle");
        set => SetGamepadChoice("UmbrellaToggle", value);
    }

    [Category("キーコン(コントローラー)")]
    [DisplayName("リコイルジャンプ")]
    [Sort(4)]
    public GamepadBindingChoice GamepadRecoilJump
    {
        get => GetGamepadChoice("RecoilJump");
        set => SetGamepadChoice("RecoilJump", value);
    }

    [Category("キーコン(コントローラー)")]
    [DisplayName("コントローラー設定をリセット")]
    [Sort(10)]
    public void ResetGamepadBindings()
    {
        var actions = GetCurrentPlayerActions();
        if (actions == null)
        {
            return;
        }

        PlayerInputBindingOverrides.ResetGroup(actions, GamepadGroup);
        NotifyGamepadBindingsChanged();
    }

    [Category("キーコン")]
    [DisplayName("キー設定をすべてリセット")]
    [Sort(100)]
    public void ResetAllBindings()
    {
        var actions = GetCurrentPlayerActions();
        if (actions == null)
        {
            return;
        }

        PlayerInputBindingOverrides.ResetGroup(actions, KeyboardMouseGroup);
        PlayerInputBindingOverrides.ResetGroup(actions, GamepadGroup);
        NotifyKeyboardBindingsChanged();
        NotifyGamepadBindingsChanged();
    }

    private void NotifyKeyboardBindingsChanged()
    {
        OnPropertyChanged(nameof(KeyboardJump));
        OnPropertyChanged(nameof(KeyboardAttack));
        OnPropertyChanged(nameof(KeyboardDodge));
        OnPropertyChanged(nameof(KeyboardUmbrellaToggle));
        OnPropertyChanged(nameof(KeyboardRecoilJump));
    }

    private void NotifyGamepadBindingsChanged()
    {
        OnPropertyChanged(nameof(GamepadJump));
        OnPropertyChanged(nameof(GamepadAttack));
        OnPropertyChanged(nameof(GamepadDodge));
        OnPropertyChanged(nameof(GamepadUmbrellaToggle));
        OnPropertyChanged(nameof(GamepadRecoilJump));
    }

    private static InputActionAsset GetCurrentPlayerActions()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<global::PlayerController>();
        if (player == null)
        {
            return null;
        }

        var playerInput = player.GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            return null;
        }

        var actions = playerInput.actions;
        if (actions != null)
        {
            PlayerInputBindingOverrides.EnsureOverridesLoaded(actions);
        }
        return actions;
    }

    private static KeyboardBindingChoice GetKeyboardChoice(string actionName)
    {
        var actions = GetCurrentPlayerActions();
        if (actions == null)
        {
            return KeyboardBindingChoice.Custom;
        }

        if (!PlayerInputBindingOverrides.TryGetBindingEffectivePath(
                actions,
                actionName,
                KeyboardMouseGroup,
                out var path,
                "<Keyboard>/"))
        {
            return KeyboardBindingChoice.Custom;
        }

        return PathToKeyboardChoice(path);
    }

    private static void SetKeyboardChoice(string actionName, KeyboardBindingChoice choice)
    {
        if (choice == KeyboardBindingChoice.Custom)
        {
            return;
        }

        var actions = GetCurrentPlayerActions();
        if (actions == null)
        {
            return;
        }

        var path = KeyboardChoiceToPath(choice);
        PlayerInputBindingOverrides.TrySetBinding(actions, actionName, KeyboardMouseGroup, path, "<Keyboard>/");
    }

    private static GamepadBindingChoice GetGamepadChoice(string actionName)
    {
        var actions = GetCurrentPlayerActions();
        if (actions == null)
        {
            return GamepadBindingChoice.Custom;
        }

        if (!PlayerInputBindingOverrides.TryGetBindingEffectivePath(
                actions,
                actionName,
                GamepadGroup,
                out var path,
                "<Gamepad>/"))
        {
            return GamepadBindingChoice.Custom;
        }

        return PathToGamepadChoice(path);
    }

    private static void SetGamepadChoice(string actionName, GamepadBindingChoice choice)
    {
        if (choice == GamepadBindingChoice.Custom)
        {
            return;
        }

        var actions = GetCurrentPlayerActions();
        if (actions == null)
        {
            return;
        }

        var path = GamepadChoiceToPath(choice);
        PlayerInputBindingOverrides.TrySetBinding(actions, actionName, GamepadGroup, path, "<Gamepad>/");
    }

    private static string KeyboardChoiceToPath(KeyboardBindingChoice choice)
    {
        switch (choice)
        {
            case KeyboardBindingChoice.Space: return "<Keyboard>/space";
            case KeyboardBindingChoice.Enter: return "<Keyboard>/enter";
            case KeyboardBindingChoice.LeftShift: return "<Keyboard>/leftShift";
            case KeyboardBindingChoice.LeftCtrl: return "<Keyboard>/leftCtrl";
            case KeyboardBindingChoice.Q: return "<Keyboard>/q";
            case KeyboardBindingChoice.E: return "<Keyboard>/e";
            case KeyboardBindingChoice.R: return "<Keyboard>/r";
            case KeyboardBindingChoice.F: return "<Keyboard>/f";
            case KeyboardBindingChoice.Z: return "<Keyboard>/z";
            case KeyboardBindingChoice.X: return "<Keyboard>/x";
            case KeyboardBindingChoice.C: return "<Keyboard>/c";
            case KeyboardBindingChoice.V: return "<Keyboard>/v";
            case KeyboardBindingChoice.MouseLeft: return "<Mouse>/leftButton";
            case KeyboardBindingChoice.MouseRight: return "<Mouse>/rightButton";
            default: return string.Empty;
        }
    }

    private static KeyboardBindingChoice PathToKeyboardChoice(string path)
    {
        switch (path)
        {
            case "<Keyboard>/space": return KeyboardBindingChoice.Space;
            case "<Keyboard>/enter": return KeyboardBindingChoice.Enter;
            case "<Keyboard>/leftShift": return KeyboardBindingChoice.LeftShift;
            case "<Keyboard>/leftCtrl": return KeyboardBindingChoice.LeftCtrl;
            case "<Keyboard>/q": return KeyboardBindingChoice.Q;
            case "<Keyboard>/e": return KeyboardBindingChoice.E;
            case "<Keyboard>/r": return KeyboardBindingChoice.R;
            case "<Keyboard>/f": return KeyboardBindingChoice.F;
            case "<Keyboard>/z": return KeyboardBindingChoice.Z;
            case "<Keyboard>/x": return KeyboardBindingChoice.X;
            case "<Keyboard>/c": return KeyboardBindingChoice.C;
            case "<Keyboard>/v": return KeyboardBindingChoice.V;
            case "<Mouse>/leftButton": return KeyboardBindingChoice.MouseLeft;
            case "<Mouse>/rightButton": return KeyboardBindingChoice.MouseRight;
            default: return KeyboardBindingChoice.Custom;
        }
    }

    private static string GamepadChoiceToPath(GamepadBindingChoice choice)
    {
        switch (choice)
        {
            case GamepadBindingChoice.ButtonSouth: return "<Gamepad>/buttonSouth";
            case GamepadBindingChoice.ButtonEast: return "<Gamepad>/buttonEast";
            case GamepadBindingChoice.ButtonWest: return "<Gamepad>/buttonWest";
            case GamepadBindingChoice.ButtonNorth: return "<Gamepad>/buttonNorth";
            case GamepadBindingChoice.LeftShoulder: return "<Gamepad>/leftShoulder";
            case GamepadBindingChoice.RightShoulder: return "<Gamepad>/rightShoulder";
            case GamepadBindingChoice.LeftTrigger: return "<Gamepad>/leftTrigger";
            case GamepadBindingChoice.RightTrigger: return "<Gamepad>/rightTrigger";
            case GamepadBindingChoice.LeftStickPress: return "<Gamepad>/leftStickPress";
            case GamepadBindingChoice.RightStickPress: return "<Gamepad>/rightStickPress";
            case GamepadBindingChoice.DpadUp: return "<Gamepad>/dpad/up";
            case GamepadBindingChoice.DpadDown: return "<Gamepad>/dpad/down";
            case GamepadBindingChoice.DpadLeft: return "<Gamepad>/dpad/left";
            case GamepadBindingChoice.DpadRight: return "<Gamepad>/dpad/right";
            case GamepadBindingChoice.Start: return "<Gamepad>/start";
            case GamepadBindingChoice.Select: return "<Gamepad>/select";
            default: return string.Empty;
        }
    }

    private static GamepadBindingChoice PathToGamepadChoice(string path)
    {
        switch (path)
        {
            case "<Gamepad>/buttonSouth": return GamepadBindingChoice.ButtonSouth;
            case "<Gamepad>/buttonEast": return GamepadBindingChoice.ButtonEast;
            case "<Gamepad>/buttonWest": return GamepadBindingChoice.ButtonWest;
            case "<Gamepad>/buttonNorth": return GamepadBindingChoice.ButtonNorth;
            case "<Gamepad>/leftShoulder": return GamepadBindingChoice.LeftShoulder;
            case "<Gamepad>/rightShoulder": return GamepadBindingChoice.RightShoulder;
            case "<Gamepad>/leftTrigger": return GamepadBindingChoice.LeftTrigger;
            case "<Gamepad>/rightTrigger": return GamepadBindingChoice.RightTrigger;
            case "<Gamepad>/leftStickPress": return GamepadBindingChoice.LeftStickPress;
            case "<Gamepad>/rightStickPress": return GamepadBindingChoice.RightStickPress;
            case "<Gamepad>/dpad/up": return GamepadBindingChoice.DpadUp;
            case "<Gamepad>/dpad/down": return GamepadBindingChoice.DpadDown;
            case "<Gamepad>/dpad/left": return GamepadBindingChoice.DpadLeft;
            case "<Gamepad>/dpad/right": return GamepadBindingChoice.DpadRight;
            case "<Gamepad>/start": return GamepadBindingChoice.Start;
            case "<Gamepad>/select": return GamepadBindingChoice.Select;
            default: return GamepadBindingChoice.Custom;
        }
    }
}
