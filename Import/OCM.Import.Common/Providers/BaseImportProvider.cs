﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OCM.API.Common.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;


namespace OCM.Import.Providers
{
    public enum ExportType
    {
        XML,
        CSV,
        API,
        JSON,
        POIModelList
    }

    public class CommonImportRefData
    {
        public SubmissionStatusType SubmissionStatus_Imported { get; set; }

        public SubmissionStatusType SubmissionStatus_DelistedDupe { get; set; }

        public StatusType Status_Operational { get; set; }

        public StatusType Status_NonOperational { get; set; }

        public StatusType Status_Unknown { get; set; }

        public StatusType Status_PlannedForFuture { get; set; }

        public UsageType UsageType_Public { get; set; }

        public UsageType UsageType_Private { get; set; }

        public UsageType UsageType_PublicPayAtLocation { get; set; }

        public UsageType UsageType_PublicMembershipRequired { get; set; }

        public UsageType UsageType_PublicNoticeRequired { get; set; }

        public OperatorInfo Operator_Unknown { get; set; }

        public ChargerType ChrgLevel_1 { get; set; }

        public ChargerType ChrgLevel_2 { get; set; }

        public ChargerType ChrgLevel_3 { get; set; }

        public ConnectionType ConnectionType_Unknown { get; set; }

        public ConnectionType ConnectionType_J1772 { get; set; }

        public ConnectionType ConnectionType_CHADEMO { get; set; }

        public ConnectionType ConnectionType_Type2Mennekes { get; set; }

        public CommonImportRefData(CoreReferenceData coreRefData)
        {
            SubmissionStatus_Imported = coreRefData.SubmissionStatusTypes.First(s => s.ID == 100); //imported and published
            SubmissionStatus_DelistedDupe = coreRefData.SubmissionStatusTypes.First(s => s.ID == 1001); //delisted duplicate

            Status_Unknown = coreRefData.StatusTypes.First(os => os.ID == 0);
            Status_Operational = coreRefData.StatusTypes.First(os => os.ID == 50);
            Status_NonOperational = coreRefData.StatusTypes.First(os => os.ID == 100);
            Status_PlannedForFuture = coreRefData.StatusTypes.First(os => os.ID == 150);

            UsageType_Public = coreRefData.UsageTypes.First(u => u.ID == 1);
            UsageType_Private = coreRefData.UsageTypes.First(u => u.ID == 2);
            UsageType_PublicPayAtLocation = coreRefData.UsageTypes.First(u => u.ID == 5);
            UsageType_PublicMembershipRequired = coreRefData.UsageTypes.First(u => u.ID == 4);
            UsageType_PublicNoticeRequired = coreRefData.UsageTypes.First(u => u.ID == 7);

            Operator_Unknown = coreRefData.Operators.First(opUnknown => opUnknown.ID == 1);

            ChrgLevel_1 = coreRefData.ChargerTypes.First(c => c.ID == 1);
            ChrgLevel_2 = coreRefData.ChargerTypes.First(c => c.ID == 2);
            ChrgLevel_3 = coreRefData.ChargerTypes.First(c => c.ID == 3);

            ConnectionType_Unknown = coreRefData.ConnectionTypes.First(c => c.ID == 0);
            ConnectionType_J1772 = coreRefData.ConnectionTypes.First(c => c.ID == 1);
            ConnectionType_CHADEMO = coreRefData.ConnectionTypes.First(c => c.ID == 2);
            ConnectionType_Type2Mennekes = coreRefData.ConnectionTypes.First(c => c.ID == 25);
        }
    }

    public class BaseImportProvider
    {
        public int DataProviderID { get; set; }

        public string ProviderName { get; set; }

        public bool IsAutoRefreshed { get; set; }

        public bool IsStringData { get; set; }

        public bool UseCustomReader { get; set; }

        public string AutoRefreshURL { get; set; }

        public string InputData { get; set; }

        public string InputPath { get; set; }

        public string OutputNamePrefix { get; set; }

        public ExportType ExportType { get; set; }

        public bool IsProductionReady { get; set; }

        public bool IncludeInvalidPOIs { get; set; }

        /// <summary>
        /// If true, import processing will look for duplicate POIs and merge the equipment listed into one master POI
        /// </summary>
        public bool MergeDuplicatePOIEquipment { get; set; }

        /// <summary>
        /// If true, POI will not be considered a duplicate if the Operator is different
        /// </summary>
        public bool AllowDuplicatePOIWithDifferentOperator { get; set; }

        /// <summary>
        /// Intended to allow duplicates in a data source, does not currently work as intended (dedupe does too much other work)
        /// </summary>
        public bool SkipDeduplication { get; set; }

        public string DataAttribution { get; set; }

        private Encoding _sourceEncoding = null;

        public Encoding SourceEncoding
        {
            get { return _sourceEncoding; }
            set
            {
                _sourceEncoding = value;
                if (webClient != null) webClient.Encoding = _sourceEncoding;
            }
        }

        /// <summary>
        /// If specified, default http method will be POST instead of GET
        /// </summary>
        public string HTTPPostVariables { get; set; }

        public string APIKeyPrimary { get; set; }

        public string APIKeySecondary { get; set; }

        public CommonImportRefData ImportRefData { get; set; }

        public DataProvider DefaultDataProvider { get; set; }

        protected CookieAwareWebClient webClient = new CookieAwareWebClient();

        public bool ImportInitialisationRequired { get; set; }

        public string ProcessingLog { get; set; }

        public BaseImportProvider()
        {
            ProviderName = "None";
            OutputNamePrefix = "output";
            ExportType = Providers.ExportType.XML;
            IsProductionReady = false;
            SourceEncoding = UTF32Encoding.UTF8; //default to ANSI 1252 western europe

            webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.3; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/39.0.2171.71 Safari/537.36");
            webClient.Encoding = SourceEncoding;
            this.IsStringData = true;
            this.ProcessingLog = "";
        }

        public string GetProviderName()
        {
            return ProviderName;
        }

        public void Log(string message)
        {
            this.ProcessingLog += "\r\n[" + ProviderName + "]: " + message;

            System.Diagnostics.Debug.WriteLine(message);
        }

        public EVSE GetEVSEFromCP(ChargePoint cp)
        {
            EVSE e = new EVSE();
            e.Title = cp.AddressInfo.Title;
            e.Latitude = cp.AddressInfo.Latitude;
            e.Longitude = cp.AddressInfo.Longitude;

            return e;
        }

        public string ConvertUppercaseToTitleCase(string val)
        {
            if (val.ToUpper() == val)
            {
                TextInfo t = Thread.CurrentThread.CurrentCulture.TextInfo;

                return t.ToTitleCase(val.ToLower()).Trim();
            }
            else
            {
                return val.Trim();
            }
        }

        public string RemoveFormattingCharacters(string val)
        {
            val = val.Replace("\t", " ");
            val = val.Replace("\n", "");
            val = val.Replace("\r", "");

            val = val.Replace("<br>", " ");
            val = val.Trim();
            return val;
        }

        public string CalculateMD5Hash(string input)
        {
            // from http://blogs.msdn.com/b/csharpfaq/archive/2006/10/09/how-do-i-calculate-a-md5-hash-from-a-string_3f00_.aspx
            // step 1, calculate MD5 hash from input
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
            byte[] hash = md5.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            return sb.ToString();
        }

        public bool LoadInputFromURL(string url)
        {
            try
            {
                if (String.IsNullOrEmpty(HTTPPostVariables))
                {
                    //get
                    InputData = webClient.DownloadString(url);
                }
                else
                {
                    //post
                    InputData = webClient.UploadString(url, HTTPPostVariables);
                }

                // cache result if we received a response
                /*if (InputData.Length > 512)
                {
                    var cacheFile = InputPath + "\\cache_" + ProviderName + ".dat";
                    System.IO.File.WriteAllText(cacheFile, InputData);
                }*/

                return true;
            }
            catch (Exception)
            {
                Log(": Failed to fetch input from url :" + url);
                return false;
            }
        }

        public bool LoadInputFromFile(string path)
        {
            try
            {
                string fullPath = System.IO.Path.GetFullPath(path);
                using (StreamReader sr = new StreamReader(fullPath, true))
                {
                    InputData = sr.ReadToEnd();
                }
                return true;
            }
            catch (Exception)
            {
                Log(": Failed to read input file :" + path);
                return false;
            }
        }

        public void SaveInputFile(string path)
        {
            try
            {
                System.IO.File.WriteAllText(path, this.InputData);
            }
            catch (Exception)
            {
                Log("Failed to saev input file:" + path);
            }
        }

        public bool ExportXMLFile(List<ChargePoint> dataList, string outputPath)
        {
            //not implemented
            return false;
        }

        public bool ExportCSVFile(List<EVSE> dataList, string outputPath)
        {
            //not implemented
            return false;
        }

        public bool ExportXMLFile(List<EVSE> dataList, string outputPath)
        {
            string output = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n";
            output += "<feed xmlns=\"http://www.w3.org/2005/Atom\" xmlns:georss=\"http://www.georss.org/georss\" xmlns:evse=\"http://www.openchargemap.org/RssEVSE\">\r\n";
            string itemTemplate = "\t<entry><title>{title}</title><link>{link}</link><id>{id}</id><updated>{updated}</updated><content>{content}</content><georss:point>{latitude} {longitude}</georss:point><evse:status>{status}</evse:status>{extendedattributes}</entry>\r\n";

            foreach (var item in dataList)
            {
                string entry = itemTemplate;
                entry = entry.Replace("{title}", item.Title);
                entry = entry.Replace("{link}", item.Link);
                entry = entry.Replace("{id}", item.ID);
                entry = entry.Replace("{updated}", item.Updated.ToUniversalTime().ToString());
                entry = entry.Replace("{content}", item.Content);
                entry = entry.Replace("{latitude}", (item.Latitude != null ? item.Latitude.ToString() : ""));
                entry = entry.Replace("{longitude}", (item.Longitude != null ? item.Longitude.ToString() : ""));
                entry = entry.Replace("{status}", item.Status);

                string extendedAttributes = "<evse:attributes>\r\n";
                if (item.ExtendedAttributes != null)
                {
                    foreach (var extAttr in item.ExtendedAttributes)
                    {
                        extendedAttributes += "<evse:attribute name=\"" + extAttr.Name + "\">" + extAttr.Value + "</evse:attribute>";
                    }
                }
                extendedAttributes += "</evse:attributes>\r\n";
                entry = entry.Replace("{extendedattributes}", extendedAttributes);
                output += entry;
            }

            output += "</feed>";

            System.IO.File.WriteAllText(outputPath, output);

            return true;
        }

        public string OutputExtendedAttribute(EVSE evse, string attributeName)
        {
            if (evse.ExtendedAttributes != null)
            {
                if (evse.ExtendedAttributes.Exists(a => a.Name == attributeName))
                {
                    return evse.ExtendedAttributes.First(a => a.Name == attributeName).Value.ToString();
                }
            }
            return "";
        }

        public void PopulateExtendedAttribute(EVSE evse, JToken item, string outputName, string inputName)
        {
            if (evse.ExtendedAttributes == null) evse.ExtendedAttributes = new List<ExtendedAttribute>();

            if (item[inputName] != null)
            {
                evse.ExtendedAttributes.Add(new ExtendedAttribute { Name = outputName, Value = item[inputName].ToString() });
            }
        }

        public string GetConnectionFieldOutput(List<ConnectionInfo> connections, string fieldName, int connectionNum)
        {
            if (connections == null) return "";
            if (connections.Count < connectionNum) return "";
            var connection = connections[connectionNum - 1];

            if (fieldName.EndsWith("_Type"))
            {
                if (connection.ConnectionType != null)
                {
                    return connection.ConnectionType.Title;
                }
                else
                {
                    return "";
                }
            }

            if (fieldName.EndsWith("_Level"))
            {
                if (connection.Level != null)
                {
                    return connection.Level.Title;
                }
                else
                {
                    return "";
                }
            }

            if (fieldName.EndsWith("_Amps") && connection.Amps != null) return connection.Amps.ToString();
            if (fieldName.EndsWith("_Volts") && connection.Voltage != null) return connection.Voltage.ToString();
            if (fieldName.EndsWith("_Quantity") && connection.Quantity != null) return connection.Quantity.ToString();

            return "";
        }

        public string GetNullableStringOutput(string val)
        {
            if (val == null) return "";
            else return val.ToString();
        }

        public bool ExportJSONFile(List<ChargePoint> dataList, string outputPath)
        {
            var json = JsonConvert.SerializeObject(dataList, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            System.IO.File.WriteAllText(outputPath, json, new UnicodeEncoding());
            return true;
        }

        public bool ExportCSVFile(List<ChargePoint> dataList, string outputPath)
        {
            string output = "";
            string delimiter = "\t";

            string[] fields = {
                "ID",
                "LocationTitle",
                "AddressLine1",
                "AddressLine2",
                "Town",
                "StateOrProvince",
                "Postcode",
                "Country",
                "Latitude",
                "Longitude",
                "Addr_ContactTelephone1",
                "Addr_ContactTelephone2",
                "Addr_ContactEmail",
                "Addr_AccessComments",
                "Addr_GeneralComments",
                "Addr_RelatedURL",
                "UsageType",
                "NumberOfPoints",
                "GeneralComments",
                "DateLastConfirmed",
                "StatusType",
                "DateLastStatusUpdate",
                "UsageCost",
                "Connection1_Type",
                "Connection1_Amps",
                "Connection1_Volts",
                "Connection1_Level",
                "Connection1_Quantity",
                "Connection2_Type",
                "Connection2_Amps",
                "Connection2_Volts",
                "Connection2_Level",
                "Connection2_Quantity",
                "Connection3_Type",
                "Connection3_Amps",
                "Connection3_Volts",
                "Connection3_Level",
                "Connection3_Quantity"
            };

            foreach (var heading in fields)
            {
                output += heading + delimiter;
            }
            output += "\r\n";

            foreach (var item in dataList)
            {
                //output value for each field if present
                foreach (var heading in fields)
                {
                    switch (heading)
                    {
                        case "ID":
                            output += item.DataProvidersReference;
                            break;

                        case "LocationTitle":
                            output += GetNullableStringOutput(item.AddressInfo.Title);
                            break;

                        case "AddressLine1":
                            output += GetNullableStringOutput(item.AddressInfo.AddressLine1);
                            break;

                        case "AddressLine2":
                            output += GetNullableStringOutput(item.AddressInfo.AddressLine2);
                            break;

                        case "Town":
                            output += GetNullableStringOutput(item.AddressInfo.Town);
                            break;

                        case "StateOrProvince":
                            output += GetNullableStringOutput(item.AddressInfo.StateOrProvince);
                            break;

                        case "Postcode":
                            output += GetNullableStringOutput(item.AddressInfo.Postcode);
                            break;

                        case "Country":
                            output += (item.AddressInfo.Country != null ? item.AddressInfo.Country.Title : "");
                            break;

                        case "Latitude":
                            output += item.AddressInfo.Latitude.ToString();
                            break;

                        case "Longitude":
                            output += item.AddressInfo.Longitude.ToString();
                            break;

                        case "Addr_ContactTelephone1":
                            output += GetNullableStringOutput(item.AddressInfo.ContactTelephone1);
                            break;

                        case "Addr_ContactTelephone2":
                            output += GetNullableStringOutput(item.AddressInfo.ContactTelephone2);
                            break;

                        case "Addr_ContactEmail":
                            output += GetNullableStringOutput(item.AddressInfo.ContactEmail);
                            break;

                        case "Addr_AccessComments":
                            output += GetNullableStringOutput(item.AddressInfo.AccessComments);
                            break;

                        case "Addr_GeneralComments":
                            output += GetNullableStringOutput(item.AddressInfo.GeneralComments);
                            break;

                        case "Addr_RelatedURL":
                            output += GetNullableStringOutput(item.AddressInfo.RelatedURL);
                            break;

                        case "NumberOfPoints":
                            output += item.NumberOfPoints;
                            break;

                        case "GeneralComments":
                            output += item.GeneralComments;
                            break;

                        case "DateLastConfirmed":
                            output += item.DateLastConfirmed.ToString();
                            break;

                        case "StatusType":
                            output += (item.StatusType != null ? item.StatusType.Title : "");
                            break;

                        case "DateLastStatusUpdate":
                            output += item.DateLastStatusUpdate.ToString();
                            break;

                        case "UsageCost":
                            output += GetNullableStringOutput(item.UsageCost);
                            break;

                        case "Connection1_Type":
                            output += GetConnectionFieldOutput(item.Connections, "_Type", 1);
                            break;

                        case "Connection1_Amps":
                            output += GetConnectionFieldOutput(item.Connections, "_Amps", 1);
                            break;

                        case "Connection1_Volts":
                            output += GetConnectionFieldOutput(item.Connections, "_Volts", 1);
                            break;

                        case "Connection1_Level":
                            output += GetConnectionFieldOutput(item.Connections, "_Level", 1);
                            break;

                        case "Connection1_Quantity":
                            output += GetConnectionFieldOutput(item.Connections, "_Quantity", 1);
                            break;

                        case "Connection2_Type":
                            output += GetConnectionFieldOutput(item.Connections, "_Type", 2);
                            break;

                        case "Connection2_Amps":
                            output += GetConnectionFieldOutput(item.Connections, "_Amps", 2);
                            break;

                        case "Connection2_Volts":
                            output += GetConnectionFieldOutput(item.Connections, "_Volts", 2);
                            break;

                        case "Connection2_Level":
                            output += GetConnectionFieldOutput(item.Connections, "_Level", 2);
                            break;

                        case "Connection2_Quantity":
                            output += GetConnectionFieldOutput(item.Connections, "_Quantity", 2);
                            break;

                        case "Connection3_Type":
                            output += GetConnectionFieldOutput(item.Connections, "_Type", 3);
                            break;

                        case "Connection3_Amps":
                            output += GetConnectionFieldOutput(item.Connections, "_Amps", 3);
                            break;

                        case "Connection3_Volts":
                            output += GetConnectionFieldOutput(item.Connections, "_Volts", 3);
                            break;

                        case "Connection3_Level":
                            output += GetConnectionFieldOutput(item.Connections, "_Level", 3);
                            break;

                        case "Connection3_Quantity":
                            output += GetConnectionFieldOutput(item.Connections, "_Quantity", 3);
                            break;
                    }
                    output += delimiter;
                }
                output += "\r\n";
            }

            System.IO.File.WriteAllText(outputPath, output, new UnicodeEncoding());
            return true;
        }

        public bool IsConnectionInfoBlank(ConnectionInfo c)
        {
            if (c.ConnectionTypeID == null && c.ConnectionType == null && c.Amps == null & c.Voltage == null && c.Level == null && c.Quantity == null)
            {
                return true;
            }
            return false;
        }

        public static bool IsPOIValidForImport(ChargePoint poi, bool includeCountryValidation = false)
        {
            if (poi == null) return false;
            if (poi.AddressInfo == null) return false;
            if (String.IsNullOrEmpty(poi.AddressInfo.Title)) return false;
            if (String.IsNullOrEmpty(poi.AddressInfo.AddressLine1) && String.IsNullOrEmpty(poi.AddressInfo.Postcode)) return false;
            if (poi.AddressInfo.Latitude == 0 || poi.AddressInfo.Longitude == 0) return false;

            if (poi.AddressInfo.Latitude < -90 || poi.AddressInfo.Latitude > 90) return false;
            if (poi.AddressInfo.Longitude < -180 || poi.AddressInfo.Longitude > 180) return false;

            if (poi.Connections == null || !poi.Connections.Any()) return false;
            if (poi.AddressInfo.Title.Contains("????")) return false; //imports with invalid character encoding
            if (includeCountryValidation && (poi.AddressInfo.CountryID == null && poi.AddressInfo.Country == null)) return false;
            return true;
        }

        public static double? ComputePowerkWForConnectionInfo(ConnectionInfo cinfo)
        {
            var powerkW = cinfo.PowerKW;

            if (cinfo.Amps > 0 && cinfo.Voltage > 0)
            {
                if (cinfo.CurrentTypeID == null || cinfo.CurrentTypeID == (int)StandardCurrentTypes.SinglePhaseAC || cinfo.CurrentTypeID == (int)StandardCurrentTypes.DC)
                {
                    powerkW = ((double)cinfo.Amps * (double)cinfo.Voltage / 1000);
                }
                else
                {
                    powerkW = ((double)cinfo.Amps * (double)cinfo.Voltage * 1.732 / 1000);
                }

                powerkW = Math.Round((double)powerkW, 1);
            }

            return powerkW;
        }
    }
}