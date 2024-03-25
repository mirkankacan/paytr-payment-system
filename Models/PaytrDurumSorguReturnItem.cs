using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PayTR.Models
{
    public class PaytrDurumSorguReturnItem
    {
        public string Amount { get; set; }
        public string Date { get; set; }
        public string Type { get; set; }
        public string DateCompleted { get; set; }
        public string AuthCode { get; set; }
        public string RefNum { get; set; }
    }
}