using MahApps.Metro.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace vietnamgiapha
{
    public class Database
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger("m0");
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
        public static bool SaveJson(GiaPhaViewModel gpView)
        {
            try
            {
                if (gpView.GP.FileName.Length == 0)
                {
                    SaveFileDialog saveFileDialog = new SaveFileDialog();
                    saveFileDialog.DefaultExt = ".json";
                    saveFileDialog.Filter = "JSON files (*.json)|*.json";
                    if (saveFileDialog.ShowDialog() == true)
                    {
                        if(File.Exists(saveFileDialog.FileName))
                        {
                            if(MessageBox.Show("Có muốn lưu đè vào file có sẵn: " + saveFileDialog.FileName,
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
                    else {
                        log.Info("Không chọn được file để lưu.");
                        return false;
                    }
                    
                }
                else
                {
                    log.Info("Không chọn được file để lưu.");
                }
                log.Info("Begin save file: " + gpView.GP.FileName);
                System.IO.File.WriteAllText(gpView.GP.FileName, gpView.ToJson());
                //MessageBox.Show("Lưu File: " + gpView.GP.FileName, "Success");
                log.Info("End save file: " + gpView.GP.FileName);
                //
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Có lỗi mở file: " + ex.Message, "Có lỗi");
                log.Error(ex);
            }
            return false;
        }

        public static GiaphaInfo FromJson(string jsonFile)
        {
            try
            {
                log.Info("Begin load file Gia Phả: " + jsonFile);
                string json = File.ReadAllText(jsonFile);
                JsonArray array = (JsonArray)JsonArray.Parse(json);

                GiaphaInfo gp  = new GiaphaInfo();
                //RootFamily root = new RootFamily();
                gp.GiaphaId = Convert.ToInt32(array[0].ToString());
                gp.GiaphaName = array[1].ToString();
                gp.GiaphaNameRoot = vietnamgiapha.Util.GetFirstWord(gp.GiaphaName);
                gp.PhaKy = vietnamgiapha.Util.Base64Decode( array[3].ToString() );
                gp.ThuyTo = vietnamgiapha.Util.Base64Decode(array[4].ToString());
                gp.Tocuoc= vietnamgiapha.Util.Base64Decode(array[5].ToString());
                gp.HuongHoa = vietnamgiapha.Util.Base64Decode(array[6].ToString());
                gp.RF_OTAI = array[7].ToString();
                gp.RF_DAYS = array[8].ToString();
                gp.RF_CHANNGON = array[9].ToString();
                JsonArray arrayFamily = (JsonArray)array[2];

                // THUY TO
                FamilyInfo family = new FamilyInfo();
                if (GetFamily(family, arrayFamily) == true)
                {
                    //OK
                }

                gp.familyRoot = family;
                // Check all to get the Family Root Name
                if (family.ListPerson.Count>0)
                {
                    String firstName = family.ListPerson[0].MANS_NAME_HUY;
                    gp.GiaphaNameRoot = vietnamgiapha.Util.GetFirstWord(firstName);
                }
                // Update main person
                UpdateFamilyMain(gp.familyRoot, gp.GiaphaNameRoot);
                log.Info("Begin load file Gia Phả: " + jsonFile + ". Thành công");
                //
                return gp;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Có lỗi mở file: " + ex.Message, "Có lỗi");
                log.Error("Có lỗi load file Gia Phả: " + jsonFile + ".");
                log.Error(ex);
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
                        for (int i = 0; i < family.ListPerson.Count; i++)
                        {
                            // Add first person
                            if (family.ListPerson[i].MANS_NAME_HUY.ToLower().Contains(rootName.ToLower()))
                            {
                                // YES
                                family.ListPerson[i].IsMainPerson = 1;
                                _listPerson.Add(family.ListPerson[i]);
                                break;
                            }
                        }

                        // add other
                        for (int i = 0; i < family.ListPerson.Count; i++)
                        {
                            // Add first person
                            if (_listPerson.Count > 0 && family.ListPerson[i].MANS_ID != _listPerson[0].MANS_ID)
                            {
                                family.ListPerson[i].IsMainPerson = 0;
                                _listPerson.Add(family.ListPerson[i]);
                            }
                        }
                    }
                    
                    //
                    family.ListPerson = _listPerson;
                }
                //
                for (int i = 0; i < family.FamilyChildren.Count; i++)
                {
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
        private static bool GetFamily(FamilyInfo family, JsonArray arrayFamily)
        {
            try
            {
                // Item 1 : Family info 
                JsonArray arrayFamilyInfo = (JsonArray)arrayFamily[0];
                family.FamilyId = Convert.ToInt16(arrayFamilyInfo[0].ToString());
                family.FamilyOrder = Convert.ToInt32(arrayFamilyInfo[2].ToString());
                family.FamilyLevel = Convert.ToInt32(arrayFamilyInfo[1].ToString());
                family.FamilyUp = Convert.ToInt32(arrayFamilyInfo[3].ToString());
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
                    familyMember.MANS_GENDER = Convert.ToInt16(personInfoArray[6].ToString()) == 1 ? "Nam" : "Nữ";
                    familyMember.MANS_DOB = personInfoArray[7].ToString();
                    familyMember.MANS_DOD = personInfoArray[8].ToString();
                    familyMember.MANS_WOD = personInfoArray[9].ToString();
                    familyMember.MANS_DETAIL = vietnamgiapha.Util.Base64Decode(personInfoArray[10].ToString());
                    familyMember.MANS_CONTHUMAY = personInfoArray[11].ToString();
                    familyMember.IsMainPerson = 0;

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
                log.Error("Có lỗi GetFamily.");
                log.Error(ex);
                MessageBox.Show("Error: " + ex.Message);
            }
            return true;
        }
    }
}