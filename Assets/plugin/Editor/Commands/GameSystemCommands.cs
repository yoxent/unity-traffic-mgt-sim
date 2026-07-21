using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMcpPro
{
    public class GameSystemCommands : BaseCommand
    {
        public static void Register(CommandRouter router)
        {
            router.Register("create_health_system", CreateHealthSystem);
            router.Register("create_inventory_system", CreateInventorySystem);
            router.Register("create_spawn_system", CreateSpawnSystem);
            router.Register("create_dialogue_system", CreateDialogueSystem);
            router.Register("create_interaction_system", CreateInteractionSystem);
        }

        #region create_health_system

        private static object CreateHealthSystem(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_health_system");

            string targetPath = GetStringParam(p, "target");
            float maxHealth = GetFloatParam(p, "max_health", 100f);
            bool hasShield = GetBoolParam(p, "has_shield", false);
            bool regeneration = GetBoolParam(p, "regeneration", false);
            float regenRate = GetFloatParam(p, "regen_rate", 5f);
            string scriptPath = GetStringParam(p, "script_path", "Assets/Scripts/Systems/HealthSystem.cs");

            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentException("'target' is required");

            var go = FindGameObject(targetPath);

            if (!scriptPath.StartsWith("Assets/"))
                scriptPath = "Assets/" + scriptPath;
            if (!scriptPath.EndsWith(".cs"))
                scriptPath += ".cs";

            string content = GenerateHealthSystemScript(maxHealth, hasShield, regeneration, regenRate);
            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), scriptPath);

            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, content);
            AssetDatabase.Refresh();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "message", $"Health system created and attached to '{go.name}'" },
                { "gameObject", GetGameObjectPath(go) },
                { "script_path", scriptPath },
                { "max_health", maxHealth },
                { "has_shield", hasShield },
                { "regeneration", regeneration }
            };
        }

        private static string GenerateHealthSystemScript(float maxHealth, bool hasShield, bool regeneration, float regenRate)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.Events;");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Health and damage system with events, optional shield, and regeneration.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("public class HealthSystem : MonoBehaviour");
            sb.AppendLine("{");
            sb.AppendLine("    #region Serialized Fields");
            sb.AppendLine();
            sb.AppendLine("    [Header(\"Health\")]");
            sb.AppendLine($"    [SerializeField] private float maxHealth = {maxHealth}f;");
            sb.AppendLine("    [SerializeField] private float currentHealth;");
            sb.AppendLine();

            if (hasShield)
            {
                sb.AppendLine("    [Header(\"Shield\")]");
                sb.AppendLine("    [SerializeField] private float maxShield = 50f;");
                sb.AppendLine("    [SerializeField] private float currentShield;");
                sb.AppendLine("    [SerializeField] private float shieldRechargeDelay = 3f;");
                sb.AppendLine("    [SerializeField] private float shieldRechargeRate = 10f;");
                sb.AppendLine();
            }

            if (regeneration)
            {
                sb.AppendLine("    [Header(\"Regeneration\")]");
                sb.AppendLine("    [SerializeField] private bool enableRegeneration = true;");
                sb.AppendLine($"    [SerializeField] private float regenerationRate = {regenRate}f;");
                sb.AppendLine("    [SerializeField] private float regenDelay = 5f;");
                sb.AppendLine();
            }

            sb.AppendLine("    [Header(\"Events\")]");
            sb.AppendLine("    [SerializeField] private UnityEvent<float> onHealthChanged;");
            sb.AppendLine("    [SerializeField] private UnityEvent<float> onDamageTaken;");
            sb.AppendLine("    [SerializeField] private UnityEvent<float> onHealed;");
            sb.AppendLine("    [SerializeField] private UnityEvent onDeath;");

            if (hasShield)
            {
                sb.AppendLine("    [SerializeField] private UnityEvent<float> onShieldChanged;");
                sb.AppendLine("    [SerializeField] private UnityEvent onShieldBroken;");
            }

            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Private Fields");
            sb.AppendLine();
            sb.AppendLine("    private bool _isDead = false;");
            sb.AppendLine("    private float _lastDamageTime;");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Properties");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Current health value.</summary>");
            sb.AppendLine("    public float CurrentHealth => currentHealth;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Maximum health value.</summary>");
            sb.AppendLine("    public float MaxHealth => maxHealth;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Health as a normalized value (0-1).</summary>");
            sb.AppendLine("    public float HealthNormalized => maxHealth > 0 ? currentHealth / maxHealth : 0f;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Whether the entity is dead.</summary>");
            sb.AppendLine("    public bool IsDead => _isDead;");

            if (hasShield)
            {
                sb.AppendLine();
                sb.AppendLine("    /// <summary>Current shield value.</summary>");
                sb.AppendLine("    public float CurrentShield => currentShield;");
                sb.AppendLine();
                sb.AppendLine("    /// <summary>Maximum shield value.</summary>");
                sb.AppendLine("    public float MaxShield => maxShield;");
                sb.AppendLine();
                sb.AppendLine("    /// <summary>Shield as a normalized value (0-1).</summary>");
                sb.AppendLine("    public float ShieldNormalized => maxShield > 0 ? currentShield / maxShield : 0f;");
            }

            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Unity Callbacks");
            sb.AppendLine();
            sb.AppendLine("    private void Awake()");
            sb.AppendLine("    {");
            sb.AppendLine("        currentHealth = maxHealth;");
            if (hasShield)
                sb.AppendLine("        currentShield = maxShield;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private void Update()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_isDead) return;");

            if (regeneration)
            {
                sb.AppendLine();
                sb.AppendLine("        // Health regeneration");
                sb.AppendLine("        if (enableRegeneration && currentHealth < maxHealth && Time.time - _lastDamageTime >= regenDelay)");
                sb.AppendLine("        {");
                sb.AppendLine("            float healAmount = regenerationRate * Time.deltaTime;");
                sb.AppendLine("            currentHealth = Mathf.Min(currentHealth + healAmount, maxHealth);");
                sb.AppendLine("            onHealthChanged?.Invoke(currentHealth);");
                sb.AppendLine("        }");
            }

            if (hasShield)
            {
                sb.AppendLine();
                sb.AppendLine("        // Shield recharge");
                sb.AppendLine("        if (currentShield < maxShield && Time.time - _lastDamageTime >= shieldRechargeDelay)");
                sb.AppendLine("        {");
                sb.AppendLine("            currentShield = Mathf.Min(currentShield + shieldRechargeRate * Time.deltaTime, maxShield);");
                sb.AppendLine("            onShieldChanged?.Invoke(currentShield);");
                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Public Methods");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Apply damage to this entity. Damage is applied to shield first (if present), then health.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <param name=\"amount\">Amount of damage to apply.</param>");
            sb.AppendLine("    public void TakeDamage(float amount)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_isDead || amount <= 0) return;");
            sb.AppendLine();
            sb.AppendLine("        _lastDamageTime = Time.time;");
            sb.AppendLine("        float remainingDamage = amount;");

            if (hasShield)
            {
                sb.AppendLine();
                sb.AppendLine("        // Absorb damage with shield first");
                sb.AppendLine("        if (currentShield > 0)");
                sb.AppendLine("        {");
                sb.AppendLine("            float shieldDamage = Mathf.Min(currentShield, remainingDamage);");
                sb.AppendLine("            currentShield -= shieldDamage;");
                sb.AppendLine("            remainingDamage -= shieldDamage;");
                sb.AppendLine("            onShieldChanged?.Invoke(currentShield);");
                sb.AppendLine();
                sb.AppendLine("            if (currentShield <= 0)");
                sb.AppendLine("                onShieldBroken?.Invoke();");
                sb.AppendLine("        }");
            }

            sb.AppendLine();
            sb.AppendLine("        if (remainingDamage > 0)");
            sb.AppendLine("        {");
            sb.AppendLine("            currentHealth = Mathf.Max(currentHealth - remainingDamage, 0f);");
            sb.AppendLine("            onHealthChanged?.Invoke(currentHealth);");
            sb.AppendLine("            onDamageTaken?.Invoke(amount);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        if (currentHealth <= 0)");
            sb.AppendLine("            Die();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Heal this entity by the specified amount.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <param name=\"amount\">Amount of health to restore.</param>");
            sb.AppendLine("    public void Heal(float amount)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_isDead || amount <= 0) return;");
            sb.AppendLine();
            sb.AppendLine("        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);");
            sb.AppendLine("        onHealthChanged?.Invoke(currentHealth);");
            sb.AppendLine("        onHealed?.Invoke(amount);");
            sb.AppendLine("    }");

            if (hasShield)
            {
                sb.AppendLine();
                sb.AppendLine("    /// <summary>");
                sb.AppendLine("    /// Restore shield by the specified amount.");
                sb.AppendLine("    /// </summary>");
                sb.AppendLine("    /// <param name=\"amount\">Amount of shield to restore.</param>");
                sb.AppendLine("    public void RestoreShield(float amount)");
                sb.AppendLine("    {");
                sb.AppendLine("        if (amount <= 0) return;");
                sb.AppendLine("        currentShield = Mathf.Min(currentShield + amount, maxShield);");
                sb.AppendLine("        onShieldChanged?.Invoke(currentShield);");
                sb.AppendLine("    }");
            }

            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Reset health to maximum and revive if dead.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public void ResetHealth()");
            sb.AppendLine("    {");
            sb.AppendLine("        _isDead = false;");
            sb.AppendLine("        currentHealth = maxHealth;");
            if (hasShield)
                sb.AppendLine("        currentShield = maxShield;");
            sb.AppendLine("        onHealthChanged?.Invoke(currentHealth);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Set max health. Optionally scales current health proportionally.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public void SetMaxHealth(float newMax, bool scaleCurrentHealth = false)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (scaleCurrentHealth && maxHealth > 0)");
            sb.AppendLine("            currentHealth = (currentHealth / maxHealth) * newMax;");
            sb.AppendLine("        maxHealth = newMax;");
            sb.AppendLine("        currentHealth = Mathf.Min(currentHealth, maxHealth);");
            sb.AppendLine("        onHealthChanged?.Invoke(currentHealth);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Private Methods");
            sb.AppendLine();
            sb.AppendLine("    private void Die()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_isDead) return;");
            sb.AppendLine("        _isDead = true;");
            sb.AppendLine("        onDeath?.Invoke();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine("}");

            return sb.ToString();
        }

        #endregion

        #region create_inventory_system

        private static object CreateInventorySystem(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_inventory_system");

            int maxSlots = GetIntParam(p, "max_slots", 20);
            bool stackable = GetBoolParam(p, "stackable", true);
            string scriptPath = GetStringParam(p, "script_path", "Assets/Scripts/Systems/InventorySystem.cs");

            if (!scriptPath.StartsWith("Assets/"))
                scriptPath = "Assets/" + scriptPath;
            if (!scriptPath.EndsWith(".cs"))
                scriptPath += ".cs";

            // Generate the combined script
            string content = GenerateInventorySystemScript(maxSlots, stackable);
            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), scriptPath);

            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, content);

            // Also generate the ItemData ScriptableObject
            string itemDataPath = Path.Combine(Path.GetDirectoryName(scriptPath), "ItemData.cs");
            string itemDataContent = GenerateItemDataScript(stackable);
            string itemDataFullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), itemDataPath);
            File.WriteAllText(itemDataFullPath, itemDataContent);

            AssetDatabase.Refresh();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "message", $"Inventory system created" },
                { "scripts", new List<object> { scriptPath, itemDataPath } },
                { "max_slots", maxSlots },
                { "stackable", stackable }
            };
        }

        private static string GenerateItemDataScript(bool stackable)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// ScriptableObject defining an item's data and properties.");
            sb.AppendLine("/// Create instances via Assets > Create > Game > Item Data.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("[CreateAssetMenu(fileName = \"NewItem\", menuName = \"Game/Item Data\")]");
            sb.AppendLine("public class ItemData : ScriptableObject");
            sb.AppendLine("{");
            sb.AppendLine("    #region Item Type Enum");
            sb.AppendLine();
            sb.AppendLine("    public enum ItemType");
            sb.AppendLine("    {");
            sb.AppendLine("        Consumable,");
            sb.AppendLine("        Equipment,");
            sb.AppendLine("        Weapon,");
            sb.AppendLine("        Material,");
            sb.AppendLine("        Quest,");
            sb.AppendLine("        Miscellaneous");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Fields");
            sb.AppendLine();
            sb.AppendLine("    [Header(\"Basic Info\")]");
            sb.AppendLine("    [SerializeField] private string itemName = \"New Item\";");
            sb.AppendLine("    [SerializeField, TextArea(2, 5)] private string description = \"\";");
            sb.AppendLine("    [SerializeField] private Sprite icon;");
            sb.AppendLine("    [SerializeField] private ItemType itemType = ItemType.Miscellaneous;");
            sb.AppendLine();
            sb.AppendLine("    [Header(\"Stacking\")]");
            sb.AppendLine($"    [SerializeField] private bool isStackable = {(stackable ? "true" : "false")};");
            sb.AppendLine("    [SerializeField] private int maxStackSize = 99;");
            sb.AppendLine();
            sb.AppendLine("    [Header(\"Value\")]");
            sb.AppendLine("    [SerializeField] private int buyPrice = 0;");
            sb.AppendLine("    [SerializeField] private int sellPrice = 0;");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Properties");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Display name of the item.</summary>");
            sb.AppendLine("    public string ItemName => itemName;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Item description text.</summary>");
            sb.AppendLine("    public string Description => description;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Icon sprite for UI display.</summary>");
            sb.AppendLine("    public Sprite Icon => icon;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Category of the item.</summary>");
            sb.AppendLine("    public ItemType Type => itemType;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Whether this item can be stacked.</summary>");
            sb.AppendLine("    public bool IsStackable => isStackable;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Maximum number of items per stack.</summary>");
            sb.AppendLine("    public int MaxStackSize => isStackable ? maxStackSize : 1;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Buy price.</summary>");
            sb.AppendLine("    public int BuyPrice => buyPrice;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Sell price.</summary>");
            sb.AppendLine("    public int SellPrice => sellPrice;");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateInventorySystemScript(int maxSlots, bool stackable)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.Events;");
            sb.AppendLine();
            sb.AppendLine("#region Inventory Slot");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Represents a single slot in the inventory.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("[Serializable]");
            sb.AppendLine("public class InventorySlot");
            sb.AppendLine("{");
            sb.AppendLine("    [SerializeField] private ItemData item;");
            sb.AppendLine("    [SerializeField] private int quantity;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>The item in this slot (null if empty).</summary>");
            sb.AppendLine("    public ItemData Item => item;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Number of items in this slot.</summary>");
            sb.AppendLine("    public int Quantity => quantity;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Whether this slot is empty.</summary>");
            sb.AppendLine("    public bool IsEmpty => item == null || quantity <= 0;");
            sb.AppendLine();
            sb.AppendLine("    public InventorySlot()");
            sb.AppendLine("    {");
            sb.AppendLine("        item = null;");
            sb.AppendLine("        quantity = 0;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public InventorySlot(ItemData item, int quantity)");
            sb.AppendLine("    {");
            sb.AppendLine("        this.item = item;");
            sb.AppendLine("        this.quantity = quantity;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Set the slot contents.</summary>");
            sb.AppendLine("    public void Set(ItemData newItem, int newQuantity)");
            sb.AppendLine("    {");
            sb.AppendLine("        item = newItem;");
            sb.AppendLine("        quantity = newQuantity;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Add to the quantity in this slot.</summary>");
            sb.AppendLine("    public void AddQuantity(int amount) => quantity += amount;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Remove from the quantity in this slot.</summary>");
            sb.AppendLine("    public void RemoveQuantity(int amount)");
            sb.AppendLine("    {");
            sb.AppendLine("        quantity -= amount;");
            sb.AppendLine("        if (quantity <= 0) Clear();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Clear this slot.</summary>");
            sb.AppendLine("    public void Clear()");
            sb.AppendLine("    {");
            sb.AppendLine("        item = null;");
            sb.AppendLine("        quantity = 0;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("#endregion");
            sb.AppendLine();
            sb.AppendLine("#region Inventory System");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Manages an inventory with slots, stacking, and item operations.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("public class InventorySystem : MonoBehaviour");
            sb.AppendLine("{");
            sb.AppendLine("    #region Serialized Fields");
            sb.AppendLine();
            sb.AppendLine($"    [SerializeField] private int maxSlots = {maxSlots};");
            sb.AppendLine("    [SerializeField] private List<InventorySlot> slots = new List<InventorySlot>();");
            sb.AppendLine();
            sb.AppendLine("    [Header(\"Events\")]");
            sb.AppendLine("    [SerializeField] private UnityEvent onInventoryChanged;");
            sb.AppendLine("    [SerializeField] private UnityEvent<ItemData, int> onItemAdded;");
            sb.AppendLine("    [SerializeField] private UnityEvent<ItemData, int> onItemRemoved;");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Properties");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Maximum number of inventory slots.</summary>");
            sb.AppendLine("    public int MaxSlots => maxSlots;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Read-only access to inventory slots.</summary>");
            sb.AppendLine("    public IReadOnlyList<InventorySlot> Slots => slots;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Number of occupied slots.</summary>");
            sb.AppendLine("    public int OccupiedSlots");
            sb.AppendLine("    {");
            sb.AppendLine("        get");
            sb.AppendLine("        {");
            sb.AppendLine("            int count = 0;");
            sb.AppendLine("            foreach (var slot in slots)");
            sb.AppendLine("                if (!slot.IsEmpty) count++;");
            sb.AppendLine("            return count;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Whether the inventory is full.</summary>");
            sb.AppendLine("    public bool IsFull => OccupiedSlots >= maxSlots;");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Unity Callbacks");
            sb.AppendLine();
            sb.AppendLine("    private void Awake()");
            sb.AppendLine("    {");
            sb.AppendLine("        InitializeSlots();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Public Methods");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Add an item to the inventory. Returns the number of items that could not be added.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <param name=\"item\">The item to add.</param>");
            sb.AppendLine("    /// <param name=\"quantity\">Number of items to add.</param>");
            sb.AppendLine("    /// <returns>Number of items that could not be added (0 if all added).</returns>");
            sb.AppendLine("    public int AddItem(ItemData item, int quantity = 1)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (item == null || quantity <= 0) return quantity;");
            sb.AppendLine();
            sb.AppendLine("        int remaining = quantity;");
            sb.AppendLine();

            if (stackable)
            {
                sb.AppendLine("        // Try stacking with existing slots first");
                sb.AppendLine("        if (item.IsStackable)");
                sb.AppendLine("        {");
                sb.AppendLine("            for (int i = 0; i < slots.Count && remaining > 0; i++)");
                sb.AppendLine("            {");
                sb.AppendLine("                if (slots[i].Item == item && slots[i].Quantity < item.MaxStackSize)");
                sb.AppendLine("                {");
                sb.AppendLine("                    int spaceInStack = item.MaxStackSize - slots[i].Quantity;");
                sb.AppendLine("                    int toAdd = Mathf.Min(remaining, spaceInStack);");
                sb.AppendLine("                    slots[i].AddQuantity(toAdd);");
                sb.AppendLine("                    remaining -= toAdd;");
                sb.AppendLine("                }");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine("        // Place remaining in empty slots");
            sb.AppendLine("        for (int i = 0; i < slots.Count && remaining > 0; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (slots[i].IsEmpty)");
            sb.AppendLine("            {");
            sb.AppendLine("                int toAdd = item.IsStackable ? Mathf.Min(remaining, item.MaxStackSize) : 1;");
            sb.AppendLine("                slots[i].Set(item, toAdd);");
            sb.AppendLine("                remaining -= toAdd;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        int added = quantity - remaining;");
            sb.AppendLine("        if (added > 0)");
            sb.AppendLine("        {");
            sb.AppendLine("            onItemAdded?.Invoke(item, added);");
            sb.AppendLine("            onInventoryChanged?.Invoke();");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        return remaining;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Remove an item from the inventory.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <param name=\"item\">The item to remove.</param>");
            sb.AppendLine("    /// <param name=\"quantity\">Number to remove.</param>");
            sb.AppendLine("    /// <returns>True if the full quantity was removed.</returns>");
            sb.AppendLine("    public bool RemoveItem(ItemData item, int quantity = 1)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (item == null || quantity <= 0) return false;");
            sb.AppendLine("        if (GetItemCount(item) < quantity) return false;");
            sb.AppendLine();
            sb.AppendLine("        int remaining = quantity;");
            sb.AppendLine("        for (int i = slots.Count - 1; i >= 0 && remaining > 0; i--)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (slots[i].Item == item)");
            sb.AppendLine("            {");
            sb.AppendLine("                int toRemove = Mathf.Min(remaining, slots[i].Quantity);");
            sb.AppendLine("                slots[i].RemoveQuantity(toRemove);");
            sb.AppendLine("                remaining -= toRemove;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        onItemRemoved?.Invoke(item, quantity);");
            sb.AppendLine("        onInventoryChanged?.Invoke();");
            sb.AppendLine("        return true;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Get the total count of a specific item across all slots.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public int GetItemCount(ItemData item)");
            sb.AppendLine("    {");
            sb.AppendLine("        int count = 0;");
            sb.AppendLine("        foreach (var slot in slots)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (slot.Item == item)");
            sb.AppendLine("                count += slot.Quantity;");
            sb.AppendLine("        }");
            sb.AppendLine("        return count;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Check whether the inventory contains at least the given quantity of an item.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public bool HasItem(ItemData item, int quantity = 1)");
            sb.AppendLine("    {");
            sb.AppendLine("        return GetItemCount(item) >= quantity;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Clear all items from the inventory.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public void ClearAll()");
            sb.AppendLine("    {");
            sb.AppendLine("        foreach (var slot in slots)");
            sb.AppendLine("            slot.Clear();");
            sb.AppendLine("        onInventoryChanged?.Invoke();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Private Methods");
            sb.AppendLine();
            sb.AppendLine("    private void InitializeSlots()");
            sb.AppendLine("    {");
            sb.AppendLine("        while (slots.Count < maxSlots)");
            sb.AppendLine("            slots.Add(new InventorySlot());");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("#endregion");

            return sb.ToString();
        }

        #endregion

        #region create_spawn_system

        private static object CreateSpawnSystem(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_spawn_system");

            string targetPath = GetStringParam(p, "target");
            string prefabPath = GetStringParam(p, "prefab_path");
            int poolSize = GetIntParam(p, "pool_size", 10);
            float spawnRate = GetFloatParam(p, "spawn_rate", 1f);
            string spawnArea = GetStringParam(p, "spawn_area", "10,0,10");
            string scriptPath = GetStringParam(p, "script_path", "Assets/Scripts/Systems/SpawnSystem.cs");

            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentException("'target' is required");

            var go = FindGameObject(targetPath);

            if (!scriptPath.StartsWith("Assets/"))
                scriptPath = "Assets/" + scriptPath;
            if (!scriptPath.EndsWith(".cs"))
                scriptPath += ".cs";

            string content = GenerateSpawnSystemScript(poolSize, spawnRate, spawnArea);
            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), scriptPath);

            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, content);
            AssetDatabase.Refresh();

            var result = new Dictionary<string, object>
            {
                { "success", true },
                { "message", $"Spawn system created for '{go.name}'" },
                { "gameObject", GetGameObjectPath(go) },
                { "script_path", scriptPath },
                { "pool_size", poolSize },
                { "spawn_rate", spawnRate },
                { "spawn_area", spawnArea }
            };

            if (!string.IsNullOrEmpty(prefabPath))
                result["prefab_path"] = prefabPath;

            return result;
        }

        private static string GenerateSpawnSystemScript(int poolSize, float spawnRate, string spawnArea)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.Events;");
            sb.AppendLine();
            sb.AppendLine("#region Object Pool");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Generic object pool for efficient instantiation and reuse of GameObjects.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("public class ObjectPool : MonoBehaviour");
            sb.AppendLine("{");
            sb.AppendLine("    #region Serialized Fields");
            sb.AppendLine();
            sb.AppendLine("    [SerializeField] private GameObject prefab;");
            sb.AppendLine($"    [SerializeField] private int initialSize = {poolSize};");
            sb.AppendLine("    [SerializeField] private bool expandable = true;");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Private Fields");
            sb.AppendLine();
            sb.AppendLine("    private Queue<GameObject> _pool = new Queue<GameObject>();");
            sb.AppendLine("    private List<GameObject> _activeObjects = new List<GameObject>();");
            sb.AppendLine("    private Transform _poolParent;");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Properties");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Number of objects currently available in the pool.</summary>");
            sb.AppendLine("    public int AvailableCount => _pool.Count;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Number of objects currently active.</summary>");
            sb.AppendLine("    public int ActiveCount => _activeObjects.Count;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>The prefab this pool manages.</summary>");
            sb.AppendLine("    public GameObject Prefab => prefab;");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Public Methods");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Initialize the pool with the specified prefab and size.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public void Initialize(GameObject poolPrefab, int size)");
            sb.AppendLine("    {");
            sb.AppendLine("        prefab = poolPrefab;");
            sb.AppendLine("        initialSize = size;");
            sb.AppendLine("        Initialize();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Initialize the pool using the assigned prefab and initial size.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public void Initialize()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (prefab == null)");
            sb.AppendLine("        {");
            sb.AppendLine("            Debug.LogError(\"ObjectPool: No prefab assigned.\");");
            sb.AppendLine("            return;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        _poolParent = new GameObject($\"{prefab.name}_Pool\").transform;");
            sb.AppendLine("        _poolParent.SetParent(transform);");
            sb.AppendLine();
            sb.AppendLine("        for (int i = 0; i < initialSize; i++)");
            sb.AppendLine("            CreatePooledObject();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Get an object from the pool.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <returns>An inactive GameObject ready for use, or null if pool is exhausted.</returns>");
            sb.AppendLine("    public GameObject Get()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_pool.Count == 0)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (expandable)");
            sb.AppendLine("                CreatePooledObject();");
            sb.AppendLine("            else");
            sb.AppendLine("                return null;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        var obj = _pool.Dequeue();");
            sb.AppendLine("        obj.SetActive(true);");
            sb.AppendLine("        _activeObjects.Add(obj);");
            sb.AppendLine("        return obj;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Return an object to the pool.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public void Return(GameObject obj)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (obj == null) return;");
            sb.AppendLine("        obj.SetActive(false);");
            sb.AppendLine("        obj.transform.SetParent(_poolParent);");
            sb.AppendLine("        _activeObjects.Remove(obj);");
            sb.AppendLine("        _pool.Enqueue(obj);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Return all active objects to the pool.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public void ReturnAll()");
            sb.AppendLine("    {");
            sb.AppendLine("        for (int i = _activeObjects.Count - 1; i >= 0; i--)");
            sb.AppendLine("            Return(_activeObjects[i]);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Private Methods");
            sb.AppendLine();
            sb.AppendLine("    private void CreatePooledObject()");
            sb.AppendLine("    {");
            sb.AppendLine("        var obj = Instantiate(prefab, _poolParent);");
            sb.AppendLine("        obj.SetActive(false);");
            sb.AppendLine("        _pool.Enqueue(obj);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("#endregion");
            sb.AppendLine();
            sb.AppendLine("#region Spawn System");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Configurable spawn system that uses ObjectPool for efficient object creation.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("public class SpawnSystem : MonoBehaviour");
            sb.AppendLine("{");
            sb.AppendLine("    #region Serialized Fields");
            sb.AppendLine();
            sb.AppendLine("    [Header(\"Spawn Settings\")]");
            sb.AppendLine("    [SerializeField] private GameObject prefab;");
            sb.AppendLine($"    [SerializeField] private float spawnRate = {spawnRate}f;");
            sb.AppendLine("    [SerializeField] private int maxActiveObjects = 50;");
            sb.AppendLine("    [SerializeField] private bool autoStart = true;");
            sb.AppendLine();
            sb.AppendLine("    [Header(\"Spawn Area\")]");

            // Parse the spawn area
            string[] areaParts = spawnArea.Split(',');
            string areaX = areaParts.Length > 0 ? areaParts[0].Trim() : "10";
            string areaY = areaParts.Length > 1 ? areaParts[1].Trim() : "0";
            string areaZ = areaParts.Length > 2 ? areaParts[2].Trim() : "10";

            sb.AppendLine($"    [SerializeField] private Vector3 spawnAreaSize = new Vector3({areaX}f, {areaY}f, {areaZ}f);");
            sb.AppendLine("    [SerializeField] private bool useSpawnPoints = false;");
            sb.AppendLine("    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();");
            sb.AppendLine();
            sb.AppendLine("    [Header(\"Pool Settings\")]");
            sb.AppendLine($"    [SerializeField] private int poolSize = {poolSize};");
            sb.AppendLine();
            sb.AppendLine("    [Header(\"Events\")]");
            sb.AppendLine("    [SerializeField] private UnityEvent<GameObject> onObjectSpawned;");
            sb.AppendLine("    [SerializeField] private UnityEvent<GameObject> onObjectDespawned;");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Private Fields");
            sb.AppendLine();
            sb.AppendLine("    private ObjectPool _pool;");
            sb.AppendLine("    private bool _isSpawning = false;");
            sb.AppendLine("    private Coroutine _spawnCoroutine;");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Properties");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Whether the spawner is currently active.</summary>");
            sb.AppendLine("    public bool IsSpawning => _isSpawning;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Number of currently active spawned objects.</summary>");
            sb.AppendLine("    public int ActiveCount => _pool != null ? _pool.ActiveCount : 0;");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Unity Callbacks");
            sb.AppendLine();
            sb.AppendLine("    private void Start()");
            sb.AppendLine("    {");
            sb.AppendLine("        _pool = gameObject.AddComponent<ObjectPool>();");
            sb.AppendLine("        _pool.Initialize(prefab, poolSize);");
            sb.AppendLine();
            sb.AppendLine("        if (autoStart)");
            sb.AppendLine("            StartSpawning();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Public Methods");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Begin spawning objects at the configured rate.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public void StartSpawning()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_isSpawning) return;");
            sb.AppendLine("        _isSpawning = true;");
            sb.AppendLine("        _spawnCoroutine = StartCoroutine(SpawnLoop());");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Stop spawning objects.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public void StopSpawning()");
            sb.AppendLine("    {");
            sb.AppendLine("        _isSpawning = false;");
            sb.AppendLine("        if (_spawnCoroutine != null)");
            sb.AppendLine("        {");
            sb.AppendLine("            StopCoroutine(_spawnCoroutine);");
            sb.AppendLine("            _spawnCoroutine = null;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Spawn a single object immediately.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <returns>The spawned GameObject, or null if max active reached.</returns>");
            sb.AppendLine("    public GameObject SpawnOne()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_pool.ActiveCount >= maxActiveObjects) return null;");
            sb.AppendLine();
            sb.AppendLine("        var obj = _pool.Get();");
            sb.AppendLine("        if (obj == null) return null;");
            sb.AppendLine();
            sb.AppendLine("        obj.transform.position = GetSpawnPosition();");
            sb.AppendLine("        obj.transform.rotation = Quaternion.identity;");
            sb.AppendLine("        onObjectSpawned?.Invoke(obj);");
            sb.AppendLine("        return obj;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Despawn an object and return it to the pool.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public void Despawn(GameObject obj)");
            sb.AppendLine("    {");
            sb.AppendLine("        onObjectDespawned?.Invoke(obj);");
            sb.AppendLine("        _pool.Return(obj);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Despawn all active objects.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public void DespawnAll()");
            sb.AppendLine("    {");
            sb.AppendLine("        _pool.ReturnAll();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Private Methods");
            sb.AppendLine();
            sb.AppendLine("    private IEnumerator SpawnLoop()");
            sb.AppendLine("    {");
            sb.AppendLine("        float interval = spawnRate > 0 ? 1f / spawnRate : 1f;");
            sb.AppendLine();
            sb.AppendLine("        while (_isSpawning)");
            sb.AppendLine("        {");
            sb.AppendLine("            SpawnOne();");
            sb.AppendLine("            yield return new WaitForSeconds(interval);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private Vector3 GetSpawnPosition()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (useSpawnPoints && spawnPoints.Count > 0)");
            sb.AppendLine("        {");
            sb.AppendLine("            int index = UnityEngine.Random.Range(0, spawnPoints.Count);");
            sb.AppendLine("            return spawnPoints[index].position;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        Vector3 center = transform.position;");
            sb.AppendLine("        Vector3 halfSize = spawnAreaSize * 0.5f;");
            sb.AppendLine("        return new Vector3(");
            sb.AppendLine("            center.x + UnityEngine.Random.Range(-halfSize.x, halfSize.x),");
            sb.AppendLine("            center.y + UnityEngine.Random.Range(-halfSize.y, halfSize.y),");
            sb.AppendLine("            center.z + UnityEngine.Random.Range(-halfSize.z, halfSize.z)");
            sb.AppendLine("        );");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Gizmos");
            sb.AppendLine();
            sb.AppendLine("    private void OnDrawGizmosSelected()");
            sb.AppendLine("    {");
            sb.AppendLine("        Gizmos.color = new Color(0f, 1f, 0f, 0.25f);");
            sb.AppendLine("        Gizmos.DrawWireCube(transform.position, spawnAreaSize);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("#endregion");

            return sb.ToString();
        }

        #endregion

        #region create_dialogue_system

        private static object CreateDialogueSystem(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_dialogue_system");

            string scriptPath = GetStringParam(p, "script_path", "Assets/Scripts/Systems/DialogueSystem.cs");

            if (!scriptPath.StartsWith("Assets/"))
                scriptPath = "Assets/" + scriptPath;
            if (!scriptPath.EndsWith(".cs"))
                scriptPath += ".cs";

            string dir = Path.GetDirectoryName(
                Path.Combine(Application.dataPath.Replace("/Assets", ""), scriptPath)
            );
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Generate DialogueNode ScriptableObject
            string nodeScriptPath = Path.Combine(Path.GetDirectoryName(scriptPath), "DialogueNode.cs");
            string nodeContent = GenerateDialogueNodeScript();
            string nodeFullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), nodeScriptPath);
            File.WriteAllText(nodeFullPath, nodeContent);

            // Generate DialogueSystem MonoBehaviour
            string systemContent = GenerateDialogueSystemScript();
            string systemFullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), scriptPath);
            File.WriteAllText(systemFullPath, systemContent);

            AssetDatabase.Refresh();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "message", "Dialogue system created" },
                { "scripts", new List<object> { nodeScriptPath, scriptPath } }
            };
        }

        private static string GenerateDialogueNodeScript()
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Represents a single dialogue choice leading to another node.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("[Serializable]");
            sb.AppendLine("public class DialogueChoice");
            sb.AppendLine("{");
            sb.AppendLine("    [SerializeField] private string choiceText;");
            sb.AppendLine("    [SerializeField] private DialogueNode nextNode;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>The text displayed for this choice.</summary>");
            sb.AppendLine("    public string ChoiceText => choiceText;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>The node to navigate to when this choice is selected.</summary>");
            sb.AppendLine("    public DialogueNode NextNode => nextNode;");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// ScriptableObject representing a single node in a dialogue tree.");
            sb.AppendLine("/// Create instances via Assets > Create > Game > Dialogue Node.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("[CreateAssetMenu(fileName = \"NewDialogueNode\", menuName = \"Game/Dialogue Node\")]");
            sb.AppendLine("public class DialogueNode : ScriptableObject");
            sb.AppendLine("{");
            sb.AppendLine("    #region Fields");
            sb.AppendLine();
            sb.AppendLine("    [Header(\"Speaker\")]");
            sb.AppendLine("    [SerializeField] private string speakerName = \"\";");
            sb.AppendLine("    [SerializeField] private Sprite speakerPortrait;");
            sb.AppendLine();
            sb.AppendLine("    [Header(\"Dialogue\")]");
            sb.AppendLine("    [SerializeField, TextArea(3, 8)] private string dialogueText = \"\";");
            sb.AppendLine();
            sb.AppendLine("    [Header(\"Choices\")]");
            sb.AppendLine("    [SerializeField] private DialogueChoice[] choices;");
            sb.AppendLine();
            sb.AppendLine("    [Header(\"Auto-Advance\")]");
            sb.AppendLine("    [SerializeField] private DialogueNode nextNode;");
            sb.AppendLine("    [SerializeField] private float autoAdvanceDelay = 0f;");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Properties");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Name of the speaker.</summary>");
            sb.AppendLine("    public string SpeakerName => speakerName;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Portrait sprite of the speaker.</summary>");
            sb.AppendLine("    public Sprite SpeakerPortrait => speakerPortrait;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>The dialogue text to display.</summary>");
            sb.AppendLine("    public string DialogueText => dialogueText;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Available choices (empty if no branching).</summary>");
            sb.AppendLine("    public DialogueChoice[] Choices => choices ?? new DialogueChoice[0];");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Whether this node has player choices.</summary>");
            sb.AppendLine("    public bool HasChoices => choices != null && choices.Length > 0;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>The next node if no choices (linear progression).</summary>");
            sb.AppendLine("    public DialogueNode NextNode => nextNode;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Delay before auto-advancing (0 = manual advance).</summary>");
            sb.AppendLine("    public float AutoAdvanceDelay => autoAdvanceDelay;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Whether this is the last node in the conversation.</summary>");
            sb.AppendLine("    public bool IsEndNode => !HasChoices && nextNode == null;");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateDialogueSystemScript()
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.Events;");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Manages dialogue flow, displaying nodes and handling player choices.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("public class DialogueSystem : MonoBehaviour");
            sb.AppendLine("{");
            sb.AppendLine("    #region Serialized Fields");
            sb.AppendLine();
            sb.AppendLine("    [Header(\"Events\")]");
            sb.AppendLine("    [SerializeField] private UnityEvent onDialogueStart;");
            sb.AppendLine("    [SerializeField] private UnityEvent onDialogueEnd;");
            sb.AppendLine("    [SerializeField] private UnityEvent<DialogueNode> onNodeChanged;");
            sb.AppendLine("    [SerializeField] private UnityEvent<string, string> onDialogueDisplay;");
            sb.AppendLine("    [SerializeField] private UnityEvent<DialogueChoice[]> onChoicesAvailable;");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Private Fields");
            sb.AppendLine();
            sb.AppendLine("    private DialogueNode _currentNode;");
            sb.AppendLine("    private bool _isDialogueActive = false;");
            sb.AppendLine("    private Coroutine _autoAdvanceCoroutine;");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Properties");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Whether a dialogue is currently active.</summary>");
            sb.AppendLine("    public bool IsDialogueActive => _isDialogueActive;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>The currently displayed dialogue node.</summary>");
            sb.AppendLine("    public DialogueNode CurrentNode => _currentNode;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Singleton instance for easy access.</summary>");
            sb.AppendLine("    public static DialogueSystem Instance { get; private set; }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Unity Callbacks");
            sb.AppendLine();
            sb.AppendLine("    private void Awake()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (Instance != null && Instance != this)");
            sb.AppendLine("        {");
            sb.AppendLine("            Destroy(gameObject);");
            sb.AppendLine("            return;");
            sb.AppendLine("        }");
            sb.AppendLine("        Instance = this;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Public Methods");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Start a new dialogue from the given root node.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <param name=\"startNode\">The first node of the dialogue.</param>");
            sb.AppendLine("    public void StartDialogue(DialogueNode startNode)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (startNode == null)");
            sb.AppendLine("        {");
            sb.AppendLine("            Debug.LogWarning(\"DialogueSystem: Cannot start dialogue with null node.\");");
            sb.AppendLine("            return;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        _isDialogueActive = true;");
            sb.AppendLine("        onDialogueStart?.Invoke();");
            sb.AppendLine("        DisplayNode(startNode);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Display a specific dialogue node.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <param name=\"node\">The node to display.</param>");
            sb.AppendLine("    public void DisplayNode(DialogueNode node)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (node == null)");
            sb.AppendLine("        {");
            sb.AppendLine("            EndDialogue();");
            sb.AppendLine("            return;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        StopAutoAdvance();");
            sb.AppendLine("        _currentNode = node;");
            sb.AppendLine("        onNodeChanged?.Invoke(node);");
            sb.AppendLine("        onDialogueDisplay?.Invoke(node.SpeakerName, node.DialogueText);");
            sb.AppendLine();
            sb.AppendLine("        if (node.HasChoices)");
            sb.AppendLine("        {");
            sb.AppendLine("            onChoicesAvailable?.Invoke(node.Choices);");
            sb.AppendLine("        }");
            sb.AppendLine("        else if (node.AutoAdvanceDelay > 0)");
            sb.AppendLine("        {");
            sb.AppendLine("            _autoAdvanceCoroutine = StartCoroutine(AutoAdvance(node.AutoAdvanceDelay));");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Select a choice by index from the current node.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <param name=\"choiceIndex\">Index of the choice to select.</param>");
            sb.AppendLine("    public void MakeChoice(int choiceIndex)");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_currentNode == null || !_currentNode.HasChoices) return;");
            sb.AppendLine();
            sb.AppendLine("        var choices = _currentNode.Choices;");
            sb.AppendLine("        if (choiceIndex < 0 || choiceIndex >= choices.Length)");
            sb.AppendLine("        {");
            sb.AppendLine("            Debug.LogWarning($\"DialogueSystem: Invalid choice index {choiceIndex}.\");");
            sb.AppendLine("            return;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        var nextNode = choices[choiceIndex].NextNode;");
            sb.AppendLine("        if (nextNode != null)");
            sb.AppendLine("            DisplayNode(nextNode);");
            sb.AppendLine("        else");
            sb.AppendLine("            EndDialogue();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Advance to the next node (for linear, non-choice nodes).");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public void AdvanceDialogue()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_currentNode == null) return;");
            sb.AppendLine();
            sb.AppendLine("        if (_currentNode.HasChoices) return;");
            sb.AppendLine();
            sb.AppendLine("        if (_currentNode.NextNode != null)");
            sb.AppendLine("            DisplayNode(_currentNode.NextNode);");
            sb.AppendLine("        else");
            sb.AppendLine("            EndDialogue();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// End the current dialogue.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public void EndDialogue()");
            sb.AppendLine("    {");
            sb.AppendLine("        StopAutoAdvance();");
            sb.AppendLine("        _currentNode = null;");
            sb.AppendLine("        _isDialogueActive = false;");
            sb.AppendLine("        onDialogueEnd?.Invoke();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Private Methods");
            sb.AppendLine();
            sb.AppendLine("    private IEnumerator AutoAdvance(float delay)");
            sb.AppendLine("    {");
            sb.AppendLine("        yield return new WaitForSeconds(delay);");
            sb.AppendLine("        AdvanceDialogue();");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private void StopAutoAdvance()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_autoAdvanceCoroutine != null)");
            sb.AppendLine("        {");
            sb.AppendLine("            StopCoroutine(_autoAdvanceCoroutine);");
            sb.AppendLine("            _autoAdvanceCoroutine = null;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine("}");

            return sb.ToString();
        }

        #endregion

        #region create_interaction_system

        private static object CreateInteractionSystem(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_interaction_system");

            string targetPath = GetStringParam(p, "target");
            float interactionRange = GetFloatParam(p, "interaction_range", 2f);
            string interactionKey = GetStringParam(p, "interaction_key", "E");
            bool useNewInputSystem = GetBoolParam(p, "use_new_input_system", false);
            string scriptPath = GetStringParam(p, "script_path", "Assets/Scripts/Systems/InteractionSystem.cs");

            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentException("'target' is required");

            var go = FindGameObject(targetPath);

            if (!scriptPath.StartsWith("Assets/"))
                scriptPath = "Assets/" + scriptPath;
            if (!scriptPath.EndsWith(".cs"))
                scriptPath += ".cs";

            string content = GenerateInteractionSystemScript(interactionRange, interactionKey, useNewInputSystem);
            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), scriptPath);

            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, content);
            AssetDatabase.Refresh();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "message", $"Interaction system created for '{go.name}'" },
                { "gameObject", GetGameObjectPath(go) },
                { "script_path", scriptPath },
                { "interaction_range", interactionRange },
                { "interaction_key", interactionKey },
                { "use_new_input_system", useNewInputSystem }
            };
        }

        private static string GenerateInteractionSystemScript(float range, string key, bool useNewInput)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.Events;");

            if (useNewInput)
                sb.AppendLine("using UnityEngine.InputSystem;");

            sb.AppendLine();
            sb.AppendLine("#region IInteractable Interface");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Interface for objects that can be interacted with by the player.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("public interface IInteractable");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Execute the interaction.</summary>");
            sb.AppendLine("    void Interact(GameObject interactor);");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Get the prompt text to display when the player is nearby.</summary>");
            sb.AppendLine("    string GetInteractionPrompt();");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Whether this object can currently be interacted with.</summary>");
            sb.AppendLine("    bool CanInteract { get; }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("#endregion");
            sb.AppendLine();
            sb.AppendLine("#region Interaction System");
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Proximity-based interaction system that detects nearby IInteractable objects.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("public class InteractionSystem : MonoBehaviour");
            sb.AppendLine("{");
            sb.AppendLine("    #region Serialized Fields");
            sb.AppendLine();
            sb.AppendLine("    [Header(\"Detection\")]");
            sb.AppendLine($"    [SerializeField] private float interactionRange = {range}f;");
            sb.AppendLine("    [SerializeField] private LayerMask interactableLayer = ~0;");
            sb.AppendLine("    [SerializeField] private int maxDetectedObjects = 10;");
            sb.AppendLine();

            if (!useNewInput)
            {
                sb.AppendLine("    [Header(\"Input\")]");
                sb.AppendLine($"    [SerializeField] private KeyCode interactionKey = KeyCode.{key};");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("    [Header(\"Input\")]");
                sb.AppendLine("    [SerializeField] private InputAction interactAction;");
                sb.AppendLine();
            }

            sb.AppendLine("    [Header(\"Events\")]");
            sb.AppendLine("    [SerializeField] private UnityEvent<IInteractable> onInteractableFound;");
            sb.AppendLine("    [SerializeField] private UnityEvent onInteractableLost;");
            sb.AppendLine("    [SerializeField] private UnityEvent<string> onPromptChanged;");
            sb.AppendLine("    [SerializeField] private UnityEvent<IInteractable> onInteracted;");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Private Fields");
            sb.AppendLine();
            sb.AppendLine("    private IInteractable _closestInteractable;");
            sb.AppendLine("    private Collider[] _overlapResults;");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Properties");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>The closest interactable object, or null if none in range.</summary>");
            sb.AppendLine("    public IInteractable ClosestInteractable => _closestInteractable;");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>Whether an interactable is currently in range.</summary>");
            sb.AppendLine("    public bool HasInteractable => _closestInteractable != null;");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Unity Callbacks");
            sb.AppendLine();
            sb.AppendLine("    private void Awake()");
            sb.AppendLine("    {");
            sb.AppendLine("        _overlapResults = new Collider[maxDetectedObjects];");
            sb.AppendLine("    }");
            sb.AppendLine();

            if (useNewInput)
            {
                sb.AppendLine("    private void OnEnable()");
                sb.AppendLine("    {");
                sb.AppendLine("        if (interactAction != null)");
                sb.AppendLine("        {");
                sb.AppendLine("            interactAction.Enable();");
                sb.AppendLine("            interactAction.performed += OnInteractPerformed;");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    private void OnDisable()");
                sb.AppendLine("    {");
                sb.AppendLine("        if (interactAction != null)");
                sb.AppendLine("        {");
                sb.AppendLine("            interactAction.performed -= OnInteractPerformed;");
                sb.AppendLine("            interactAction.Disable();");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            sb.AppendLine("    private void Update()");
            sb.AppendLine("    {");
            sb.AppendLine("        FindClosestInteractable();");

            if (!useNewInput)
            {
                sb.AppendLine();
                sb.AppendLine("        if (Input.GetKeyDown(interactionKey))");
                sb.AppendLine("            TryInteract();");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Public Methods");
            sb.AppendLine();
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Attempt to interact with the closest interactable object.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public void TryInteract()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_closestInteractable == null) return;");
            sb.AppendLine("        if (!_closestInteractable.CanInteract) return;");
            sb.AppendLine();
            sb.AppendLine("        _closestInteractable.Interact(gameObject);");
            sb.AppendLine("        onInteracted?.Invoke(_closestInteractable);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Private Methods");
            sb.AppendLine();

            if (useNewInput)
            {
                sb.AppendLine("    private void OnInteractPerformed(InputAction.CallbackContext context)");
                sb.AppendLine("    {");
                sb.AppendLine("        TryInteract();");
                sb.AppendLine("    }");
                sb.AppendLine();
            }

            sb.AppendLine("    private void FindClosestInteractable()");
            sb.AppendLine("    {");
            sb.AppendLine("        int count = Physics.OverlapSphereNonAlloc(");
            sb.AppendLine("            transform.position, interactionRange, _overlapResults, interactableLayer);");
            sb.AppendLine();
            sb.AppendLine("        IInteractable closest = null;");
            sb.AppendLine("        float closestDist = float.MaxValue;");
            sb.AppendLine();
            sb.AppendLine("        for (int i = 0; i < count; i++)");
            sb.AppendLine("        {");
            sb.AppendLine("            var interactable = _overlapResults[i].GetComponent<IInteractable>();");
            sb.AppendLine("            if (interactable == null || !interactable.CanInteract) continue;");
            sb.AppendLine();
            sb.AppendLine("            float dist = Vector3.Distance(transform.position, _overlapResults[i].transform.position);");
            sb.AppendLine("            if (dist < closestDist)");
            sb.AppendLine("            {");
            sb.AppendLine("                closestDist = dist;");
            sb.AppendLine("                closest = interactable;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        if (closest != _closestInteractable)");
            sb.AppendLine("        {");
            sb.AppendLine("            _closestInteractable = closest;");
            sb.AppendLine();
            sb.AppendLine("            if (_closestInteractable != null)");
            sb.AppendLine("            {");
            sb.AppendLine("                onInteractableFound?.Invoke(_closestInteractable);");
            sb.AppendLine("                onPromptChanged?.Invoke(_closestInteractable.GetInteractionPrompt());");
            sb.AppendLine("            }");
            sb.AppendLine("            else");
            sb.AppendLine("            {");
            sb.AppendLine("                onInteractableLost?.Invoke();");
            sb.AppendLine("                onPromptChanged?.Invoke(\"\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine();
            sb.AppendLine("    #region Gizmos");
            sb.AppendLine();
            sb.AppendLine("    private void OnDrawGizmosSelected()");
            sb.AppendLine("    {");
            sb.AppendLine("        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);");
            sb.AppendLine("        Gizmos.DrawWireSphere(transform.position, interactionRange);");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    #endregion");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("#endregion");

            return sb.ToString();
        }

        #endregion
    }
}
