using System;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[Serializable]
public class HudWeapon
{
    public string displayName = "Arma";
    public Sprite icon;
    public bool usesAmmo = true;
    public int clipSize = 17;
    public int startReserve = 100;
    public int maxReserve = 999;

    [NonSerialized] public int clip;
    [NonSerialized] public int reserve;
}

/// <summary>
/// HUD de gameplay estilo GTA San Andreas: vida, dinheiro, arma, munição e minimapa.
/// Os valores podem ser alterados pelo teclado (para teste) ou pela API pública.
///
/// Teclas:  J/K = vida -/+   M/N = dinheiro +/-   ESPAÇO = atirar
///          R = recarregar   Q/E = arma anterior/próxima
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GtaHudController : MonoBehaviour
{
    [Header("Jogador")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int startMoney = 350;
    [SerializeField] private int healthStep = 10;
    [SerializeField] private int moneyStep = 100;

    [Header("Armas")]
    [SerializeField] private HudWeapon[] weapons;

    [Header("Minimapa")]
    [Tooltip("Opcional. Pode ser definido depois com SetMinimap().")]
    [SerializeField] private Texture2D minimapTexture;

    [Header("Debug")]
    [SerializeField] private bool showKeyHint = true;

    private VisualElement _healthFill, _weaponIcon, _minimap;
    private Label _moneyLabel, _ammoLabel, _weaponName, _hint;

    private int _health;
    private int _money;
    private int _weaponIndex;

    private HudWeapon CurrentWeapon => weapons[_weaponIndex];

    // Valores padrão ao adicionar o componente
    private void Reset()
    {
        weapons = new[]
        {
            new HudWeapon { displayName = "PUNHOS", usesAmmo = false },
            new HudWeapon { displayName = "PISTOLA", clipSize = 17, startReserve = 102, maxReserve = 999 },
            new HudWeapon { displayName = "AK-47", clipSize = 30, startReserve = 150, maxReserve = 999 },
        };
    }

    private void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _healthFill = root.Q("health-fill");
        _moneyLabel = root.Q<Label>("money-label");
        _ammoLabel = root.Q<Label>("ammo-label");
        _weaponIcon = root.Q("weapon-icon");
        _weaponName = root.Q<Label>("weapon-name");
        _minimap = root.Q("minimap");
        _hint = root.Q<Label>("debug-hint");

        if (weapons == null || weapons.Length == 0) Reset();
        foreach (var w in weapons)
        {
            w.clip = w.usesAmmo ? w.clipSize : 0;
            w.reserve = w.usesAmmo ? Mathf.Min(w.startReserve, w.maxReserve) : 0;
        }

        _health = maxHealth;
        _money = startMoney;
        _weaponIndex = 0;

        _hint.style.display = showKeyHint ? DisplayStyle.Flex : DisplayStyle.None;

        SetMinimap(minimapTexture);
        RefreshHealth();
        RefreshMoney();
        RefreshWeapon();
    }

    private void Update()
    {
        if (Pressed(HudKey.Damage)) AddHealth(-healthStep);
        if (Pressed(HudKey.Heal)) AddHealth(healthStep);
        if (Pressed(HudKey.MoneyUp)) AddMoney(moneyStep);
        if (Pressed(HudKey.MoneyDown)) AddMoney(-moneyStep);
        if (Pressed(HudKey.Fire)) Fire();
        if (Pressed(HudKey.Reload)) Reload();
        if (Pressed(HudKey.PrevWeapon)) ChangeWeapon(-1);
        if (Pressed(HudKey.NextWeapon)) ChangeWeapon(1);
    }

    // ---------------------------------------------------------------- API pública

    public void SetHealth(int value)
    {
        _health = Mathf.Clamp(value, 0, maxHealth);
        RefreshHealth();
    }

    public void AddHealth(int delta) => SetHealth(_health + delta);

    public void SetMoney(int value)
    {
        _money = Mathf.Max(0, value);
        RefreshMoney();
    }

    public void AddMoney(int delta) => SetMoney(_money + delta);

    public void Fire()
    {
        var w = CurrentWeapon;
        if (!w.usesAmmo || w.clip <= 0) return;

        w.clip--;
        RefreshAmmo();
    }

    public void Reload()
    {
        var w = CurrentWeapon;
        if (!w.usesAmmo) return;

        int needed = w.clipSize - w.clip;
        int taken = Mathf.Min(needed, w.reserve);
        w.clip += taken;
        w.reserve -= taken;
        RefreshAmmo();
    }

    public void ChangeWeapon(int direction)
    {
        _weaponIndex = (_weaponIndex + direction + weapons.Length) % weapons.Length;
        RefreshWeapon();
    }

    public void SetMinimap(Texture2D texture)
    {
        _minimap.style.backgroundImage = texture != null
            ? new StyleBackground(texture)
            : new StyleBackground(StyleKeyword.None);
    }

    // ---------------------------------------------------------------- Atualização da UI

    private void RefreshHealth()
    {
        _healthFill.style.width = Length.Percent(100f * _health / maxHealth);
    }

    private void RefreshMoney()
    {
        _moneyLabel.text = "$" + _money;
    }

    private void RefreshWeapon()
    {
        var w = CurrentWeapon;

        _weaponIcon.style.backgroundImage = w.icon != null
            ? new StyleBackground(w.icon)
            : new StyleBackground(StyleKeyword.None);

        // Mostra o nome só enquanto a arma não tiver imagem
        _weaponName.text = w.displayName;
        _weaponName.style.display = w.icon == null ? DisplayStyle.Flex : DisplayStyle.None;

        RefreshAmmo();
    }

    // Formato do GTA SA: total na reserva - balas no carregador (ex.: 85-17)
    private void RefreshAmmo()
    {
        var w = CurrentWeapon;
        _ammoLabel.text = w.usesAmmo ? $"{w.reserve}-{w.clip}" : string.Empty;
    }

    // ---------------------------------------------------------------- Teclado

    private enum HudKey { Damage, Heal, MoneyUp, MoneyDown, Fire, Reload, PrevWeapon, NextWeapon }

    private static bool Pressed(HudKey key)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;

        switch (key)
        {
            case HudKey.Damage: return kb.jKey.wasPressedThisFrame;
            case HudKey.Heal: return kb.kKey.wasPressedThisFrame;
            case HudKey.MoneyUp: return kb.mKey.wasPressedThisFrame;
            case HudKey.MoneyDown: return kb.nKey.wasPressedThisFrame;
            case HudKey.Fire: return kb.spaceKey.wasPressedThisFrame;
            case HudKey.Reload: return kb.rKey.wasPressedThisFrame;
            case HudKey.PrevWeapon: return kb.qKey.wasPressedThisFrame;
            case HudKey.NextWeapon: return kb.eKey.wasPressedThisFrame;
        }
        return false;
#else
        switch (key)
        {
            case HudKey.Damage: return Input.GetKeyDown(KeyCode.J);
            case HudKey.Heal: return Input.GetKeyDown(KeyCode.K);
            case HudKey.MoneyUp: return Input.GetKeyDown(KeyCode.M);
            case HudKey.MoneyDown: return Input.GetKeyDown(KeyCode.N);
            case HudKey.Fire: return Input.GetKeyDown(KeyCode.Space);
            case HudKey.Reload: return Input.GetKeyDown(KeyCode.R);
            case HudKey.PrevWeapon: return Input.GetKeyDown(KeyCode.Q);
            case HudKey.NextWeapon: return Input.GetKeyDown(KeyCode.E);
        }
        return false;
#endif
    }
}
