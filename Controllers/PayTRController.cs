using System.Web.Mvc;

namespace PayTR.Controllers
{
    public class PayTRController : Controller
    {
        [Route("odeme")]
        public ActionResult Odeme()
        {
            return View();
        }

        [Route("basarili")]
        public ActionResult Basarili()
        {
            return View();
        }

        [Route("basarisiz")]
        public ActionResult Basarisiz()
        {
            return View();
        }
    }
}