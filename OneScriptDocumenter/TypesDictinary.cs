﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OneScriptDocumenter
{
    class TypeInfo
    {
        public string fullName = "";
        public string ShortName = "";
        public string nameEng = "";
        public string nameRus = "";
    }

    class TypesDictinary
    {
        List<TypeInfo> list;

        public TypesDictinary()
        {
            if (System.IO.File.Exists(@"map.json"))
            {
                string jsonData = System.IO.File.ReadAllText(@"map.json");
                list = JsonConvert.DeserializeObject<List<TypeInfo>>(jsonData);
            }
            else
            {
                list = new List<TypeInfo>();
            }
        }

        public TypeInfo findByFullName(string fullName)
        {

            foreach(TypeInfo curType in list)
            {
                if (curType.fullName == fullName)
                {
                    return curType;
                }
            }

            return null;
        }

        public void add(TypeInfo value)
        {
            if (findByFullName(value.fullName) == null)
            {
                list.Add(value);
            }
        }

        public void save()
        {
            System.IO.File.WriteAllText(@"map.json", JsonConvert.SerializeObject(list));
        }

        public List<TypeInfo> types { get { return list; } }
    }
}
