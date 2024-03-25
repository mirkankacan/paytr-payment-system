using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PayTR.Models
{
    public class PaytrDurumSorguResponse
    {
        public string Status { get; set; }
        public string net_tutar { get; set; }
        public string kesinti_tutari { get; set; }
        public string payment_amount { get; set; }
        public string payment_total { get; set; }
        public string payment_date { get; set; }
        public string currency { get; set; }
        public string taksit { get; set; }
        public string kart_marka { get; set; }
        public string masked_pan { get; set; }
        public string odeme_tipi { get; set; }
        public string test_mode { get; set; }
        public List<PaytrDurumSorguReturnItem> returns { get; set; }
        public string err_no { get; set; }
        public string err_msg { get; set; }
    }
}