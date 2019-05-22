using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Data;

namespace JurisUtilityBase
{
    public class TimeEntry
    {
        public int ID { get; set; }
        public string ClientNo { get; set; }
        public string MatterNo { get; set; }
        public string Timekeeper { get; set; }
        public string Date { get; set; }
        public decimal hours { get; set; }
        public decimal amount { get; set; }
        public bool isBillable { get; set; }
        public int oldEntryStatus { get; set; }
        public string explanation { get; set; }
        public string ExpCode { get; set; }
        public decimal quantity { get; set; }
        public bool Summarize { get; set; }
        public int newEntryStatus { get; set; }



    }




}
