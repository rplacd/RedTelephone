using System;
using System.Data.Objects;
using System.Data.Objects.DataClasses;
using System.Data.EntityClient;
using System.ComponentModel;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Data.Linq;

namespace RedTelephone.Models {

    public partial class ModelsDataContext : ObjectContext {
        //REFACTOR: why can't I just db.xs.orderbydescending(version).first?
        public IEnumerable<Office> newestOffices()
        {
            var officeCodes = Offices.Select(o => new { contract = o.contractCode, company = o.companyCode, code = o.code }).Distinct();
            List<Office> ret = new List<Office>();
            foreach (var query in officeCodes) {
                ret.Add(Offices.Where(e => e.contractCode == query.contract && e.companyCode == query.company && e.code == query.code)
                            .OrderByDescending(e => e.version).FirstOrDefault());
            }
            return ret;
        }

        public IEnumerable<Employee> newestEmployees()
        {
            var employeeCodes = Employees.Select(e => new { contract = e.contractCode, company = e.companyCode, code = e.code }).Distinct();
            List<Employee> ret = new List<Employee>();
            foreach (var query in employeeCodes) {
                ret.Add(Employees.Where(e => e.contractCode == query.contract && e.companyCode == query.company && e.code == query.code)
                            .OrderByDescending(e => e.version).FirstOrDefault());
            }
            return ret;
        }
    }

}