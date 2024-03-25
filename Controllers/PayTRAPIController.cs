using Newtonsoft.Json.Linq;
using PayTR.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Http;

namespace PayTR.Controllers
{
    [RoutePrefix("api")]
    public class PayTRAPI : ApiController
    {
        private const string merchant_id = "1111111";
        private const string merchant_key = "xxxxxxxx";
        private const string merchant_salt = "zzzzzzzzzz";
        private const string TRANSFER_URL = "https://www.paytr.com/odeme/durum-sorgu";

        public static Guid GenerateGuid()
        {
            return Guid.NewGuid();
        }

        [HttpPost, Route("paytr-iframe")]
        public IHttpActionResult PayTRIFrame()
        {
            try
            {
                string vergi_tc = HttpContext.Current.Request.Params["vergi_tc"];
                string tutarStr = HttpContext.Current.Request.Params["tutar"];
                tutarStr = tutarStr.Replace(",", "").Replace("₺", "").Trim();
                int tutarInt = Convert.ToInt32(tutarStr);
                using (PayTREntities db = new PayTREntities())
                {
                    CARI_KAYIT cari = null;

                    if (vergi_tc.Length == 10)
                    {
                        cari = db.CARI_KAYIT.FirstOrDefault(x => x.VERGI_NUMARASI == vergi_tc);
                    }
                    else if (vergi_tc.Length == 11)
                    {
                        cari = db.CARI_KAYIT.FirstOrDefault(x => x.TCKIMLIKNO == vergi_tc);
                    }
                    if (cari != null)
                    {
                        string emailstr = !string.IsNullOrWhiteSpace(cari.EMAIL) ? cari.EMAIL : "test@test.com.tr";
                        string user_namestr = !string.IsNullOrWhiteSpace(cari.CARI_ISIM) ? cari.CARI_ISIM : "Test";
                        string user_addressstr = !string.IsNullOrWhiteSpace(cari.CARI_ADRES) ? cari.CARI_ADRES : "Test";
                        string user_phonestr = !string.IsNullOrWhiteSpace(cari.CARI_TEL) ? cari.CARI_TEL : "05000000000";
                        string merchant_oid = "MOID" + DateTime.Now.Year + GenerateGuid().ToString("N");
                        int payment_amountstr = tutarInt * 100;
                        string merchant_ok_url = "https://websitesi.com/basarili";
                        string merchant_fail_url = "https://websitesi.com/basarisiz";
                        string user_ip = GetInternalIPAddress();
                        if (user_ip == "" || user_ip == null || user_ip == "::1")
                        {
                            user_ip = GetExternalIPAddress();
                        }
                        object[][] user_basket = {
                          new object[] {"Hizmet Bedeli", payment_amountstr, 1}, // 1. ürün (Ürün Ad - Birim Fiyat - Adet)
                         };
                        string timeout_limit = "5";
                        string debug_on = "1";
                        string test_mode = "0";
                        string no_installment = "1";
                        string max_installment = "0";
                        string currency = "TL";
                        string lang = "tr";
                        string store_card_off = "1";
                        NameValueCollection data = new NameValueCollection();
                        data["merchant_id"] = merchant_id;
                        data["user_ip"] = user_ip;
                        data["merchant_oid"] = merchant_oid;
                        data["email"] = emailstr;
                        data["payment_amount"] = payment_amountstr.ToString();

                        System.Web.Script.Serialization.JavaScriptSerializer ser = new System.Web.Script.Serialization.JavaScriptSerializer();
                        string user_basket_json = ser.Serialize(user_basket);
                        string user_basketstr = Convert.ToBase64String(Encoding.UTF8.GetBytes(user_basket_json));
                        data["user_basket"] = user_basketstr;

                        string Birlestir = string.Concat(merchant_id, user_ip, merchant_oid, emailstr, payment_amountstr.ToString(), user_basketstr, no_installment, max_installment, currency, test_mode, merchant_salt);
                        HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(merchant_key));
                        byte[] b = hmac.ComputeHash(Encoding.UTF8.GetBytes(Birlestir));

                        data["paytr_token"] = Convert.ToBase64String(b);
                        data["debug_on"] = debug_on;
                        data["test_mode"] = test_mode;
                        data["no_installment"] = no_installment;
                        data["max_installment"] = max_installment;
                        data["user_name"] = user_namestr;
                        data["user_address"] = user_addressstr;
                        data["user_phone"] = user_phonestr;
                        data["merchant_ok_url"] = merchant_ok_url;
                        data["merchant_fail_url"] = merchant_fail_url;
                        data["timeout_limit"] = timeout_limit;
                        data["currency"] = currency;
                        data["store_card_off"] = store_card_off;
                        data["lang"] = lang;

                        using (WebClient client = new WebClient())
                        {
                            client.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                            byte[] result = client.UploadValues("https://www.paytr.com/odeme/api/get-token", "POST", data);
                            string ResultAuthTicket = Encoding.UTF8.GetString(result);
                            dynamic json = JValue.Parse(ResultAuthTicket);

                            if (json.status == "success")
                            {
                                if (Asama1MukerrerKontrol(merchant_oid) == false)
                                {
                                    Asama1Ekle(emailstr, tutarInt, user_namestr, user_phonestr, user_addressstr, user_ip, cari.CARI_KOD, merchant_oid);
                                }
                                string jToken = json.token.ToString();
                                string paytrUrl = "https://www.paytr.com/odeme/guvenli/" + jToken;
                                string[] dataArray = new string[] { paytrUrl, cari.CARI_ISIM };
                                return Ok(dataArray);
                            }
                            else
                            {
                                return BadRequest("PayTR ödeme sayfası oluşturulamadı. Hata: " + json.reason);
                            }
                        }
                    }
                    else
                    {
                        return BadRequest("Cari bulunamadı. Vergi numarasını kontrol ediniz.");
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private bool Asama1MukerrerKontrol(string merchant_oid)
        {
            if (!string.IsNullOrEmpty(merchant_oid))
            {
                using (PayTREntities db = new PayTREntities())
                {
                    bool asama1Kontrol = db.PAYTR_ASAMA1.Any(x => x.MERCHANT_OID == merchant_oid);
                    if (!asama1Kontrol)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            else
            {
                throw new Exception("MukerrerKontrol metodunda merchant_oid null");
            }
        }

        private void Asama1Ekle(string mail, int tutar, string adSoyad, string telefon, string adres, string icIp, string cariKodu, string m_oid)
        {
            if (!string.IsNullOrEmpty(mail) && !string.IsNullOrEmpty(adSoyad) && !string.IsNullOrEmpty(telefon) && tutar != 0 && !string.IsNullOrEmpty(adres) && !string.IsNullOrEmpty(icIp) && !string.IsNullOrEmpty(cariKodu) && !string.IsNullOrEmpty(m_oid))
            {
                using (PayTREntities db = new PayTREntities())
                {
                    PAYTR_ASAMA1 yeniAsama1 = new PAYTR_ASAMA1()
                    {
                        EMAIL = mail,
                        TUTAR = Convert.ToDecimal(tutar),
                        AD_SOYAD = adSoyad,
                        TELEFON = telefon,
                        ADRES = adres,
                        IC_IP = icIp,
                        CARI_KODU = cariKodu,
                        MERCHANT_OID = m_oid,
                        TARIH = DateTime.Now,
                    };
                    db.PAYTR_ASAMA1.Add(yeniAsama1);
                    db.SaveChanges();
                }
            }
        }

        [HttpPost, Route("callback-check")]
        public HttpResponseMessage Callback()
        {
            var response = new HttpResponseMessage();
            response.Content = new StringContent("OK", Encoding.UTF8, "text/plain");
            // PAYTR'ye yapılan bildirimde ne olursa olsun OK dönmelidir

            string merchant_oid = System.Web.HttpContext.Current.Request.Form["merchant_oid"];
            try
            {
                string status = System.Web.HttpContext.Current.Request.Form["status"];
                string total_amount = System.Web.HttpContext.Current.Request.Form["total_amount"];
                string hash = System.Web.HttpContext.Current.Request.Form["hash"];
                string payment_type = System.Web.HttpContext.Current.Request.Form["payment_type"];
                string currency = System.Web.HttpContext.Current.Request.Form["currency"];
                string payment_amount = System.Web.HttpContext.Current.Request.Form["payment_amount"];
                string Birlestir = string.Concat(merchant_oid, merchant_salt, status, total_amount);
                HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(merchant_key));
                byte[] b = hmac.ComputeHash(Encoding.UTF8.GetBytes(Birlestir));
                string token = Convert.ToBase64String(b);

                if (hash.ToString() != token) // PAYTR'den gelen bildirimde uyuşmazlık var
                {
                    return response;
                }
                if (CariHareketMukerrerKontrol(merchant_oid) == true)
                {
                    return response;
                }

                if (status.ToLower() == "success") // PAYTR'den gelen bildirim başarılı
                {
                    CariHareketEkle(merchant_oid);
                    MailGonder(merchant_oid);
                    return response;
                }
                else // PAYTR'den gelen bildirim başarısız
                {
                    var errorCode = string.IsNullOrWhiteSpace(System.Web.HttpContext.Current.Request.Form["failed_reason_code"]) ? "Not Request" : System.Web.HttpContext.Current.Request.Form["failed_reason_code"];
                    var errorMessage = string.IsNullOrWhiteSpace(System.Web.HttpContext.Current.Request.Form["failed_reason_msg"]) ? "Not Request" : System.Web.HttpContext.Current.Request.Form["failed_reason_msg"];
                    return response;
                }
            }
            catch (Exception ex)
            {
                return response;
            }
        }

        private bool CariHareketMukerrerKontrol(string merchant_oid)
        {
            if (!string.IsNullOrEmpty(merchant_oid))
            {
                using (PayTREntities db = new PayTREntities())
                {
                    bool cariHareketKontrol = db.CARI_HAREKET.Any(x => x.ACIKLAMA == merchant_oid);
                    if (!cariHareketKontrol)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            else
            {
                throw new Exception("MukerrerKontrol metodunda merchant_oid bilgisi alınamadı");
            }
        }

        private void CariHareketEkle(string merchant_oid)
        {
            using (PayTREntities db = new PayTREntities())
            {
                if (!string.IsNullOrEmpty(merchant_oid))
                {
                    var odemeAsama1 = db.PAYTR_ASAMA1.SingleOrDefault(x => x.MERCHANT_OID == merchant_oid);
                    if (odemeAsama1 != null)
                    {
                        decimal totalAmountToDecimal = Convert.ToDecimal(odemeAsama1.TUTAR);
                        CARI_HAREKET cariHareket = new CARI_HAREKET()
                        {
                            CARI_KODU = odemeAsama1.CARI_KODU,
                            TARIH = DateTime.Now,
                            ACIKLAMA = merchant_oid,
                            BORC = totalAmountToDecimal,
                            ALACAK = 0,
                            HAREKET_TIPI = "G",
                            KAYIT_ZAMAN = DateTime.Now,
                            AKTARIM = 0
                        };
                        db.CARI_HAREKET.Add(cariHareket);
                        db.SaveChanges();
                    }
                    else
                    {
                        throw new Exception("CariHareketEkle metodunda odeme bulunamadı");
                    }
                }
                else
                {
                    throw new Exception("CariHareketEkle metodunda eksik bilgiler var");
                }
            }
        }

        private void MailGonder(string merchant_oid)
        {
            CARI_HAREKET odeme = null;
            CARI_KAYIT cari = null;
            using (PayTREntities db = new PayTREntities())
            {
                odeme = db.CARI_HAREKET.SingleOrDefault(x => x.ACIKLAMA == merchant_oid);
                cari = db.CARI_KAYIT.SingleOrDefault(x => x.CARI_KOD == odeme.CARI_KODU);
            }
            if (odeme != null && cari != null)
            {
                MailAddress fromAddress = new MailAddress("test@test.com.tr", "Test Test");
                const string fromPassword = "test123"; 
                List<MailAddress> recipients = new List<MailAddress>
                {
                    new MailAddress("test@test.com.tr","Test Test"),
                };
                const string subject = "Ödeme Başarılı";
                string body = @"
                    <!DOCTYPE html>
                    <html lang=""en"">
                    <head>
                    <meta charset=""UTF-8"">
                    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                    <title>Bilgilendirme</title>
                    <style>
                        body {
                            font-family: Arial, sans-serif;
                            background-color: #f4f4f4;
                            margin: 0;
                            padding: 0;
                        }

                        .container {
                            max-width: 800px;
                            margin: 0 auto;
                            padding: 20px;
                            background-color: #ffffff;
                            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
                            border-radius: 5px;
                        }

                        h1 {
                            color: #333;
                        }

                        p {
                            color: #666;
                        }

                        .footer {
                            margin-top: 20px;
                            text-align: center;
                            color: #999;
                        }
                    </style>
                </head>
                <body>
                    <div class=""container"">
                        <p>
                            " + cari.CARI_ISIM + " carisi, " + odeme.TARIH.ToString("dd.MM.yyyy HH:mm") + " tarihinde " + odeme.BORC.ToString("C", new CultureInfo("tr-TR")) + " tutarında yükleme başarıyla yapılmıştır."
                            + @"
                        </p>

                    </div>
                  
                </body>
                </html>
                ";

                using (SmtpClient smtpClient = new SmtpClient("smtp-mail.outlook.com"))
                {
                    smtpClient.Port = 587;
                    smtpClient.EnableSsl = true;
                    smtpClient.UseDefaultCredentials = false;
                    smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtpClient.Credentials = new NetworkCredential(fromAddress.Address, fromPassword);
                    using (MailMessage mailMessage = new MailMessage())
                    {
                        mailMessage.From = fromAddress;

                        foreach (MailAddress recipient in recipients)
                        {
                            mailMessage.To.Add(recipient);
                        }

                        mailMessage.Subject = subject;
                        mailMessage.Body = body;
                        mailMessage.IsBodyHtml = true;
                        smtpClient.Send(mailMessage);
                    }
                }
            }
            else
            {
                throw new Exception("MailGonder metodunda odeme bulunamadı");
            }
        }

        public string GetExternalIPAddress()
        {
            using (HttpClient client = new HttpClient())
            {
                string url = "https://api64.ipify.org?format=json";
                HttpResponseMessage response = client.GetAsync(url).Result;
                if (response.IsSuccessStatusCode)
                {
                    string content = response.Content.ReadAsStringAsync().Result;
                    dynamic jsonResponse = Newtonsoft.Json.JsonConvert.DeserializeObject(content);
                    string publicIpAddress = jsonResponse.ip;
                    return publicIpAddress;
                }
                else
                {
                    throw new Exception("Dış IP Adresi bulunamadı.");
                }
            }
        }

        private string GetInternalIPAddress()
        {
            string internalIp = System.Web.HttpContext.Current.Request.UserHostAddress;
            if (!string.IsNullOrWhiteSpace(internalIp))
            {
                return internalIp;
            }
            else
            {
                throw new Exception("İç IP Adresi bulunamadı.");
            }
        }

        [HttpPost, Route("cari-kontrol")]
        public IHttpActionResult CariKontrol()
        {
            try
            {
                string vergi_tc_kontrol = HttpContext.Current.Request.Params["vergi_tc_kontrol"];

                using (PayTREntities db = new PayTREntities())
                {
                    if (vergi_tc_kontrol.Length == 10)
                    {
                        var vergi_cari_kontrol = db.CARI_KAYIT.FirstOrDefault(x => x.VERGI_NUMARASI == vergi_tc_kontrol);
                        if (vergi_cari_kontrol != null)
                        {
                            string[] cariArray = new string[] { "VAR", vergi_cari_kontrol.CARI_ISIM };
                            return Ok(cariArray);
                        }
                        else
                        {
                            return Ok("YOK");
                        }
                    }
                    else if (vergi_tc_kontrol.Length == 11)
                    {
                        var tc_cari_kontrol = db.CARI_KAYIT.FirstOrDefault(x => x.TCKIMLIKNO == vergi_tc_kontrol);
                        if (tc_cari_kontrol != null)
                        {
                            string[] cariArray = new string[] { "VAR", tc_cari_kontrol.CARI_ISIM };

                            return Ok(cariArray);
                        }
                        else
                        {
                            return Ok("YOK");
                        }
                    }
                    else { return BadRequest("String uzunluğu 10 ve 11'den farklı"); }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private void Karsilastir()
        {
            using (PayTREntities db = new PayTREntities())
            {
                var asama1Records = db.PAYTR_ASAMA1.Select(x => x.MERCHANT_OID).ToList();
                foreach (var merchantOid in asama1Records)
                {
                    // Check if the record exists in CARI_HAREKET
                    bool cariHareketKontrol = db.CARI_HAREKET.Any(x => x.ACIKLAMA == merchantOid);

                    if (!cariHareketKontrol)
                    {
                        string Birlestir = string.Concat(merchant_id, merchantOid, merchant_salt);
                        HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(merchant_key));
                        byte[] b = hmac.ComputeHash(Encoding.UTF8.GetBytes(Birlestir));
                        string paytr_token = Convert.ToBase64String(b);

                        NameValueCollection data = new NameValueCollection();
                        data["merchant_id"] = merchant_id;
                        data["merchant_oid"] = merchantOid;
                        data["paytr_token"] = paytr_token;

                        dynamic json;

                        using (WebClient client = new WebClient())
                        {
                            client.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                            byte[] result = client.UploadValues("https://www.paytr.com/odeme/durum-sorgu", "POST", data);
                            string ResultAuthTicket = Encoding.UTF8.GetString(result);
                            json = JValue.Parse(ResultAuthTicket);
                        }
                        if (json.status == "success")
                        {
                            string status_response = json.status.ToString();
                            string net_tutar_response = json.net_tutar.ToString();
                            string kesinti_tutari_response = json.kesinti_tutari.ToString();
                            string kesinti_orani_response = json.kesinti_orani.ToString();
                            string payment_amount_response = json.payment_amount.ToString();
                            string payment_total_response = json.payment_total.ToString();
                            string payment_date_response = json.payment_date.ToString();
                            string currency_response = json.currency.ToString();
                            string taksit_response = json.taksit.ToString();
                            string kart_marka_response = json.kart_marka.ToString();
                            string masked_pan_response = json.masked_pan.ToString();
                            string odeme_tipi_response = json.odeme_tipi.ToString();
                            string test_mode_response = json.test_mode.ToString();
                            string[] returns_response = json.returns.ToObject<string[]>();

                            PAYTR_KONTROL kontrolEkle = new PAYTR_KONTROL()
                            {
                                MERCHANT_OID = merchantOid,
                                STATUS = status_response,
                                NET_TUTAR = net_tutar_response,
                                KESINTI_TUTAR = kesinti_tutari_response,
                                KESINTI_ORAN = kesinti_orani_response,
                                PAYMENT_AMOUNT = payment_amount_response,
                                RESPONSE_TARIH = Convert.ToDateTime(payment_date_response).Date,
                                CURRENCY = currency_response,
                                TAKSIT = taksit_response,
                                KART_MARKA = kart_marka_response,
                                MASKED_PAN = masked_pan_response,
                                ODEME_TIPI = odeme_tipi_response,
                                TEST_MODE = test_mode_response
                            };
                            db.PAYTR_KONTROL.Add(kontrolEkle);
                            db.SaveChanges();
                        }
                        else
                        {
                            string err_no_response = json.err_no.ToString();
                            string err_msg_response = json.err_msg.ToString();
                        }
                    }
                }
            }
        }
    }
}