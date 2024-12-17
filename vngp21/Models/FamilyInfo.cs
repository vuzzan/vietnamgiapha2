using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vietnamgiapha
{

    public class FamilyInfo : INotifyPropertyChanged
    {
        public int FamilyId { get; set; }
        public int FamilyUp { get; set; }
        public int FamilyOrder { get; set; }
        public int FamilyLevel { get; set; }
        public int FamilyNew { get; set; }

        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public FamilyInfo()
        {
            FamilyNew = 1;
        }
        // LIST CHILD FAMILY IN FAMILY
        ObservableCollection<FamilyInfo> _familyChildren = new ObservableCollection<FamilyInfo>();
        public ObservableCollection<FamilyInfo> FamilyChildren
        {
            get { 
                return _familyChildren; 
            }
            set { 
                _familyChildren = value; 
            }
        }
        // LIST PERSON IN FAMILY
        private ObservableCollection<PersonInfo> _listPerson = new ObservableCollection<PersonInfo>();
        public ObservableCollection<PersonInfo> ListPerson
        {
            get { 
                return _listPerson; 
            }
            set { 
                _listPerson = value; 
            }
        }
        // NAME OF FAMILY
        public string Name { 
            get
            {
                String tmp = FamilyLevel+". ";
                foreach( var item in _listPerson)
                {
                    if (item.IsMainPerson == 1)
                    {
                        tmp += Util.RemoveSpecialChar(item.MANS_NAME_HUY) + " + ";
                        break;
                    }
                }
                foreach (var item in _listPerson)
                {
                    if (item.IsMainPerson == 0)
                    {
                        tmp += Util.RemoveSpecialChar(item.MANS_NAME_HUY) + " + ";
                    }
                }

                tmp = tmp.Substring(0, tmp.Length-2);
                if( FamilyNew == 1)
                {
                    tmp += "*";
                }
                return tmp;
            }
        }

        public string Name0
        {
            get
            {
                String tmp = "";
                if (_listPerson.Count >= 1)
                {
                    tmp += Util.RemoveSpecialChar(_listPerson[0].MANS_NAME_HUY) + " + ";
                    tmp = tmp.Substring(0, tmp.Length - 2);
                    if (FamilyNew == 1)
                    {
                        //tmp += "*";
                    }
                    return tmp;
                }
                else if (_listPerson.Count == 0)
                {
                }
                return "No";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public virtual void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public int GetMaxFamilyId(int maxFamilyId)
        {
            if (maxFamilyId < FamilyId)
            {
                maxFamilyId = FamilyId;
            }
            foreach(var f in _familyChildren)
            {
                maxFamilyId = f.GetMaxFamilyId(maxFamilyId);
            }
            return maxFamilyId;
        }
    }
}
