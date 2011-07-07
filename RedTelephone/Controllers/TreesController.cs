using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Jayrock.Json;
using RedTelephone.Models;
using RedTelephone.Extensions;

namespace RedTelephone.Extensions {
    public static partial class Extensions {
        public static void writeSharedJSONMembers(this JsonWriter w, dynamic subject)
        {
            w.WriteStartObject();
            w.WriteMember("code");
            w.WriteString(subject.code.ToString());
            w.WriteMember("description");
            w.WriteString(subject.description);
        }
        public static void writeSharedJSONProlog(this JsonWriter w)
        {
            w.WriteMember("children");
            w.WriteStartArray();
        }
        public static void writeSharedJSONEpilog(this JsonWriter w)
        {
            w.WriteEndArray();
            w.WriteEndObject();
        }
    }
}


namespace RedTelephone.Controllers
{
    public class TreesController : RedTelephoneController
    {
        ModelsDataContext db = new ModelsDataContext();
        //two JSON accessors that simply dump the contents of the Sources tree and the Contract tree.
        //this is so that the little checkboxes can change clientside, and so that the link between parent and children rows
        //are enforced - something we don't do in the DB.
        private ContentResult makeJSONResult()
        //I don't want to use JSONResult, so set up a ContentResult properly.
        {
            return new ContentResult() { ContentType = "application/json" };
        }

        //IEnumerable<T> WhereActiveOrderBy(
        //REFACTOR: we're building things up in memory, and inefficiently as well...
        //MESS: NAIVE: these are nasty messes of code as well.
        public ActionResult Contracts()
        {
            //get a list with the newest employees/offices for each employee/office code.
            var newestOffices = db.newestOffices();
            var newestEmployees = db.newestEmployees();
            
            return authenticatedAction(new String[] { "UT", "UR" }, () => {
                content:
                var result = makeJSONResult();
                using (JsonTextWriter w = new JsonTextWriter()) {
                    w.WriteStartArray();
                    foreach (Contract c in db.Contracts.WAOBTL()) {
                        w.writeSharedJSONMembers(c);
                        w.writeSharedJSONProlog();
                        foreach (Company co in db.Companies.Where(tco => tco.contractCode == c.code).WAOBTL()) {
                            w.writeSharedJSONMembers(co);
                            w.WriteMember("offices");
                            w.WriteStartArray();
                            foreach (Office o in newestOffices
                                .Where(o => o.companyCode == co.code)
                                .Where(o => o.contractCode == c.code)
                                .WAOBTL()
                                 ) {
                                w.WriteStartObject();
                                //LOOK AT THIS! WE'RE NOT JUST SENDING OVER THE CODE, BUT THE VERSION AS WELL!
                                w.WriteMember("code");
                                w.WriteString(o.code + "?" + o.version.ToString());
                                w.WriteMember("description");
                                w.WriteString(o.description);
                                w.WriteEndObject();
                            }
                            w.WriteEndArray();
                            w.WriteMember("employees");
                            w.WriteStartArray();
                            foreach (Employee e in newestEmployees
                                .Where(e => e.contractCode == c.code)
                                .Where(e => e.companyCode == c.code)
                                .WAOBTL()) {
                                w.WriteStartObject();
                                //LOOK AT THIS! WE'RE NOT JUST SENDING OVER THE CODE, BUT THE VERSION AS WELL!
                                w.WriteMember("code");
                                w.WriteString(e.code + "?" + e.version.ToString());
                                w.WriteMember("description");
                                w.WriteString(e.firstName + " " + e.lastName);
                                w.WriteEndObject();
                            }
                            w.WriteEndArray();
                            w.WriteEndObject();
                        }
                        w.writeSharedJSONEpilog();
                    }
                    w.WriteEndArray();
                    result.Content = w.ToString();
                }
                logger.Debug("TreesController.Contracts accessed.");
                return result;
            });
        }

        public ActionResult Sources()
        {
            return authenticatedAction(new String[] { "UT", "UR" }, () => {
                var result = makeJSONResult();
                using (JsonTextWriter w = new JsonTextWriter()) {
                    w.WriteStartArray();
                    foreach (IssueSourceLvl1 i in db.IssueSourceLvl1s.WAOBTL()) {
                        w.writeSharedJSONMembers(i);
                        w.writeSharedJSONProlog();
                        foreach (IssueSourceLvl2 i2 in db.IssueSourceLvl2s.Where(ti2 => ti2.parentCode == i.code).WAOBTL()) {
                            w.writeSharedJSONMembers(i2);
                            w.writeSharedJSONProlog();
                            foreach (IssueSourceLvl3 i3 in db.IssueSourceLvl3s
                                .Where(ti3 => ti3.grandparentCode == i.code)
                                .Where(ti3 => ti3.parentCode == i2.code)
                                .WAOBTL()) {
                                w.writeSharedJSONMembers(i3);
                                w.WriteEndObject();
                            }
                            w.writeSharedJSONEpilog();
                        }
                        w.writeSharedJSONEpilog();
                    }
                    w.WriteEndArray();
                    result.Content = w.ToString();
                }
                logger.Debug("TreesController.Sources accessed.");
                return result;
            });
        }
    }
}
