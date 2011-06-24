using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace RedTelephone.Controllers
{
    public class ReferenceDataController : RedTelephoneController
    {
        // Pretty much blank. This part is static content.
        public ActionResult Index()
        {
            return authenticatedAction(new String[]{"UR"}, () => View());
        }

    }
}
