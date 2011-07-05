using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Data.Linq;


namespace RedTelephone.Models {
    partial class ModelsDataContext {
        //REFACTOR: why can't I just db.xs.orderbydescending(version).first?
        public IEnumerable<Office> newestOffices()
        {
            var officesByName = Offices.GroupBy(o => o.code).ToList();
            Dictionary<String, int> highestOfficeVersions = new Dictionary<string, int>();
            foreach (IGrouping<String, Office> group in officesByName) {
                highestOfficeVersions.Add(group.Key, group.Max(o => o.version));
            }
            return officesByName.Select((IGrouping<String, Office> go) =>
                go.FirstOrDefault(o => o.version == highestOfficeVersions[go.Key])
            );
        }

        public IEnumerable<Employee> newestEmployees()
        {
            var employeesByName = Employees.GroupBy(o => o.code).ToList();
            Dictionary<String, int> highestEmployeeVersions = new Dictionary<string, int>();
            foreach (IGrouping<String, Employee> group in employeesByName) {
                highestEmployeeVersions.Add(group.Key, group.Max(o => o.version));
            }
            return employeesByName.Select((IGrouping<String, Employee> go) =>
                go.FirstOrDefault(o => o.version == highestEmployeeVersions[go.Key])
            );
        }
    }
}
