#region License (GPL v2)
/*
    IceBox - Make small storage boxes keep food like a fridge
    Copyright (c) RFC1920 <desolationoutpostpve@gmail.com>

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; version 2
    of the License only.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/
#endregion License (GPL v2)

using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using System.Collections.Generic;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("IceBox", "RFC1920", "0.0.5")]
    [Description("Normal box that preserves food")]
    internal class IceBox : RustPlugin
    {
        public static IceBox Instance;
        private ConfigData configData;
        private const string effPrefab = "assets/prefabs/misc/xmas/ice throne/effects/pfx_icethrone.prefab";
        private Dictionary<string, bool> iceEnable = new();
        private Dictionary<ulong, List<ulong>> iceBoxes = new();
        private bool newsave = false;

        #region Message
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private void Message(IPlayer player, string key, params object[] args) => player.Reply(Lang(key, player.Id, args));

        private void DoLog(string message)
        {
            if (configData.Options.debug)
            {
                Interface.GetMod().LogInfo($"{Name}: {message}");
            }
        }
        #endregion Message
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["notenabled"] = "IceBox not enabled.",
                ["isicebox"] = "This is an IceBox!",
                ["isnoticebox"] = "This is NOT an IceBox!",
                ["isnowicebox"] = "Box is now an IceBox!",
                ["wasicebox"] = "IceBox removed!",
                ["enabled"] = "IceBox enabled for 20 seconds.",
                ["creatingice"] = "Creating IceBox."
            }, this);
        }

        private void OnNewSave() => newsave = true;

        private void OnServerInitialized()
        {
            LoadConfigVariables();
            LoadData();
            AddCovalenceCommand("ib", "cmdIceBox");
            iceEnable.Clear();
            Instance = this;
            if (newsave)
            {
                iceBoxes.Clear();
                SaveData();
                return;
            }

            foreach (KeyValuePair<ulong, List<ulong>> ibuser in iceBoxes)
            {
                foreach (ulong ib in ibuser.Value)
                {
                    BaseNetworkable box = BaseNetworkable.serverEntities.Find(new NetworkableId(ib));
                    DoLog($"Setting up previously deployed IceBox for id {box.net.ID.Value}");
                    box?.gameObject.GetOrAddComponent<IcyBox>();
                }
            }
        }

        private void Unload()
        {
            foreach (KeyValuePair<ulong, List<ulong>> ibuser in iceBoxes)
            {
                foreach (ulong ib in ibuser.Value)
                {
                    BaseNetworkable box = BaseNetworkable.serverEntities.Find(new NetworkableId(ib));
                    if (box != null)
                    {
                        IcyBox boxComp = box.GetComponent<IcyBox>();
                        if (boxComp != null)
                        {
                            DoLog($"Removing IceBox component for id {box.net.ID.Value}");
                            UnityEngine.Object.DestroyImmediate(boxComp);
                        }
                    }
                }
            }
        }

        [Command("ib")]
        private void cmdIceBox(IPlayer iplayer, string command, string[] args)
        {
            if (iceEnable.ContainsKey(iplayer.Id)) iceEnable.Remove(iplayer.Id);
            BasePlayer player = iplayer.Object as BasePlayer;

            if (args.Length > 0)
            {
                if (!iceBoxes.ContainsKey(player.userID))
                {
                    iceBoxes.Add(player.userID, new List<ulong>());
                }
                switch (args[0])
                {
                    case "check":
                        {
                            BaseEntity box = RaycastAll<BaseEntity>(player.eyes.HeadRay()) as BaseEntity;
                            if (box == null) return;

                            if (configData.Options.allowedStorage != null && configData.Options.allowedStorage.Contains(box?.ShortPrefabName))
                            {
                                IcyBox ib = box?.gameObject.GetComponent<IcyBox>();

                                if (ib != null)
                                {
                                    Message(iplayer, "isicebox");
                                    return;
                                }
                                Message(iplayer, "isnoticebox");
                            }
                        }
                        break;
                    case "add":
                        {
                            BaseEntity box = RaycastAll<BaseEntity>(player.eyes.HeadRay()) as BaseEntity;
                            if (box == null) return;
                            if (configData.Options.allowedStorage != null && configData.Options.allowedStorage.Contains(box?.ShortPrefabName))
                            {
                                box?.gameObject.GetOrAddComponent<IcyBox>();
                                iceBoxes[player.userID].Add(box.net.ID.Value);
                                SaveData();
                                Message(iplayer, "isnowicebox");
                            }
                        }
                        break;
                    case "remove":
                        {
                            BaseEntity box = RaycastAll<BaseEntity>(player.eyes.HeadRay()) as BaseEntity;
                            if (box == null) return;
                            if (configData.Options.allowedStorage != null && configData.Options.allowedStorage.Contains(box?.ShortPrefabName))
                            {
                                IcyBox boxComp = box.GetComponent<IcyBox>();
                                if (boxComp != null)
                                {
                                    UnityEngine.Object.DestroyImmediate(boxComp);
                                }
                                iceBoxes[player.userID].Remove(box.net.ID.Value);
                                SaveData();
                                Message(iplayer, "wasicebox");
                            }
                        }
                        break;
                }
                return;
            }

            iceEnable.Add(iplayer.Id, true);
            timer.Once(20, () => { iceEnable.Remove(iplayer.Id); });
            Message(iplayer, "enabled");
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            BaseEntity box = go?.gameObject?.ToBaseEntity();
            //if (box?.ShortPrefabName == "woodbox_deployed")
            if (configData.Options.allowedStorage != null && configData.Options.allowedStorage.Contains(box?.ShortPrefabName))
            {
                BasePlayer pl = plan.GetOwnerPlayer();
                if (!iceEnable.ContainsKey(pl?.UserIDString))
                {
                    Message(pl.IPlayer, "notenabled");
                    return;
                }
                Message(pl.IPlayer, "creatingice");
                go.GetOrAddComponent<IcyBox>();

                if (!iceBoxes.ContainsKey(pl.userID))
                {
                    iceBoxes.Add(pl.userID, new List<ulong>());
                }
                iceBoxes[pl.userID].Add(box.net.ID.Value);
                SaveData();

                if (configData.Options.reskin)
                {
                    box.skinID = configData.Options.skinID > 0 ? configData.Options.skinID : 3336369277;
                }
                box.SendNetworkUpdateImmediate();
            }
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (!configData.Options.showui) return;
            if (!player.userID.IsSteamId()) return;

            CuiHelper.DestroyUi(player, "custom.title.ui");

            if (entity is StorageContainer container)
            {
                if (iceBoxes.ContainsKey(player.userID))
                {
                    if (iceBoxes[player.userID].Contains(container.net.ID.Value) && entity.ShortPrefabName == "woodbox_deployed")
                    {
                        DoLog("Setting up title for IceBox");
                        ShowContainerTitleUI(player, "- IceBox");
                    }
                }
            }
        }

        private void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if (!player.userID.IsSteamId()) return;
            DoLog("Removing title for IceBox");
            CuiHelper.DestroyUi(player, "custom.title.ui");
        }

        private void ShowContainerTitleUI(BasePlayer player, string title)
        {
            CuiElementContainer ui = new();
            string panelName = "custom.title.ui";

            ui.Add(new CuiElement
            {
                Name = panelName,
                Parent = "Overlay",
                Components =
                {
                    //new CuiRectTransformComponent { AnchorMin = "0.4 0.95", AnchorMax = "0.6 1.0" },
                    new CuiRectTransformComponent { AnchorMin = "0.715 0.412", AnchorMax = "0.8 0.45" },
                    new CuiTextComponent
                    {
                        Text = title,
                        FontSize = 14,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 1 1 1"
                    }
                }
            });

            CuiHelper.AddUi(player, ui);
        }

        public class IcyBox : ContainerIOEntity, IFoodSpoilModifier
        {
            [Range(0f, 1f)]
            public float PoweredFoodSpoilageRateMultiplier = 0.1f;

            public float GetSpoilMultiplier(Item arg)
            {
                return PoweredFoodSpoilageRateMultiplier;
            }
        }

        private object RaycastAll<T>(Ray ray) where T : BaseEntity
        {
            RaycastHit[] hits = Physics.RaycastAll(ray);
            GamePhysics.Sort(hits);
            const float distance = 6f;
            object target = false;
            foreach (RaycastHit hit in hits)
            {
                BaseEntity ent = hit.GetEntity();
                if (ent is T && hit.distance < distance)
                {
                    target = ent;
                    break;
                }
            }

            return target;
        }

        #region data
        private void LoadData()
        {
            iceBoxes = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, List<ulong>>>(Name + "/iceboxes");
        }

        private void SaveData()
        {
            Interface.GetMod().DataFileSystem.WriteObject(Name + "/iceboxes", iceBoxes);
        }
        #endregion data

        #region config
        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            ConfigData config = new()
            {
                Options = new Options()
                {
                    allowedStorage = new List<string>() { "woodbox_deployed", "campfire" },
                    skinID = 3336369277,
                    reskin = true,
                    showui = true,
                    debug = false
                },
                Version = Version
            };
            SaveConfig(config);
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();

            configData.Options.allowedStorage ??= new List<string>() { "woodbox_deployed", "campfire" };
            configData.Version = Version;
            SaveConfig(configData);
        }

        private void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }

        public class ConfigData
        {
            public Options Options = new();
            public VersionNumber Version;
        }

        public class Options
        {
            [JsonProperty(PropertyName = "List of allowed storage items")]
            public List<string> allowedStorage;

            [JsonProperty(PropertyName = "IceBox Skin ID")]
            public uint skinID;

            [JsonProperty(PropertyName = "Reskin IceBox")]
            public bool reskin;

            [JsonProperty(PropertyName = "Show UI Overlay")]
            public bool showui;

            public bool debug;
        }
        #endregion config
    }
}
