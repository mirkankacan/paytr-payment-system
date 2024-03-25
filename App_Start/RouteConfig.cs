using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace PayTR
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");


            routes.MapRoute(
                 name: "Odeme",
                 url: "odeme",
             defaults: new { controller = "PayTR", action = "Odeme" }
         );
            routes.MapRoute(
                name: "Basarili",
                url: "basarili",
            defaults: new { controller = "PayTR", action = "Basarili" }
        );
            routes.MapRoute(
                name: "Basarisiz",
                url: "basarisiz",
            defaults: new { controller = "PayTR", action = "Basarisiz" }
        );
            routes.MapRoute(
              name: "Sorgu",
              url: "sorgu",
          defaults: new { controller = "PayTR", action = "Sorgu" }
      );
            routes.MapRoute(
            name: "Giris",
            url: "giris",
        defaults: new { controller = "PayTR", action = "Giris" }
    );
            routes.MapRoute(
              name: "Default",
              url: "{controller}/{action}/{id}",
              defaults: new { controller = "PayTR", action = "Odeme", id = UrlParameter.Optional }
          );
       
        }
    }
}
