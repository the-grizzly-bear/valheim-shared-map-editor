using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace SharedMapEditor
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class SharedMapEditorPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "mishka.valheim.sharedmapeditor";
        public const string PluginName = "SharedMapEditor";
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            new Terminal.ConsoleCommand(
                "dumpmaptables",
                "writes every cartography table's stored pin data (owner ids, positions) to a file next to the plugin",
                (Terminal.ConsoleEventArgs args) =>
                {
                    DumpMapTables();
                    return true;
                },
                isCheat: false, isNetwork: false, onlyServer: true, remoteCommand: true);

            new Terminal.ConsoleCommand(
                "purgemaptablepins",
                "<ownerID> - removes every pin with that owner id from every cartography table's shared data",
                (Terminal.ConsoleEventArgs args) =>
                {
                    if (!args.TryParameterLong(1, out long ownerID))
                    {
                        Console.instance?.Print("usage: purgemaptablepins <ownerID>");
                        return false;
                    }
                    PurgeOwnerPins(ownerID);
                    return true;
                },
                isCheat: false, isNetwork: false, onlyServer: true, remoteCommand: true);
        }

        private struct PinEntry
        {
            public long OwnerID;
            public string Name;
            public Vector3 Pos;
            public int Type;
            public bool Checked;
            public string Author;
        }

        private class TableBlob
        {
            public int Version;
            public List<bool> Explored;
            public List<PinEntry> Pins;
        }

        private static List<ZDO> FindMapTableZDOs()
        {
            var objectsByID = (Dictionary<ZDOID, ZDO>)AccessTools
                .Field(typeof(ZDOMan), "m_objectsByID")
                .GetValue(ZDOMan.instance);

            var result = new List<ZDO>();
            foreach (ZDO zdo in objectsByID.Values)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
                if (prefab != null && prefab.GetComponent<MapTable>() != null)
                {
                    result.Add(zdo);
                }
            }
            return result;
        }

        private static TableBlob ReadBlob(byte[] raw)
        {
            byte[] decompressed = Utils.Decompress(raw);
            ZPackage pkg = new ZPackage(decompressed);

            TableBlob blob = new TableBlob
            {
                Version = pkg.ReadInt(),
                Explored = new List<bool>(),
                Pins = new List<PinEntry>(),
            };

            int exploredCount = pkg.ReadInt();
            for (int i = 0; i < exploredCount; i++)
            {
                blob.Explored.Add(pkg.ReadBool());
            }

            int pinCount = pkg.ReadInt();
            for (int i = 0; i < pinCount; i++)
            {
                blob.Pins.Add(new PinEntry
                {
                    OwnerID = pkg.ReadLong(),
                    Name = pkg.ReadString(),
                    Pos = pkg.ReadVector3(),
                    Type = pkg.ReadInt(),
                    Checked = pkg.ReadBool(),
                    Author = pkg.ReadString(),
                });
            }

            return blob;
        }

        private static byte[] WriteBlob(TableBlob blob)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(blob.Version);
            pkg.Write(blob.Explored.Count);
            foreach (bool b in blob.Explored)
            {
                pkg.Write(b);
            }
            pkg.Write(blob.Pins.Count);
            foreach (PinEntry pin in blob.Pins)
            {
                pkg.Write(pin.OwnerID);
                pkg.Write(pin.Name);
                pkg.Write(pin.Pos);
                pkg.Write(pin.Type);
                pkg.Write(pin.Checked);
                pkg.Write(pin.Author);
            }
            return Utils.Compress(pkg.GetArray());
        }

        private static string DumpDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        private static void DumpMapTables()
        {
            List<ZDO> tables = FindMapTableZDOs();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Found {tables.Count} cartography table(s).");

            foreach (ZDO zdo in tables)
            {
                byte[] raw = zdo.GetByteArray(ZDOVars.s_data);
                sb.AppendLine();
                sb.AppendLine($"Table at {zdo.GetPosition()} (zdo {zdo.m_uid})");
                if (raw == null)
                {
                    sb.AppendLine("  no shared map data yet");
                    continue;
                }

                TableBlob blob;
                try
                {
                    blob = ReadBlob(raw);
                }
                catch (Exception e)
                {
                    sb.AppendLine($"  failed to parse: {e.Message}");
                    continue;
                }

                sb.AppendLine($"  {blob.Pins.Count} pin(s)");
                foreach (var group in blob.Pins.GroupBy(p => p.OwnerID))
                {
                    PinEntry sample = group.First();
                    sb.AppendLine($"  ownerID {group.Key}: {group.Count()} pin(s), author='{sample.Author}', sample name='{sample.Name}', sample pos={sample.Pos}");
                }
            }

            string path = Path.Combine(DumpDirectory(), "maptable-dump.txt");
            File.WriteAllText(path, sb.ToString());
            Console.instance?.Print($"wrote {path}");
            ZLog.Log("[SharedMapEditor] wrote " + path);
        }

        private static void PurgeOwnerPins(long ownerID)
        {
            long sessionID = (long)AccessTools.Field(typeof(ZDOMan), "m_sessionID").GetValue(ZDOMan.instance);
            List<ZDO> tables = FindMapTableZDOs();
            int totalRemoved = 0;

            foreach (ZDO zdo in tables)
            {
                byte[] raw = zdo.GetByteArray(ZDOVars.s_data);
                if (raw == null)
                {
                    continue;
                }

                TableBlob blob;
                try
                {
                    blob = ReadBlob(raw);
                }
                catch (Exception e)
                {
                    ZLog.LogWarning("[SharedMapEditor] failed to parse table at " + zdo.GetPosition() + ": " + e.Message);
                    continue;
                }

                int before = blob.Pins.Count;
                blob.Pins.RemoveAll(p => p.OwnerID == ownerID);
                int removed = before - blob.Pins.Count;
                if (removed <= 0)
                {
                    continue;
                }

                zdo.SetOwner(sessionID);
                zdo.Set(ZDOVars.s_data, WriteBlob(blob));
                totalRemoved += removed;
                ZLog.Log($"[SharedMapEditor] removed {removed} pin(s) from table at {zdo.GetPosition()}");
            }

            Console.instance?.Print($"removed {totalRemoved} pin(s) for owner {ownerID}");
            ZLog.Log($"[SharedMapEditor] done, removed {totalRemoved} pin(s) total for owner {ownerID}");
        }
    }
}
