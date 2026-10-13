using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Controla o menu principal e a tela de opções (estilo GTA San Andreas).
/// Navegação por teclado/controle (setas, Enter, Esc) e mouse.
/// Requer um EventSystem na cena (o do Input System ou o padrão).
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GtaMainMenuController : MonoBehaviour
{
    [Header("Áudio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip moveClip;      // navegar entre itens
    [SerializeField] private AudioClip confirmClip;   // clicar / confirmar
    [SerializeField] private AudioClip backClip;      // voltar
    [Tooltip("Opcional. Expor os parâmetros MusicVolume e SfxVolume (dB).")]
    [SerializeField] private AudioMixer mixer;

    [Header("Fluxo")]
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private float confirmDelay = 0.35f;

    // Chaves salvas em PlayerPrefs (leia no jogo)
    public const string KeyMusic = "gta_music";
    public const string KeySfx = "gta_sfx";
    public const string KeyBrightness = "gta_brightness";
    public const string KeySubtitles = "gta_subtitles";

    private VisualElement _root, _mainScreen, _optionsScreen, _overlay;
    private bool _silentFocus = true;

    private void Start()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;
        _mainScreen = _root.Q("main-screen");
        _optionsScreen = _root.Q("options-screen");
        _overlay = _root.Q("brightness-overlay");

        // Menu principal
        BindButton("btn-new-game", OnNewGame);
        BindButton("btn-load-game", OnLoadGame);
        BindButton("btn-options", () => ShowScreen(_optionsScreen, "row-music"));
        BindButton("btn-quit", OnQuit);

        // Opções
        BindSlider("row-music", KeyMusic, 7, ApplyMusic);
        BindSlider("row-sfx", KeySfx, 7, ApplySfx);
        BindSlider("row-brightness", KeyBrightness, 10, ApplyBrightness);
        BindToggle("row-subtitles", "subtitles-value", KeySubtitles, true, null);
        BindButton("btn-back", GoBack, playConfirm: false);

        // Esc / botão B do controle
        _root.RegisterCallback<NavigationCancelEvent>(_ => GoBack());

        FocusSilently(_root.Q("btn-new-game"));
    }

    // ---------------------------------------------------------------- Telas

    private void ShowScreen(VisualElement target, string focusName)
    {
        _mainScreen.AddToClassList("hidden");
        _optionsScreen.AddToClassList("hidden");
        target.RemoveFromClassList("hidden");
        FocusSilently(_root.Q(focusName));
    }

    private void GoBack()
    {
        if (_optionsScreen.ClassListContains("hidden")) return;

        Play(backClip);
        PlayerPrefs.Save();
        ShowScreen(_mainScreen, "btn-options");
    }

    // ---------------------------------------------------------------- Ações

    private void OnNewGame()
    {
        _root.SetEnabled(false);
        Invoke(nameof(LoadGameScene), confirmDelay);
    }

    private void LoadGameScene() => SceneManager.LoadScene(gameSceneName);

    private void OnLoadGame()
    {
        // TODO: implemente seu sistema de save/load aqui.
        Debug.Log("Carregar Jogo: ainda não implementado.");
    }

    private void OnQuit()
    {
        _root.SetEnabled(false);
        Invoke(nameof(QuitGame), confirmDelay);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------------------------------------------------------------- Bindings

    private void BindButton(string name, Action action, bool playConfirm = true)
    {
        var button = _root.Q<Button>(name);
        RegisterFeedback(button);
        button.clicked += () =>
        {
            if (playConfirm) Play(confirmClip);
            action();
        };
    }

    private void BindSlider(string rowName, string prefsKey, int defaultValue, Action<int> onChanged)
    {
        var row = _root.Q(rowName);
        var fill = row.Q(className: "bar-fill");
        int value = PlayerPrefs.GetInt(prefsKey, defaultValue);

        void SetValue(int v, bool playSound)
        {
            v = Mathf.Clamp(v, 0, 10);
            bool changed = v != value;
            value = v;
            fill.style.width = Length.Percent(v * 10);
            PlayerPrefs.SetInt(prefsKey, v);
            onChanged?.Invoke(v);
            if (playSound && changed) Play(moveClip);
        }

        SetValue(value, false);

        // Esquerda/direita ajustam o valor; cima/baixo continuam navegando
        row.RegisterCallback<NavigationMoveEvent>(evt =>
        {
            if (evt.direction == NavigationMoveEvent.Direction.Left) SetValue(value - 1, true);
            else if (evt.direction == NavigationMoveEvent.Direction.Right) SetValue(value + 1, true);
            else return;

            evt.PreventDefault();
            evt.StopPropagation();
        });

        row.Q<Button>(className: "arrow-left").clicked += () => SetValue(value - 1, true);
        row.Q<Button>(className: "arrow-right").clicked += () => SetValue(value + 1, true);

        RegisterFeedback(row);
    }

    private void BindToggle(string rowName, string valueLabelName, string prefsKey, bool defaultValue, Action<bool> onChanged)
    {
        var row = _root.Q(rowName);
        var label = _root.Q<Label>(valueLabelName);
        bool on = PlayerPrefs.GetInt(prefsKey, defaultValue ? 1 : 0) == 1;

        void Refresh()
        {
            label.text = on ? "LIGADO" : "DESLIGADO";
            PlayerPrefs.SetInt(prefsKey, on ? 1 : 0);
            onChanged?.Invoke(on);
        }

        void Toggle()
        {
            on = !on;
            Refresh();
            Play(confirmClip);
        }

        Refresh();

        row.RegisterCallback<ClickEvent>(_ => Toggle());
        row.RegisterCallback<NavigationSubmitEvent>(evt =>
        {
            Toggle();
            evt.StopPropagation();
        });
        row.RegisterCallback<NavigationMoveEvent>(evt =>
        {
            if (evt.direction != NavigationMoveEvent.Direction.Left &&
                evt.direction != NavigationMoveEvent.Direction.Right) return;

            Toggle();
            evt.PreventDefault();
            evt.StopPropagation();
        });

        RegisterFeedback(row);
    }

    // ---------------------------------------------------------------- Aplicar opções

    private void ApplyMusic(int step) => SetMixerVolume("MusicVolume", step);

    private void ApplySfx(int step)
    {
        if (mixer != null) SetMixerVolume("SfxVolume", step);
        else if (sfxSource != null) sfxSource.volume = step / 10f;
    }

    private void ApplyBrightness(int step)
    {
        // 10 = tela normal; valores menores escurecem
        _overlay.style.opacity = (10 - step) / 10f * 0.6f;
    }

    private void SetMixerVolume(string parameter, int step)
    {
        if (mixer == null) return;
        mixer.SetFloat(parameter, step <= 0 ? -80f : Mathf.Log10(step / 10f) * 20f);
    }

    // ---------------------------------------------------------------- Som e foco

    private void RegisterFeedback(VisualElement element)
    {
        element.RegisterCallback<FocusInEvent>(_ =>
        {
            if (!_silentFocus) Play(moveClip);
        });
        element.RegisterCallback<PointerEnterEvent>(_ => element.Focus());
    }

    // Foca um elemento sem tocar o som de navegação
    private void FocusSilently(VisualElement element)
    {
        _silentFocus = true;
        _root.schedule.Execute(() =>
        {
            element.Focus();
            _root.schedule.Execute(() => _silentFocus = false).StartingIn(50);
        }).StartingIn(10);
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip);
    }
}
