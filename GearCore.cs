/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.IO;

namespace Oxide.Plugins
{
    [Info("Gear Core", "VisEntities", "1.0.0")]
    [Description("An API for other plugins to create and equip gear sets.")]
    public class GearCore : RustPlugin
    {
        #region Fields

        private static GearCore _plugin;
        private StoredData _storedData;

        #endregion Fields

        #region Stored Data

        public class StoredData
        {
            [JsonProperty("Gear Sets")]
            public Dictionary<string, GearSet> GearSets { get; set; } = new Dictionary<string, GearSet>();
        }

        public class GearSet
        {
            [JsonProperty("Main")]
            public List<ItemInfo> Main { get; set; } = new List<ItemInfo>();

            [JsonProperty("Wear")]
            public List<ItemInfo> Wear { get; set; } = new List<ItemInfo>();

            [JsonProperty("Belt")]
            public List<ItemInfo> Belt { get; set; } = new List<ItemInfo>();
        }

        public class ItemInfo
        {
            [JsonProperty(PropertyName = "Short Name")]
            public string ShortName { get; set; }

            [JsonProperty(PropertyName = "Amount")]
            public int Amount { get; set; }

            [JsonProperty(PropertyName = "Skin Id")]
            public ulong SkinId { get; set; }

            [JsonProperty(PropertyName = "Name")]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "Condition")]
            public float Condition { get; set; }

            [JsonProperty(PropertyName = "Position")]
            public int Position { get; set; }

            [JsonProperty(PropertyName = "Ammunition")]
            public SubItemInfo Ammunition { get; set; }

            [JsonProperty(PropertyName = "Attachments")]
            public List<SubItemInfo> Attachments { get; set; } = new List<SubItemInfo>();
        }

        public class SubItemInfo
        {
            [JsonProperty(PropertyName = "Short Name")]
            public string ShortName { get; set; }

            [JsonProperty(PropertyName = "Amount")]
            public int Amount { get; set; }
        }

        #endregion Stored Data

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
            _storedData = DataFileUtil.LoadOrCreate<StoredData>(DataFileUtil.GetFilePath());
        }

        private void Unload()
        {
            _plugin = null;
        }

        #endregion Oxide Hooks

        #region Helper Classes

        public static class DataFileUtil
        {
            private const string FOLDER = "";

            public static string GetFilePath(string filename = null)
            {
                if (filename == null)
                    filename = _plugin.Name;

                return Path.Combine(FOLDER, filename);
            }

            public static string[] GetAllFilePaths()
            {
                string[] filePaths = Interface.Oxide.DataFileSystem.GetFiles(FOLDER);

                for (int i = 0; i < filePaths.Length; i++)
                {
                    filePaths[i] = filePaths[i].Substring(0, filePaths[i].Length - 5);
                }

                return filePaths;
            }

            public static bool Exists(string filePath)
            {
                return Interface.Oxide.DataFileSystem.ExistsDatafile(filePath);
            }

            public static T Load<T>(string filePath) where T : class, new()
            {
                T data = Interface.Oxide.DataFileSystem.ReadObject<T>(filePath);
                if (data == null)
                    data = new T();

                return data;
            }

            public static T LoadIfExists<T>(string filePath) where T : class, new()
            {
                if (Exists(filePath))
                    return Load<T>(filePath);
                else
                    return null;
            }

            public static T LoadOrCreate<T>(string filePath) where T : class, new()
            {
                T data = LoadIfExists<T>(filePath);
                if (data == null)
                    data = new T();

                return data;
            }

            public static void Save<T>(string filePath, T data)
            {
                Interface.Oxide.DataFileSystem.WriteObject<T>(filePath, data);
            }

            public static void Delete(string filePath)
            {
                Interface.Oxide.DataFileSystem.DeleteDataFile(filePath);
            }
        }

        #endregion Helper Classes

        #region Permissions

        public static class PermissionUtil
        {
            public const string USE = "gearcore.use";
            private static readonly List<string> _permissions = new List<string>
            {
                USE
            };

            public static void RegisterPermissions()
            {
                foreach (var permission in _permissions)
                {
                    _plugin.permission.RegisterPermission(permission, _plugin);
                }
            }

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                return _plugin.permission.UserHasPermission(player.UserIDString, permissionName);
            }
        }

        #endregion Permissions

        #region Commands

        private static class Cmd
        {
            /// <summary>
            /// gear save <name>
            /// gear equip <name>
            /// gear delete <name>
            /// gear list
            /// </summary>
            public const string GEAR = "gear";
        }

        [ChatCommand(Cmd.GEAR)]
        private void cmdGear(BasePlayer player, string cmd, string[] args)
        {
            if (player == null)
                return;

            if (!PermissionUtil.HasPermission(player, PermissionUtil.USE))
            {
                SendMessage(player, Lang.NoPermission);
                return;
            }

            if (args.Length < 1)
            {
                SendMessage(player, Lang.InvalidCommand);
                return;
            }

            string subCommand = args[0].ToLower();
            string gearSetName = null;
            if (args.Length > 1)
                gearSetName = args[1];

            switch (subCommand)
            {
                case "save":
                    {
                        if (gearSetName == null)
                        {
                            SendMessage(player, Lang.UsageSaveGear);
                            return;
                        }

                        bool isUpdated = SaveOrUpdateGearSet(player, gearSetName);

                        if (isUpdated)
                            SendMessage(player, Lang.GearUpdated, gearSetName);
                        else
                            SendMessage(player, Lang.GearSaved, gearSetName);
                        
                        break;
                    }
                case "equip":
                    {
                        if (gearSetName == null)
                        {
                            SendMessage(player, Lang.UsageEquipGear);
                            return;
                        }
                        if (EquipGearSet(player, gearSetName))
                            SendMessage(player, Lang.GearEquipped, gearSetName);
                        else
                            SendMessage(player, Lang.GearSetNotFound, gearSetName);
                        break;
                    }
                case "list":
                    {
                        if (_storedData.GearSets.Count == 0)
                        {
                            SendMessage(player, Lang.NoGearSetsAvailable);
                            return;
                        }

                        string gearSets = GetGearSets();
                        SendMessage(player, Lang.AvailableGearSets, gearSets);
                        break;
                    }
                case "delete":
                    {
                        if (gearSetName == null)
                        {
                            SendMessage(player, Lang.UsageDeleteGear);
                            return;
                        }
                        if (DeleteGearSet(gearSetName))
                            SendMessage(player, Lang.GearDeleted, gearSetName);
                        else
                            SendMessage(player, Lang.GearSetNotFound, gearSetName);
                        break;
                    }

                default:
                    SendMessage(player, Lang.InvalidCommand);
                    break;
            }
        }

        #endregion Commands

        #region Gear Set Handling

        private bool SaveOrUpdateGearSet(BasePlayer player, string gearSetName)
        {
            GearSet gearSet = new GearSet();

            GetItemsFromContainer(player.inventory.containerMain, gearSet.Main);
            GetItemsFromContainer(player.inventory.containerWear, gearSet.Wear);
            GetItemsFromContainer(player.inventory.containerBelt, gearSet.Belt);

            bool isUpdate = _storedData.GearSets.ContainsKey(gearSetName);

            _storedData.GearSets[gearSetName] = gearSet;
            DataFileUtil.Save(DataFileUtil.GetFilePath(), _storedData);

            return isUpdate;
        }

        private void GetItemsFromContainer(ItemContainer container, List<ItemInfo> itemList)
        {
            foreach (Item item in container.itemList)
            {
                ItemInfo itemInfo = new ItemInfo
                {
                    ShortName = item.info.shortname,
                    Amount = item.amount,
                    SkinId = item.skin,
                    Name = item.name,
                    Condition = item.condition,
                    Position = item.position
                };

                if (item.info.category == ItemCategory.Weapon && item.GetHeldEntity() is BaseProjectile weapon)
                {
                    SubItemInfo ammunitionInfo = new SubItemInfo();
                    if (weapon.primaryMagazine.ammoType != null)
                        ammunitionInfo.ShortName = weapon.primaryMagazine.ammoType.shortname;
                    ammunitionInfo.Amount = weapon.primaryMagazine.contents;

                    itemInfo.Ammunition = ammunitionInfo;

                    if (item.contents != null && item.contents.itemList.Count > 0)
                    {
                        foreach (var attachment in item.contents.itemList)
                        {
                            SubItemInfo attachmentInfo = new SubItemInfo
                            {
                                ShortName = attachment.info.shortname,
                                Amount = attachment.amount
                            };
                            itemInfo.Attachments.Add(attachmentInfo);
                        }
                    }
                }

                itemList.Add(itemInfo);
            }
        }

        private void EquipItemsToContainer(ItemContainer container, List<ItemInfo> itemList)
        {
            foreach (ItemInfo itemInfo in itemList)
            {
                Item item = ItemManager.CreateByName(itemInfo.ShortName, itemInfo.Amount, itemInfo.SkinId);
                if (item != null)
                {
                    item.condition = itemInfo.Condition;
                    item.position = itemInfo.Position;
                    item.name = itemInfo.Name;
                    item.SetParent(container);

                    if (item.info.category == ItemCategory.Weapon && item.GetHeldEntity() is BaseProjectile weapon)
                    {
                        if (itemInfo.Ammunition != null)
                        {
                            weapon.primaryMagazine.contents = itemInfo.Ammunition.Amount;
                            ItemDefinition ammoDef = ItemManager.FindItemDefinition(itemInfo.Ammunition.ShortName);
                            if (ammoDef != null)
                                weapon.primaryMagazine.ammoType = ammoDef;
                        }
                    }

                    if (itemInfo.Attachments.Count > 0 && item.contents != null)
                    {
                        foreach (var attachmentInfo in itemInfo.Attachments)
                        {
                            Item attachment = ItemManager.CreateByName(attachmentInfo.ShortName, attachmentInfo.Amount);
                            if (attachment != null)
                                attachment.SetParent(item.contents);
                        }
                    }
                }
            }
        }

        private string GetGearSets()
        {
            return string.Join("\n", _storedData.GearSets.Keys);
        }

        private bool DeleteGearSet(string gearSetName)
        {
            if (!_storedData.GearSets.ContainsKey(gearSetName))
                return false;

            _storedData.GearSets.Remove(gearSetName);
            DataFileUtil.Save(DataFileUtil.GetFilePath(), _storedData);

            return true;
        }

        #endregion Gear Set Handling

        #region Hooks

        [HookMethod(nameof(EquipGearSet))]
        private bool EquipGearSet(BasePlayer player, string gearSetName, bool clearCurrentInventory = true)
        {
            if (!_storedData.GearSets.ContainsKey(gearSetName))
                return false;

            GearSet gearSet = _storedData.GearSets[gearSetName];

            if (clearCurrentInventory)
            {
                player.inventory.containerMain.Clear();
                player.inventory.containerWear.Clear();
                player.inventory.containerBelt.Clear();
            }

            EquipItemsToContainer(player.inventory.containerMain, gearSet.Main);
            EquipItemsToContainer(player.inventory.containerWear, gearSet.Wear);
            EquipItemsToContainer(player.inventory.containerBelt, gearSet.Belt);

            return true;
        }

        #endregion Hook

        #region Localization

        private class Lang
        {
            public const string NoPermission = "NoPermission";
            public const string InvalidCommand = "InvalidCommand";
            public const string UsageSaveGear = "UsageSaveGear";
            public const string UsageEquipGear = "UsageEquipGear";
            public const string UsageDeleteGear = "UsageDeleteGear";
            public const string GearEquipped = "GearEquipped";
            public const string GearSetNotFound = "GearSetNotFound";
            public const string GearUpdated = "GearUpdated";
            public const string GearSaved = "GearSaved";
            public const string GearDeleted = "GearDeleted";
            public const string NoGearSetsAvailable = "NoGearSetsAvailable";
            public const string AvailableGearSets = "AvailableGearSets";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.NoPermission] = "You don't have permission to use this command.",
                [Lang.InvalidCommand] = "Invalid command. Correct usage:\n- <color=#F0E68C>/gear save <name></color> - Saves your inventory as a gear set.\n- <color=#F0E68C>/gear equip <name></color> - Equips the specified gear set.\n- <color=#F0E68C>/gear delete <name></color> - Deletes the specified gear set.\n- <color=#F0E68C>/gear list</color> - Lists all available gear sets.",
                [Lang.UsageSaveGear] = "Invalid command. Correct usage:\n- <color=#F0E68C>/gear save <name></color> - Saves your inventory as a gear set.",
                [Lang.UsageEquipGear] = "Invalid command. Correct usage:\n- <color=#F0E68C>/gear equip <name></color> - Equips the specified gear set.",
                [Lang.UsageDeleteGear] = "Invalid command. Correct usage:\n- <color=#F0E68C>/gear delete <name></color> - Deletes the specified gear set.",
                [Lang.GearEquipped] = "You have equipped the gear set <color=#ADFF2F>{0}</color>.",
                [Lang.GearSetNotFound] = "Gear set <color=#ADFF2F>{0}</color> does not exist.",
                [Lang.GearUpdated] = "Gear set <color=#ADFF2F>{0}</color> updated successfully.",
                [Lang.GearSaved] = "Gear set <color=#ADFF2F>{0}</color> created and saved successfully.",
                [Lang.GearDeleted] = "Gear set <color=#ADFF2F>{0}</color> deleted successfully.",
                [Lang.NoGearSetsAvailable] = "No gear sets available.",
                [Lang.AvailableGearSets] = "Available gear sets:\n- <color=#ADFF2F>{0}</color>",

            }, this, "en");
        }

        private void SendMessage(BasePlayer player, string messageKey, params object[] args)
        {
            string message = lang.GetMessage(messageKey, this, player.UserIDString);
            if (args.Length > 0)
                message = string.Format(message, args);

            PrintToChat(message);
        }

        #endregion Localization
    }
}