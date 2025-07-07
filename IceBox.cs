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
using System.Collections.Generic;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("IceBox", "RFC1920", "0.0.1")]
    [Description("Normal box that preserves food")]
    internal class IceBox : RustPlugin
    {
        public static IceBox Instance;
        private ConfigData configData;
        private const string effPrefab = "assets/prefabs/misc/xmas/ice throne/effects/pfx_icethrone.prefab";
        private Dictionary<string, bool> iceEnable = new();
        private Dictionary<ulong, List<ulong>> iceBoxes = new();

        private void OnServerInitialized()
        {
            LoadConfigVariables();
            LoadData();
            AddCovalenceCommand("ib", "cmdIceBox");
            iceEnable.Clear();
            Instance = this;

            foreach (KeyValuePair<ulong, List<ulong>> ibuser in iceBoxes)
            {
                foreach (ulong ib in ibuser.Value)
                {
                    BaseNetworkable box = BaseNetworkable.serverEntities.Find(new NetworkableId(ib));
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
                            if (box.ShortPrefabName.Equals("woodbox_deployed"))
                            {
                                IcyBox ib = box?.gameObject.GetComponent<IcyBox>();

                                if (ib != null)
                                {
                                    SendReply(player, "This is an IceBox!");
                                    return;
                                }
                                SendReply(player, "This is NOT an IceBox!");
                            }
                        }
                        break;
                    case "add":
                        {
                            BaseEntity box = RaycastAll<BaseEntity>(player.eyes.HeadRay()) as BaseEntity;
                            if (box == null) return;
                            if (box.ShortPrefabName.Equals("woodbox_deployed"))
                            {
                                box?.gameObject.GetOrAddComponent<IcyBox>();
                                iceBoxes[player.userID].Add(box.net.ID.Value);
                                SaveData();
                                SendReply(iplayer.Object as BasePlayer, "Box is now an IceBox!");
                            }
                        }
                        break;
                    case "remove":
                        {
                            BaseEntity box = RaycastAll<BaseEntity>(player.eyes.HeadRay()) as BaseEntity;
                            if (box == null) return;
                            if (box.ShortPrefabName.Equals("woodbox_deployed"))
                            {
                                IcyBox boxComp = box.GetComponent<IcyBox>();
                                if (boxComp != null)
                                {
                                    UnityEngine.Object.DestroyImmediate(boxComp);
                                }
                                iceBoxes[player.userID].Remove(box.net.ID.Value);
                                SaveData();
                                SendReply(iplayer.Object as BasePlayer, "IceBox removed!");
                            }
                        }
                        break;
                }
                return;
            }

            iceEnable.Add(iplayer.Id, true);
            timer.Once(20, () => { iceEnable.Remove(iplayer.Id); });
            SendReply(iplayer.Object as BasePlayer, "IceBox enabled for 20 seconds.");
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

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            BaseEntity box = go?.gameObject?.ToBaseEntity();
            if (box?.ShortPrefabName == "woodbox_deployed")
            {
                BasePlayer pl = plan.GetOwnerPlayer();
                if (!iceEnable.ContainsKey(pl?.UserIDString))
                {
                    SendReply(pl, "IceBox not enabled.");
                    return;
                }
                SendReply(pl, "Creating IceBox.");
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
                if (true)
                {
                    ItemContainer boxContainer = box.GetComponent<ItemContainer>();
                    BoxStorage boxStorage = box.GetComponent<BoxStorage>();
                    if (boxStorage != null)
                    {
                        // Of course, this does not work
                        boxStorage.panelTitle = "IceBox";
                    }
                }
                box.SendNetworkUpdateImmediate();
            }
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
                    skinID = 3336369277,
                    reskin = true,
                    debug = false
                },
                Version = Version
            };
            SaveConfig(config);
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();

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
            [JsonProperty(PropertyName = "IceBox Skin ID")]
            public uint skinID;

            [JsonProperty(PropertyName = "Reskin IceBox")]
            public bool reskin;

            public bool debug;
        }
        #endregion config
    }
}
