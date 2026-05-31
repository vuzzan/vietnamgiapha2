using MahApps.Metro.Controls;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace vietnamgiapha
{
    public class Database
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger("m0");

        private static void ReportParseError(string message, Exception ex = null)
        {
            if (ex != null)
            {
                log.Error(message, ex);
            }
            else
            {
                log.Error(message);
            }

            // Parse JSON chạy background — không gọi MessageBox ngoài STA/UI.
            if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == true)
            {
                MessageBox.Show(message, "Có lỗi");
            }
        }
        public static bool SaveJsonAs(GiaPhaViewModel gp)
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.DefaultExt = ".json";
                saveFileDialog.Filter = "JSON files (*.json)|*.json";
                if (saveFileDialog.ShowDialog() == true)
                {
                    if (File.Exists(saveFileDialog.FileName))
                    {
                        if (MessageBox.Show("Có muốn lưu đè vào file có sẵn: " + saveFileDialog.FileName,
                            "Xác nhận", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                        {
                            //gp.FileName = saveFileDialog.FileName;
                        }
                        else
                        {
                            log.Info("Hủy chọn được file để lưu." + saveFileDialog.FileName);
                            return false;
                        }
                    }
                    else
                    {
                        //gp.FileName = saveFileDialog.FileName;
                    }
                    log.Info("Begin save file: " + saveFileDialog.FileName);
                    System.IO.File.WriteAllText(saveFileDialog.FileName, gp.ToJson());
                    MessageBox.Show("Lưu File: " + saveFileDialog.FileName, "Success");
                    log.Info("End save file: " + saveFileDialog.FileName);
                }
                else
                {
                    log.Info("Hủy chọn được file để lưu.");
                    return false;
                }
                
                //
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Có lỗi : " + ex.Message, "Có lỗi");
                log.Error(ex);
            }
            return false;
        }
        public static bool SaveJson(GiaPhaViewModel gpView, string fileNameNew="")
        {
            try
            {
                if (fileNameNew.Length == 0)
                {
                    if (gpView.GP.FileName.Length == 0)
                    {
                        SaveFileDialog saveFileDialog = new SaveFileDialog();
                        saveFileDialog.DefaultExt = ".json";
                        saveFileDialog.Filter = "JSON files (*.json)|*.json";
                        if (saveFileDialog.ShowDialog() == true)
                        {
                            if (File.Exists(saveFileDialog.FileName))
                            {
                                if (MessageBox.Show("Có muốn lưu đè vào file có sẵn: " + saveFileDialog.FileName,
                                    "Xác nhận", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                                {
                                    gpView.GP.FileName = saveFileDialog.FileName;
                                }
                                else
                                {
                                    log.Info("Hủy chọn được file để lưu.");
                                    return false;
                                }
                            }
                            else
                            {
                                gpView.GP.FileName = saveFileDialog.FileName;
                            }
                        }
                        else
                        {
                            log.Info("Không chọn được file để lưu.");
                            return false;
                        }

                    }
                    else
                    {
                        //log.Info("File để lưu: " + gpView.GP.FileName);
                    }
                    //log.Info("Begin save file: " + gpView.GP.FileName);
                    System.IO.File.WriteAllText(gpView.GP.FileName, gpView.ToJson());
                    //log.Info("End save file: " + gpView.GP.FileName);
                }
                else
                {
                    string defaultSaveFolder = ConfigurationManager.AppSettings["defaultSaveFolder"];
                    //log.Debug("Begin save file: " + defaultSaveFolder + "\\" + fileNameNew);
                    System.IO.File.WriteAllText(defaultSaveFolder + "\\" + fileNameNew, gpView.ToJson());
                    //log.Debug("End save file: " + defaultSaveFolder + "\\" + fileNameNew);
                }
                gpView.GP.FileNameUpdate = DateTime.Now;
                //
                return true;
            }
            catch (Exception ex)
            {
                //
                log.Error(ex);
                //
                string defaultSaveFolder = ConfigurationManager.AppSettings["defaultSaveFolder"];
                gpView.GP.FileName = defaultSaveFolder + "\\" + gpView.GP.Username.Replace(" ", "_") + "_" + ".json";
                //log.Debug("Begin save file: " + gpView.GP.FileName);
                System.IO.File.WriteAllText(gpView.GP.FileName, gpView.ToJson());
                //log.Debug("End save file: " + gpView.GP.FileName);
                //
                return true;
            }
            return false;
        }
        public static async Task<string> UploadWeb(string u, string p, string jsonBody)
        {
            try
            {
                var client = new HttpClient();

                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/json;q=0.9,image/avif,image/webp,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.0.0 Safari/537.36");
                //https://vietnamgiapha.com/export/index2c.php?u=nghia&p=shogun
                string url = "https://vietnamgiapha.com/export/index2c.php?f=u&u=" + u + "&p=" + p;
                log.Info("Upload " + url);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                log.Info("Upload: " + responseBody.Length);
                log.Info(responseBody);
                return responseBody;
            }
            catch (Exception ex)
            {
                log.Error("ERROR: Upload " + ex.Message);
                return null;
            }
        }
        private static async Task<string> DownloadWeb(string u, string p)
        {
            try
            {

                var client = new HttpClient();

                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.0.0 Safari/537.36");
                string url = "https://vietnamgiapha.com/export/index2c.php?u="+u+"&p="+p;
                log.Info("Download " + url);
                var response = await client.GetAsync(url).ConfigureAwait(false);
                string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                log.Info("Download: " + responseBody.Length);
                //if(responseBody.Length < 200)
                {
                    log.Info(responseBody);
                }
                return responseBody;
            }
            catch (Exception ex)
            {
                log.Error("ERROR: Download " + ex.Message);
                return null;
            }
        }
        public static async Task<GiaphaInfo> Download(string u, string p)
        {
            try
            {
                u = Util.Unicode2ASCII(u);
                p = Util.Unicode2ASCII(p);
                string json = await DownloadWeb(u, p).ConfigureAwait(false);
                log.Info("Download json length: " + (json?.Length ?? 0));
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                // JSON lớn: parse ở background để không khóa STA/UI thread.
                return await Task.Run(() =>
                {
                    JsonObject objData = (JsonObject)JsonObject.Parse(json);
                    return ParseJsonGiaPha(objData);
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.Error("Có lỗi Download Gia Phả.");
                log.Error(ex);
                throw;
            }
        }
        public static GiaphaInfo ParseJsonGiaPha(JsonObject objData)
        {
            try
            {
                //JsonObject objData = (JsonObject)JsonObject.Parse(jsonString);
                if (Convert.ToInt32(objData["code"].ToString()) != 0)
                {
                    // NO DATA
                    log.Error("ERROR: Download " + objData["msg"].ToString());
                    throw new Exception(objData["msg"].ToString());
                }

                JsonArray array = (JsonArray)objData["data"];
                if (array == null)
                {
                    ReportParseError("Có lỗi gia phả không có data");
                    throw new Exception("ERROR: Download - No data");
                    //return new GiaphaInfo();
                }
                GiaphaInfo gp = new GiaphaInfo();
                //RootFamily root = new RootFamily();
                gp.GiaphaId = Convert.ToInt32(array[0].ToString());
                gp.GiaphaName = array[1].ToString();
                gp.GiaphaNameRoot = vietnamgiapha.Util.GetFirstWord(gp.GiaphaName);
                gp.PhaKy = vietnamgiapha.Util.Base64Decode(array[3].ToString());
                gp.PhaKy = vietnamgiapha.Util.StripHTML(gp.PhaKy);

                gp.ThuyTo = vietnamgiapha.Util.Base64Decode(array[4].ToString());
                gp.ThuyTo = vietnamgiapha.Util.StripHTML(gp.ThuyTo);

                gp.Tocuoc = vietnamgiapha.Util.Base64Decode(array[5].ToString());
                gp.Tocuoc = vietnamgiapha.Util.StripHTML(gp.Tocuoc);
                gp.HuongHoa = vietnamgiapha.Util.Base64Decode(array[6].ToString());
                gp.HuongHoa = vietnamgiapha.Util.StripHTML(gp.HuongHoa);


                gp.RF_OTAI = array[7].ToString();
                gp.RF_DAYS = array[8].ToString();
                gp.RF_CHANNGON = array[9].ToString();
                gp.Username = array.Count > 10 ? array[10].ToString() : "";
                gp.Password = "";
                if (array.Count > 11 && array[11].ToString().Length > 0)
                {
                    try
                    {
                        gp.Password = Util.Base64Decode(array[11].ToString());
                    }
                    catch(Exception exx) {
                        gp.Password = "";
                    }
                }

                if (array.Count > GiaPhaRender.PhaDoSvgCatalog.RootJsonSvgCatalogIndex)
                {
                    var catalogNode = array[GiaPhaRender.PhaDoSvgCatalog.RootJsonSvgCatalogIndex];
                    if (catalogNode is JsonArray catalogArray)
                    {
                        gp.SvgShapesById = ParseSvgCatalogFromJsonArray(catalogArray);
                    }

                    if (gp.SvgShapesById == null || gp.SvgShapesById.Count == 0)
                    {
                        string catalogJson = catalogNode?.ToString() ?? "[]";
                        gp.SvgShapesById = GiaPhaRender.PhaDoSvgCatalog.ParseJsonArray(catalogJson);
                    }
                }

                JsonArray arrayFamily = (JsonArray)array[2];

                // THUY TO
                FamilyInfo family = new FamilyInfo();
                if (GetFamily(family, arrayFamily) == true)
                {
                    //OK
                }

                gp.familyRoot = family;
                // Check all to get the Family Root Name
                if (family.ListPerson.Count > 0)
                {
                    String firstName = family.ListPerson[0].MANS_NAME_HUY;
                    gp.GiaphaNameRoot = vietnamgiapha.Util.GetFirstWord(firstName);
                }
                // Update main person
                UpdateFamilyMain(gp.familyRoot, gp.GiaphaNameRoot);
                log.Info("Begin load file Gia Phả. Thành công");
                return gp;
            }
            catch(Exception ex)
            {
                ReportParseError(ex.Message, ex);
                log.Info("Begin load Gia Phả. Có lỗi");
            }
            return null;
        }

        /// <summary>Đọc file .json gia phả — parse từ byte[], không ghép chuỗi wrapper (tiết kiệm RAM).</summary>
        public static GiaphaInfo FromJson(string jsonFile)
        {
            try
            {
                log.Info("Begin load file Gia Phả: " + jsonFile);
                byte[] utf8Bytes = File.ReadAllBytes(jsonFile);
                JsonNode rootNode = JsonNode.Parse(utf8Bytes);
                JsonObject objData = WrapGiaPhaJsonRoot(rootNode);
                if (objData == null)
                {
                    ReportParseError("Định dạng file JSON gia phả không hợp lệ.");
                    return null;
                }

                return ParseJsonGiaPha(objData);
            }
            catch (Exception ex)
            {
                ReportParseError("Có lỗi mở file: " + ex.Message, ex);
                log.Error("Có lỗi load file Gia Phả: " + jsonFile + ".");
            }
            return null;
        }

        private static JsonObject WrapGiaPhaJsonRoot(JsonNode rootNode)
        {
            if (rootNode == null)
            {
                return null;
            }

            if (rootNode is JsonArray rootArray)
            {
                return new JsonObject
                {
                    ["code"] = 0,
                    ["msg"] = " ",
                    ["data"] = rootArray
                };
            }

            if (rootNode is JsonObject rootObject && rootObject["data"] != null)
            {
                return rootObject;
            }

            return null;
        }
        private static bool UpdateFamilyMain(FamilyInfo family, String rootName)
        {
            try
            {
                // Item 3: Child family
                
                if (family.ListPerson.Count >0)
                {
                    ObservableCollection<PersonInfo> _listPerson = new ObservableCollection<PersonInfo>();
                    if (family.ListPerson.Count == 1)
                    {
                        // YES
                        family.ListPerson[0].IsMainPerson = 0;
                        if (family.ListPerson[0].MANS_NAME_HUY.ToLower().Contains(rootName.ToLower()))
                        {
                            family.ListPerson[0].IsMainPerson = 1;
                        }
                        _listPerson.Add(family.ListPerson[0]);
                    }
                    else
                    {
                        int countMain = 0;
                        for (int i = 0; i < family.ListPerson.Count; i++)
                        {
                            // Add first person
                            if (family.ListPerson[i].MANS_NAME_HUY.ToLower().Contains(rootName.ToLower()))
                            {
                                // YES
                                family.ListPerson[i].IsMainPerson = 1;
                                _listPerson.Add(family.ListPerson[i]);
                                countMain++;
                                break;
                            }
                        }
                        //
                        // If count main =0, set defualt 1
                        if (countMain == 0)
                        {
                            if (family.ListPerson.Count > 0)
                            {
                                family.ListPerson[0].IsMainPerson = 1;
                                _listPerson.Add(family.ListPerson[0]);
                            }
                        }
                        // add other
                        for (int i = 0; i < family.ListPerson.Count; i++)
                        {
                            // Add first person
                            if (_listPerson.Count > 0 && 
                                (family.ListPerson[i].MANS_ID != _listPerson[0].MANS_ID
                                    || family.ListPerson[i].MANS_NAME_HUY != _listPerson[0].MANS_NAME_HUY
                                )
                            )
                            {
                                family.ListPerson[i].IsMainPerson = 0;
                                _listPerson.Add(family.ListPerson[i]);
                            }
                            if(family.ListPerson[i].IsMainPerson==1)
                            {
                                countMain++;
                            }
                        }
                        
                    }
                    
                    //
                    family.ListPerson = _listPerson;
                }
                //
                for (int i = 0; i < family.FamilyChildren.Count; i++)
                {
                    family.FamilyChildren[i].FamilyOrder = (i + 1);
                    UpdateFamilyMain(family.FamilyChildren[i], rootName);
                }
            }
            catch (Exception ex)
            {
                log.Error("Có lỗi update family main.");
                log.Error(ex);
            }
            return true;
        }
        private static Dictionary<string, GiaPhaRender.PhaDoSvgShape> ParseSvgCatalogFromJsonArray(JsonArray catalogArray)
        {
            var result = new Dictionary<string, GiaPhaRender.PhaDoSvgShape>(StringComparer.Ordinal);
            if (catalogArray == null)
            {
                return result;
            }

            for (int i = 0; i < catalogArray.Count; i++)
            {
                var row = catalogArray[i] as JsonArray;
                if (row == null || row.Count < 4)
                {
                    continue;
                }

                string svgId = JsonValueToString(row[0]);
                string svgBase64 = JsonValueToString(row[1]);
                if (string.IsNullOrWhiteSpace(svgId) || string.IsNullOrWhiteSpace(svgBase64))
                {
                    continue;
                }

                double vbW = 100;
                double vbH = 80;
                double.TryParse(JsonValueToString(row[2]), NumberStyles.Float, CultureInfo.InvariantCulture, out vbW);
                double.TryParse(JsonValueToString(row[3]), NumberStyles.Float, CultureInfo.InvariantCulture, out vbH);

                result[svgId] = new GiaPhaRender.PhaDoSvgShape
                {
                    SvgId = svgId,
                    SvgBase64 = svgBase64,
                    ViewBoxWidth = vbW,
                    ViewBoxHeight = vbH
                };
            }

            return result;
        }

        private static string JsonValueToString(object jsonValue)
        {
            if (jsonValue == null)
            {
                return "";
            }

            string s = jsonValue.ToString();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
            {
                return s.Substring(1, s.Length - 2)
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");
            }

            return s;
        }

        private static bool GetFamily(FamilyInfo family, JsonArray arrayFamily)
        {
            try
            {
                // Item 1 : Family info 
                JsonArray arrayFamilyInfo = (JsonArray)arrayFamily[0];
                family.FamilyId = Convert.ToInt32(arrayFamilyInfo[0].ToString());
                family.FamilyOrder = Convert.ToInt32(arrayFamilyInfo[2].ToString());
                family.FamilyLevel = Convert.ToInt32(arrayFamilyInfo[1].ToString());
                family.FamilyUp = Convert.ToInt32(arrayFamilyInfo[3].ToString());
                
                // FamilyNew = 0 : Exist in Web DB - 1: New Family ID
                family.FamilyNew = 0;
                if (arrayFamilyInfo.Count > 4)
                {
                    family.FamilyNew = Convert.ToInt32(arrayFamilyInfo[4].ToString());
                }
                if (arrayFamilyInfo.Count > 5)
                {
                    family.X = Convert.ToInt32(arrayFamilyInfo[5].ToString());
                    family.Y = Convert.ToInt32(arrayFamilyInfo[6].ToString());
                    family.Width = Convert.ToInt32(arrayFamilyInfo[7].ToString());
                    family.Height = Convert.ToInt32(arrayFamilyInfo[8].ToString());
                }

                if (arrayFamilyInfo.Count > GiaPhaRender.PhaDoSvgCatalog.FamilyInfoSvgIdIndex)
                {
                    string svgId = arrayFamilyInfo[GiaPhaRender.PhaDoSvgCatalog.FamilyInfoSvgIdIndex].ToString();
                    if (!string.IsNullOrWhiteSpace(svgId))
                    {
                        family.PhaDoShapeSvgId = svgId.Trim().Trim('"');
                    }
                }
                //family.FamilyName = family.FamilyId.ToString();
                // Item 2: List Person name
                JsonArray arrayPerson = (JsonArray)arrayFamily[1];
                for (int i = 0; i < arrayPerson.Count; i++)
                {
                    JsonArray personInfoArray = (JsonArray)arrayPerson[i];
                    //family.Name += personInfoArray[0].ToString() + " - ";
                    PersonInfo familyMember = new PersonInfo(personInfoArray[0].ToString(), family);
                    familyMember.MANS_NAME_TU = personInfoArray[1].ToString();
                    familyMember.MANS_NAME_THUONG = personInfoArray[2].ToString();
                    familyMember.MANS_NAME_THUY = personInfoArray[3].ToString();
                    familyMember.MANS_ID = personInfoArray[4].ToString();
                    familyMember.fid = personInfoArray[5].ToString();
                    if(personInfoArray[6].ToString().Length>0)
                    {
                        try
                        {
                            familyMember.MANS_GENDER = Convert.ToInt32(personInfoArray[6].ToString()) == 1 ? "Nam" : "Nữ";
                        }
                        catch (Exception exx)
                        {
                            familyMember.MANS_GENDER = "Nam";
                        }
                    }
                    familyMember.MANS_DOB = personInfoArray[7].ToString();
                    familyMember.MANS_DOD = personInfoArray[8].ToString();
                    familyMember.MANS_WOD = personInfoArray[9].ToString();
                    familyMember.MANS_DETAIL = vietnamgiapha.Util.Base64Decode(personInfoArray[10].ToString());
                    familyMember.MANS_CONTHUMAY = personInfoArray[11].ToString();
                    familyMember.IsMainPerson = 0;
                    // Correct gender
                    if(familyMember.MANS_NAME_HUY.ToUpper().Contains(" VĂN "))
                    {
                        familyMember.MANS_GENDER = "Nam";
                    }
                    else if (familyMember.MANS_NAME_HUY.ToUpper().Contains(" THỊ "))
                    {
                        familyMember.MANS_GENDER = "Nữ";
                    }
                    family.ListPerson.Add(familyMember);
                }
                // 

                // Item 3: Child family
                if (arrayFamily.Count >= 3)
                {
                    JsonArray arrayChildren = (JsonArray)arrayFamily[2];
                    for (int i = 0; i < arrayChildren.Count; i++)
                    {
                        JsonArray children = (JsonArray)arrayChildren[i];
                        if (children != null)
                        {
                            FamilyInfo childPerson = new FamilyInfo();
                            GetFamily(childPerson, children);
                            family.FamilyChildren.Add(childPerson);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                log.Error("Có lỗi GetFamily.", ex);
            }
            return true;
        }
    }
}