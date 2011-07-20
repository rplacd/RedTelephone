using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Objects;
using RedTelephone.Models;


namespace RedTelephone.Controllers
{
    public class OfficesAndEmployeesController : RedTelephoneController
    {
        ModelsDataContext db = new ModelsDataContext();
        public ActionResult Index()
        {
            return authenticatedAction( new String[]{ "UR" }, () =>  {
                var contracts = db.Contracts;
                ViewData["Contracts"] = contracts;
                {
                    var temp_firstCt = contracts.FirstOrDefault();
                    if(temp_firstCt == default(Contract)) {
                        ViewData["FirstContractCode"] = new Decimal();
                        ViewData["InitCompanies"] = new List<Company>();
                    } else {
                        ViewData["FirstContractCode"] = temp_firstCt.code;
                        ViewData["InitCompanies"] = db.Companies.Where(c => c.contractCode == temp_firstCt.code);
                    }
                }
                return View();
            });
        }

        public String[][] parseTabDelimitedFile(Stream input)
        {
            var reader = new StreamReader(input);

            //read in all the lines first
            var lineStrs = new List<String>();
            {
                var temp_line = default(string);
                Func<bool> update = ()=>{ 
                    temp_line = reader.ReadLine();
                    return temp_line != default(string);
                };
                while(update()) {
                    lineStrs.Add(temp_line);
                }
            }

            //map over them, split by tab.
            var foo = lineStrs.Select(s => s.Split(new char[]{ '\t' })).ToArray();
            return foo;
        }

        public class UpdateResult {
            public enum t_tag { Updated, Created, Ignored, Error };
            public t_tag tag;
            public String description;
            public String code;
            public String error_message; //god I hate C#'s lack of ADTs.
        }
        //should we do the actual parsing + updating in a seperate thread from this as well?
        [HttpPost]
        public ActionResult Index(FormCollection collection)
        {
            return authenticatedAction(new String[] { "UR" }, () => {
                Decimal contractCode = Convert.ToDecimal(collection["contract"]);
                Decimal companyCode = Convert.ToDecimal(collection["company"]);
                IQueryable<Office> eligibleOffices = db.Offices.Where(o => o.companyCode == companyCode &&
                                                                o.contractCode == contractCode);
                IQueryable<Employee> eligibleEmployees = db.Employees.Where(e => e.contractCode == contractCode &&
                                                                e.companyCode == companyCode);
                short officeMaxSortIndex = 0;
                if (eligibleOffices.Count() > 0)
                    officeMaxSortIndex = eligibleOffices.Max(o => o.sortIndex);
                short employeeMaxSortIndex = 0;
                if(eligibleEmployees.Count() > 0)
                    employeeMaxSortIndex = eligibleEmployees.Max(e => e.sortIndex);
                List<UpdateResult> officeResults = new List<UpdateResult>();
                List<UpdateResult> employeeResults = new List<UpdateResult>();

                Action<Stream, Int32, Action<String[]>> doBody = (stream, colCount, body) => {
                    var contents = parseTabDelimitedFile(stream);
                    foreach (var line in contents) {
                        if (line.Count() != colCount) {
                            continue;
                        } else {
                            body(line);
                        }
                    }
                };

                if (Request.Files["officesdelta"] != null) {
                    doBody(Request.Files["officesdelta"].InputStream, 2, (line) => {
                        String code = line[0]; String description = line[1];
                        Office possibleOffice = eligibleOffices.Where(o => o.code == code)
                                                                .OrderByDescending(o => o.version)
                                                                .FirstOrDefault();
                        Office newOffice;

                        UpdateResult.t_tag result;
                        if (possibleOffice == null) {
                            result = UpdateResult.t_tag.Created;
                            newOffice = new Office {
                                contractCode = contractCode,
                                companyCode = companyCode,
                                code = code,
                                version = 0,
                                description = description,
                                sortIndex = ++officeMaxSortIndex,
                                active_p = "A"
                            };
                            db.Offices.AddObject(newOffice);
                        } else if (possibleOffice.description == description) {
                            result = UpdateResult.t_tag.Ignored;
                        } else {
                            result = UpdateResult.t_tag.Updated;
                            newOffice = new Office {
                                contractCode = possibleOffice.contractCode,
                                companyCode = possibleOffice.companyCode,
                                code = possibleOffice.code,
                                version = 1 + possibleOffice.version,
                                description = description,
                                sortIndex = possibleOffice.sortIndex,
                                active_p = possibleOffice.active_p
                            };
                            db.Offices.AddObject(newOffice);
                        }
                        officeResults.Add(new UpdateResult { tag = result, code = code, description = description });
                    });
                }

                db.SaveChanges();

                if (Request.Files["employeesdelta"] != null) {
                    doBody(Request.Files["employeesdelta"].InputStream, 3, (line) => {
                        String code = line[0]; String firstName = line[1]; String lastName = line[2];
                        Employee possibleEmployee = eligibleEmployees.Where(e => e.code == code)
                                                                .OrderByDescending(e => e.version)
                                                                .FirstOrDefault();
                        Employee newEmployee;

                        UpdateResult.t_tag result;
                        if (possibleEmployee == null) {
                            result = UpdateResult.t_tag.Created;
                            newEmployee = new Employee {
                                contractCode = contractCode,
                                companyCode = companyCode,
                                code = code,
                                version = 0,
                                firstName = firstName,
                                lastName = lastName,
                                sortIndex = ++employeeMaxSortIndex,
                                active_p = "A"
                            };
                            db.Employees.AddObject(newEmployee);
                        } else if (possibleEmployee.firstName == firstName && possibleEmployee.lastName == lastName) {
                            result = UpdateResult.t_tag.Ignored;
                        } else {
                            result = UpdateResult.t_tag.Updated;
                            newEmployee = new Employee {
                                contractCode = possibleEmployee.contractCode,
                                companyCode = possibleEmployee.companyCode,
                                code = code,
                                version = 1 + possibleEmployee.version,
                                firstName = firstName,
                                lastName = lastName,
                                sortIndex = possibleEmployee.sortIndex,
                                active_p = possibleEmployee.active_p
                            };
                        }
                        employeeResults.Add(new UpdateResult { tag = result, code = code, description = firstName + " " + lastName });
                    });
                }

                db.SaveChanges();
                ViewData["OfficeResults"] = officeResults;
                ViewData["EmployeeResults"] = employeeResults;
                return View("Results");
            });
        }
    }
}
