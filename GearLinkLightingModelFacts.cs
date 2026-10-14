// SPDX-License-Identifier: MIT. Exact normal PID, transport and area interface facts;
// shared by the GUI and isolated worker. No native APIs or device I/O.
using System;

namespace RogControl
{
    public sealed class GearLinkLightingModelRecord
    {
        public readonly int Pid;
        public readonly string Family, Kind, Model, Transport;
        public readonly bool LightingOnly;
        private readonly int[] areas;
        public int[] Areas { get { return (int[])areas.Clone(); } }
        public int PrimaryArea { get { return areas[0]; } }
        internal GearLinkLightingModelRecord(int pid,string family,string kind,string model,string transport,bool lightingOnly,params int[] supportedAreas)
        { Pid=pid;Family=family;Kind=kind;Model=model;Transport=transport;LightingOnly=lightingOnly;areas=(int[])supportedAreas.Clone(); }
    }
    public static class GearLinkLightingModelFacts
    {
        private static readonly GearLinkLightingModelRecord[] records = {
            new GearLinkLightingModelRecord(6802,"p713","mouse","ROG HARPE ACE AIM LAB EDITION","usb",true,1),
            new GearLinkLightingModelRecord(6804,"p713","mouse","ROG HARPE ACE AIM LAB EDITION","rf",true,1),
            new GearLinkLightingModelRecord(6934,"p714","mouse","ROG KERIS II ACE","usb",true,0),
            new GearLinkLightingModelRecord(6936,"p714","mouse","ROG KERIS II ACE","rf",true,0),
            new GearLinkLightingModelRecord(7011,"p716","mouse","ROG HARPE ACE MINI","usb",true,1),
            new GearLinkLightingModelRecord(7013,"p716","mouse","ROG HARPE ACE MINI","rf",true,1),
            new GearLinkLightingModelRecord(7015,"p718","mouse","ROG HARPE ACE EXTREME","usb",true,1),
            new GearLinkLightingModelRecord(7017,"p718","mouse","ROG HARPE ACE EXTREME","rf",true,1),
            new GearLinkLightingModelRecord(7180,"p722","mouse","ROG KERIS II ORIGIN","usb",true,0,1,2),
            new GearLinkLightingModelRecord(7182,"p722","mouse","ROG KERIS II ORIGIN","rf",true,0,1,2),
            new GearLinkLightingModelRecord(7273,"p723","mouse","ROG HARPE II ACE","usb",true,0),
            new GearLinkLightingModelRecord(7275,"p723","mouse","ROG HARPE II ACE","rf",true,0),
            new GearLinkLightingModelRecord(7546,"p725","mouse","ROG HARPE II EXTREME EDITION20","usb",false,0),
            new GearLinkLightingModelRecord(7548,"p725","mouse","ROG HARPE II EXTREME EDITION20","rf",false,0),
            new GearLinkLightingModelRecord(6787,"m701","keyboard","ROG AZOTH","usb",true,0),
            new GearLinkLightingModelRecord(6789,"m701","keyboard","ROG AZOTH","rf",true,0),
            new GearLinkLightingModelRecord(6975,"m702","keyboard","ROG AZOTH EXTREME","usb",true,0),
            new GearLinkLightingModelRecord(6978,"m702","keyboard","ROG AZOTH EXTREME","rf",true,0),
            new GearLinkLightingModelRecord(7204,"m704","keyboard","ROG AZOTH X","usb",true,0),
            new GearLinkLightingModelRecord(7205,"m704","keyboard","ROG AZOTH X","rf",true,0),
            new GearLinkLightingModelRecord(7586,"m709","keyboard","ROG AZOTH EXTREME EDITION20","usb",false,0),
            new GearLinkLightingModelRecord(7588,"m709","keyboard","ROG AZOTH EXTREME EDITION20","rf",false,0)
        };
        public static GearLinkLightingModelRecord[] All {get{return (GearLinkLightingModelRecord[])records.Clone();}}
        public static GearLinkLightingModelRecord Lookup(int pid){foreach(var record in records)if(record.Pid==pid)return record;return null;}
        public static bool Supports(int pid){return Lookup(pid)!=null;}
        public static bool IsLightingOnly(int pid){var record=Lookup(pid);return record!=null&&record.LightingOnly;}
        public static int[] AreasForPid(int pid){var record=Lookup(pid);return record==null?new int[0]:record.Areas;}
        public static string KindForPid(int pid){var record=Lookup(pid);return record==null?"unknown":record.Kind;}
        public static string ModelForPid(int pid){var record=Lookup(pid);return record==null?null:record.Model;}
        public static string TransportForPid(int pid){var record=Lookup(pid);return record==null?"unknown":record.Transport;}
    }
}
